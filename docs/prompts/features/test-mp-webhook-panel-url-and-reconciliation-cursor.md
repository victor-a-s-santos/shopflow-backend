Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Mercado Pago Orders API, webhooks, workers, idempotência e integração de pagamentos.

Contexto:
O Pix Mercado Pago já é criado corretamente via Orders API.

Logs reais recentes:

* ProviderOrderId: ORDTST01KXHJ8H6SDRPZQV72MAXBTV3X
* ProviderPaymentId: PAY01KXHJ8H7FMQM5F1V5FW7XFMJR
* Webhook real chegou.
* body_application_id bate com configured_application_id.
* body_user_id bate com configured_user_id.
* body_live_mode=false e configured_environment=Sandbox.
* query data.id existe.
* x-request-id existe.
* x-signature existe.
* SDK oficial Mercado Pago rejeitou:
  sdk_signature_valid=false
  sdk_exception_type=InvalidWebhookSignatureException
* Manual também rejeitou.
* Evento aprovado chegou como:
  action=order.processed
  data.status=processed
  data.status_detail=accredited
  payment_method.id=pix
  payment_method.type=bank_transfer

Conclusão:
O problema não parece mais ser algoritmo HMAC. O SDK oficial também rejeitou.
A suspeita forte agora é configuração/canal do webhook.

A documentação Mercado Pago anexada diz:
“As URLs configuradas durante a criação do pagamento terão prioridade sobre aquelas configuradas através de Suas integrações.”

O backend provavelmente envia `notification_url` no POST /v1/orders.
Precisamos testar se isso está fazendo o webhook seguir um caminho diferente da URL configurada no painel Webhooks.

Objetivos:

1. Tornar o envio de `notification_url` no POST /v1/orders configurável.
2. Permitir testar o fluxo usando somente a URL de Webhook configurada no painel Mercado Pago.
3. Manter logs seguros informando se `notification_url` foi enviada.
4. Implementar ou preparar uma reconciliação segura de Pix pendentes por GET /v1/orders/{ProviderOrderId}, para não depender exclusivamente do webhook no MVP.

Não mexer no frontend.
Não expor AccessToken, WebhookSecret, x-signature completa, v1 completo, copyPasteCode ou dados sensíveis.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Ler:

* MercadoPagoPixPaymentProvider
* MercadoPagoOrderClient
* MercadoPago options/config
* ProcessMercadoPagoPixWebhookCommandHandler
* Webhook signature validator SDK/manual
* Worker de expiração
* PaymentsPix DbContext
* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments/MP-PIX-003-webhook-raw-capture-temporary.md
* deploy/.env.test.example
* deploy/.env.hml.example

==================================================
2. CONFIGURAR ENVIO DE NOTIFICATION_URL
=======================================

Adicionar configuração:

MercadoPago__SendNotificationUrlInOrderCreate=true/false

Comportamento:

* Se true:

  * enviar notification_url no payload POST /v1/orders, como hoje.
* Se false:

  * NÃO enviar notification_url no payload POST /v1/orders.
  * Mercado Pago deve usar a URL configurada no painel Webhooks.

Default:

* Preservar comportamento atual se não houver env, para evitar breaking change.
* Mas documentar recomendação de teste:
  MercadoPago__SendNotificationUrlInOrderCreate=false

Logs seguros na criação da order:

* MercadoPago notification_url sent: true/false
* NotificationUrl configured: true/false
* Nunca logar AccessToken.

Critério:

* Permitir gerar Pix real novo sem notification_url no payload.

==================================================
3. AJUSTAR ENV TESTE/HML
========================

Atualizar exemplos:

No env.test/hml, para testar painel:

MercadoPago__SendNotificationUrlInOrderCreate=false

Manter:

MercadoPago__NotificationUrl=https://api-teste.vipassessoriadigital.com.br/api/payments/pix/webhooks/mercado-pago

Observação:
NotificationUrl ainda é útil para documentação/startup/checklist, mas não deve ser enviada no payload quando SendNotificationUrlInOrderCreate=false.

==================================================
4. TESTE OPERACIONAL ESPERADO
=============================

Depois do deploy:

1. Configurar no painel Mercado Pago:

   * Modo teste
   * URL teste:
     https://api-teste.vipassessoriadigital.com.br/api/payments/pix/webhooks/mercado-pago
   * Evento: Order (Mercado Pago)
   * Secret da tela Webhooks

2. Definir:
   MercadoPago__SendNotificationUrlInOrderCreate=false

3. Recriar api-test.

4. Gerar novo Pix.

5. Validar logs:

   * notification_url sent: false
   * webhook real chega
   * sdk_signature_valid=true

Se com notification_url=false o SDK passar, documentar causa:

* URL enviada na criação da order tinha prioridade e gerava assinatura incompatível com o secret do painel, ou comportamento diferente no Mercado Pago.

Se continuar falhando:

* Redefinir secret no painel.
* Atualizar env.
* Recriar API.
* Gerar Pix novo.

==================================================
5. RECONCILIAÇÃO DE PIX PENDENTES
=================================

Implementar ou preparar fallback seguro para MVP:

Criar serviço/command:

ReconcilePendingMercadoPagoPixPaymentsCommand

Objetivo:
Consultar periodicamente PixPayments pendentes com Provider=MercadoPago e ProviderOrderId preenchido, chamando:

GET /v1/orders/{ProviderOrderId}

Usar a resposta do Mercado Pago como fonte de verdade.

Regras:

* Só processar PixPayment Status=Pending.
* Só Provider=MercadoPago.
* Só ProviderOrderId não vazio.
* Respeitar batch size.
* Não processar pagamentos já Paid/Canceled/Expired.
* Idempotente.
* Se GET retornar action_required/waiting_transfer:

  * manter Pending.
* Se GET retornar processed/accredited:

  * confirmar reserva;
  * marcar PixPayment Paid;
  * marcar Order Paid.
* Se Order já Paid:

  * sucesso idempotente.
* Se reserva já confirmada:

  * sucesso idempotente.
* Se canceled/expired/failed:

  * aplicar regra existente se houver, ou documentar pendência.
* Logar status seguro.
* Não expor AccessToken.

Config sugerida:

MercadoPagoReconciliation__Enabled=true
MercadoPagoReconciliation__IntervalSeconds=60
MercadoPagoReconciliation__BatchSize=20
MercadoPagoReconciliation__MaxAgeMinutes=180

Pode rodar no Worker existente, junto com ExpirationWorker, ou como HostedService separado no worker.

Importante:
A reconciliação não substitui webhook. Ela é fallback operacional.
A documentação do Mercado Pago permite obter o recurso por GET /v1/orders/{id} após a notificação; para MVP, isso também ajuda a recuperar evento não processado.

==================================================
6. TESTES OBRIGATÓRIOS
======================

Criar/ajustar testes para:

1. Quando SendNotificationUrlInOrderCreate=true:

   * payload POST /v1/orders inclui notification_url.
2. Quando SendNotificationUrlInOrderCreate=false:

   * payload POST /v1/orders NÃO inclui notification_url.
3. Log informa notification_url sent true/false.
4. Reconciliation busca apenas PixPayment Pending/MercadoPago.
5. Reconciliation ignora Fake.
6. Reconciliation ignora Paid.
7. Reconciliation com GET action_required/waiting_transfer mantém Pending.
8. Reconciliation com GET processed/accredited marca PixPayment Paid.
9. Reconciliation com GET processed/accredited marca Order Paid.
10. Reconciliation confirma reserva.
11. Reconciliation duplicada é idempotente.
12. Reconciliation trata GET 404/400 sem quebrar batch.
13. Reconciliation trata timeout/5xx com retry na próxima rodada.
14. Worker não processa se Enabled=false.
15. Webhook continua funcionando igual.
16. Raw capture continua gated Testing/HML e não Production.

Não chamar Mercado Pago real nos testes.

==================================================
7. DOCUMENTAÇÃO
===============

Atualizar:

* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments/MP-PIX-003-webhook-raw-capture-temporary.md
* docs/payments-pix.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/technical-debt.md
* deploy/.env.test.example
* deploy/.env.hml.example

Documentar:

* Diferença entre URL do painel Webhooks e notification_url enviada na criação da order.
* Segundo a documentação Mercado Pago, URLs enviadas na criação têm prioridade.
* Como testar com SendNotificationUrlInOrderCreate=false.
* Como validar sdk_signature_valid=true.
* Como a reconciliação funciona.
* Que reconciliação é fallback, não substitui webhook.
* Como desligar raw capture depois do diagnóstico.

==================================================
8. RESULTADO ESPERADO
=====================

Ao final, retorne:

1. Arquivos alterados.
2. Config nova criada.
3. Se notification_url agora é opcional.
4. Como testar com painel Webhooks.
5. Se reconciliação foi implementada ou apenas preparada.
6. Testes criados/alterados.
7. Resultado dotnet build.
8. Resultado dotnet test.
9. Próximo teste operacional recomendado.

Critérios de aceite:

* É possível criar Pix Mercado Pago sem enviar notification_url no payload.
* Com SendNotificationUrlInOrderCreate=false, a URL do painel deve ser usada.
* Webhook continua validado por SDK.
* Reconciliation consegue marcar Paid via GET /v1/orders quando Mercado Pago retorna processed/accredited.
* Fluxo é idempotente.
* Nenhum secret/token é logado.
* dotnet build passa.
* dotnet test passa.
