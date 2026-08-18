Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, segurança de pagamentos, Pix, webhooks e Mercado Pago Checkout Transparente via Orders API.

Objetivo:
Corrigir/implementar a integração Pix do Mercado Pago usando Checkout Transparente via Orders API.

Atenção:
A integração Pix NÃO deve usar `/v1/payments` como fluxo principal.
Para Checkout Transparente via Orders, a criação do Pix deve usar:

POST https://api.mercadopago.com/v1/orders

E a confirmação no webhook deve consultar:

GET https://api.mercadopago.com/v1/orders/{id}

O evento configurado no painel do Mercado Pago deve ser:

Order

Não usar evento payment para este fluxo.

==================================================

1. CONTEXTO DO SHOPFLOW
   ==================================================

O Shopflow já possui:

* Catalog real.
* Inventory real.
* CheckoutSession real.
* Orders real.
* PaymentsPix com IPixPaymentProvider.
* FakePixPaymentProvider.
* POST /api/payments/pix/orders/{orderId}
* PixPayment nasce Pending.
* Order nasce PendingPayment.
* Inventory mantém reserva de estoque.
* Worker de expiração existe.
* Admin Auth real.
* Customer Auth real.
* HML/teste com domínio HTTPS.
* Mercado Pago app criada com credenciais de teste.
* Frontend já chama POST /api/payments/pix/orders/{orderId}.
* Frontend já está preparado para exibir QR/copia-e-cola quando backend retornar valores não-null.

Importante:

* Não implementar cartão.
* Não implementar boleto.
* Não implementar Checkout Pro.
* Não redirecionar o cliente para Mercado Pago como fluxo principal.
* Não usar JWT/localStorage.
* Não expor Access Token no frontend.
* Não commitar credenciais.
* Não mexer no frontend nesta etapa, salvo se algum contrato backend já estiver quebrado e precisar ser documentado para etapa futura.
* Não marcar pagamento como Paid sem consultar a Order real no Mercado Pago.
* Não confirmar estoque se a Order Mercado Pago não estiver aprovada.
* Não criar endpoint simulate-paid.
* Não quebrar Fake provider em Development/Test se MercadoPago estiver disabled.

==================================================
2. LEITURA OBRIGATÓRIA
======================

Antes de implementar, leia:

* docs/prompts/00-project-context.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/payments-pix.md
* docs/orders.md
* docs/cart-checkout.md
* docs/expiration-worker.md
* módulo PaymentsPix completo
* IPixPaymentProvider atual
* FakePixPaymentProvider atual
* MercadoPagoPixPaymentProvider, se já existir
* PixPayment aggregate
* PaymentsPixDbContext/migrations
* módulo Orders
* módulo Inventory
* services de reserva/confirm/cancel do Inventory
* endpoint POST /api/payments/pix/orders/{orderId}
* middleware CSRF atual
* Program.cs
* appsettings atuais
* docker-compose/deploy env examples
* testes PaymentsPix atuais
* testes Orders/Inventory relacionados

Também revisar qualquer implementação anterior que use:

* POST /v1/payments
* GET /v1/payments/{id}
* evento payment

Se existir, refatorar para Orders API.

==================================================
3. CONFIGURAÇÃO
===============

Criar/ajustar configuração:

MercadoPago:
Enabled: false
Environment: Sandbox
BaseUrl: https://api.mercadopago.com
AccessToken: ""
PublicKey: ""
WebhookSecret: ""
NotificationUrl: ""
PixExpirationMinutes: 30
WebhookSignatureToleranceMinutes: 10
SandboxPayerFirstNameOverride: ""
SandboxTestPayerEmail: "[test_user_br@testuser.com](mailto:test_user_br@testuser.com)"

Variáveis esperadas:

MercadoPago__Enabled=true
MercadoPago__Environment=Sandbox
MercadoPago__BaseUrl=https://api.mercadopago.com
MercadoPago__AccessToken=APP_USR-...
MercadoPago__PublicKey=...
MercadoPago__WebhookSecret=...
MercadoPago__NotificationUrl=https://api-hml.seudominio.com.br/api/payments/pix/webhooks/mercado-pago
MercadoPago__PixExpirationMinutes=30
MercadoPago__WebhookSignatureToleranceMinutes=10

Para teste sandbox, permitir opcionalmente:

MercadoPago__SandboxPayerFirstNameOverride=APRO

Regras:

* AccessToken é obrigatório quando MercadoPago__Enabled=true.
* WebhookSecret é obrigatório quando MercadoPago__Enabled=true em HML/Production.
* Em Development/Test com Enabled=false, continuar usando FakePixPaymentProvider.
* PublicKey pode ser documentada, mas não deve ser usada como segredo.
* Não commitar .env real.
* Atualizar deploy/.env.*.example sem secrets reais.
* Não usar CPF fake em Production.
* Não alterar o valor real do pedido Shopflow para forçar teste. O total enviado ao Mercado Pago deve bater com o total do Order/PixPayment.

Observação sobre testes Pix Mercado Pago:
Na documentação de teste Pix via Orders, o nome `APRO` no campo `payer.first_name` é usado para simular fluxo que retorna `action_required/waiting_transfer` e depois atualiza automaticamente para aprovado. Permitir esse override apenas em Sandbox, nunca em Production.

==================================================
4. PROVIDER MERCADO PAGO VIA ORDERS API
=======================================

Criar/ajustar:

MercadoPagoPixPaymentProvider

Implementar IPixPaymentProvider usando Orders API.

Endpoint correto:

POST /v1/orders

Headers:

* Authorization: Bearer <AccessToken>
* Content-Type: application/json
* X-Idempotency-Key: <chave-estavel>

Payload esperado:

{
"type": "online",
"external_reference": "<shopflow-order-id-ou-order-number>",
"total_amount": "50.00",
"payer": {
"email": "[cliente@email.com](mailto:cliente@email.com)",
"first_name": "Nome"
},
"transactions": {
"payments": [
{
"amount": "50.00",
"payment_method": {
"id": "pix",
"type": "bank_transfer"
}
}
]
}
}

Regras:

* `type` deve ser `online`.
* `external_reference` deve ser estável e vincular à Order do Shopflow.
* `total_amount` deve ser string decimal com duas casas, usando ponto.
* `transactions.payments[0].amount` deve bater com total_amount.
* `payment_method.id` deve ser `pix`.
* `payment_method.type` deve ser `bank_transfer`.
* Usar `X-Idempotency-Key` estável por Order/PixPayment.
* Não gerar idempotency key aleatória em cada retry.
* Usar HttpClient via IHttpClientFactory.
* Não logar AccessToken.
* Não logar payload com dados sensíveis completos.
* Mapear erros da API para exceções controladas do módulo.

Se a documentação/API permitir `notification_url` no payload de Orders, usar MercadoPago__NotificationUrl. Se não estiver confirmado pelo contrato atual, não enviar esse campo e depender da configuração de Webhook no painel. Documentar a decisão.

==================================================
5. RESPOSTA DA ORDERS API
=========================

A resposta esperada da criação da order Mercado Pago possui:

* id da order Mercado Pago, exemplo:
  ORD01JP84C939T20S0P1DN382FQ6K

* status:
  action_required

* status_detail:
  waiting_transfer

* transactions.payments[0].id, exemplo:
  PAY01JP84C939T20S0P1DN6FCMWQC

* transactions.payments[0].status:
  action_required

* transactions.payments[0].status_detail:
  waiting_transfer

* transactions.payments[0].payment_method.ticket_url

* transactions.payments[0].payment_method.qr_code

* transactions.payments[0].payment_method.qr_code_base64

Mapear para PixPayment interno:

* Provider = MercadoPago
* ProviderOrderId = ORD...
* ProviderPaymentId ou ProviderTransactionId = PAY...
* ProviderStatus = status da order
* ProviderStatusDetail = status_detail da order
* ProviderTransactionStatus = status da transação, se campo existir
* ProviderTransactionStatusDetail = status_detail da transação, se campo existir
* ExternalReference = Shopflow orderId/orderNumber
* CopyPasteCode = qr_code
* QrCodeBase64 = qr_code_base64
* TicketUrl = ticket_url
* ExpiresAt, se a API retornar ou se calculado internamente

Atenção:

* Não confundir ORD com PAY.
* O webhook de Orders deve localizar PixPayment preferencialmente por ProviderOrderId.
* Se o modelo atual só tiver ProviderPaymentId, adicionar ProviderOrderId/ProviderTransactionId com migration.
* Não salvar AccessToken.
* Não salvar dados sensíveis desnecessários.
* Não salvar payload completo com PII, salvo se existir decisão explícita segura.

==================================================
6. CONTRATO DO ENDPOINT SHOPFLOW
================================

Manter endpoint existente:

POST /api/payments/pix/orders/{orderId}

Resposta ao frontend deve continuar compatível com PixPaymentDto atual.

Mapeamento sugerido:

* paymentId = id interno PixPayment
* orderId = id interno Order Shopflow
* status = Pending
* provider = MercadoPago
* amount = total do pedido
* copyPasteCode = transactions.payments[0].payment_method.qr_code
* qrCode = qr_code_base64, se vier preenchido
* qrCodeImageUrl = null, salvo se o contrato atual usar imagem real
* ticketUrl = ticket_url, se puder adicionar campo opcional
* expiresAt = validade do Pix, se disponível/calculada
* message = "Pix gerado. Aguardando pagamento."

Importante:

* `ticket_url` não é imagem de QR Code. Não salvar como `qrCodeImageUrl` se esse campo for usado como `<img src>`.
* Se qr_code_base64 vier vazio no Sandbox, ainda retornar copyPasteCode.
* Frontend deve conseguir exibir copia-e-cola mesmo sem imagem base64.
* Não alterar frontend nesta etapa.

==================================================
7. IDEMPOTÊNCIA NA CRIAÇÃO
==========================

Regras:

* Um Order PendingPayment deve ter no máximo um PixPayment Pending ativo.
* Se já existir PixPayment Pending com ProviderOrderId e QR/copia-e-cola salvo, retornar o existente.
* Se chamada repetir por timeout, usar a mesma X-Idempotency-Key.
* Não criar múltiplas Orders Mercado Pago para o mesmo pedido Shopflow.
* Não criar múltiplos PixPayment Pending para o mesmo pedido.
* Se provider retorna conflito/idempotência, mapear corretamente.

Testes:

* Duas chamadas para POST /payments/pix/orders/{orderId} não criam duas Orders Mercado Pago.
* Segunda chamada retorna PixPayment existente.
* IdempotencyKey é estável.

==================================================
8. WEBHOOK MERCADO PAGO VIA ORDERS
==================================

Criar endpoint:

POST /api/payments/pix/webhooks/mercado-pago

Regras:

* Público, sem autenticação por cookie.
* Excluído do CSRF.
* Segurança vem da assinatura + consulta confirmatória ao Mercado Pago.
* Não exige CORS.
* Não aplicar rate limit agressivo que bloqueie Mercado Pago.
* Evento configurado no painel Mercado Pago: Order.
* Responder 200/201 em tempo adequado.
* Não confiar apenas no body do webhook.
* Sempre consultar GET /v1/orders/{id} antes de marcar Paid.

Payload típico pode conter:
{
"action": "...",
"api_version": "...",
"data": {
"id": "ORD..."
},
"date_created": "...",
"id": "...",
"live_mode": false,
"type": "order",
"user_id": "..."
}

O `data.id` esperado para Orders deve ser o ProviderOrderId.

==================================================
9. VALIDAÇÃO DE ASSINATURA
==========================

Criar serviço:

IMercadoPagoWebhookSignatureValidator
MercadoPagoWebhookSignatureValidator

Headers:

* x-signature
* x-request-id

Regras:

* Secret vem de MercadoPago__WebhookSecret.
* Extrair `ts` e `v1` de x-signature.
* Ler `data.id` preferencialmente de Request.Query["data.id"], conforme documentação de assinatura.
* Se `data.id` também vier no body, conferir que bate com o valor usado para consultar a order.
* Não substituir silenciosamente `data.id` da query por body.data.id no manifesto.
* Montar manifesto exatamente:
  id:<data.id>;request-id:<x-request-id>;ts:<ts>;
* Se data.id for alfanumérico com letras maiúsculas, usar lowercase no manifesto.
* Calcular HMAC-SHA256(secret, manifest) em hexadecimal lowercase.
* Comparar com v1 em tempo constante.
* Se assinatura inválida, retornar 401.
* Se x-signature ausente, 401.
* Se x-request-id ausente, 401.
* Se ts/v1 ausentes, 401.
* Se data.id ausente, retornar erro seguro e documentar. Não aceitar body.data.id como substituto automático para o manifesto sem teste explícito.
* Implementar tolerância de timestamp:
  MercadoPago__WebhookSignatureToleranceMinutes=10
* Não logar secret.
* Não logar assinatura completa.

==================================================
10. CONSULTA DE ORDER NO MERCADO PAGO
=====================================

Criar serviço/client:

IMercadoPagoOrderClient
MercadoPagoOrderClient

Endpoint:

GET /v1/orders/{orderId}

Headers:

* Authorization: Bearer <AccessToken>
* Content-Type: application/json

Regras:

* Usar HttpClient via IHttpClientFactory.
* Timeout razoável.
* Não logar AccessToken.
* Tratar 401/403/404/5xx.
* Mapear response para DTO interno.

Campos importantes:

* id
* status
* status_detail
* external_reference
* total_amount
* transactions.payments[0].id
* transactions.payments[0].amount
* transactions.payments[0].status
* transactions.payments[0].status_detail
* transactions.payments[0].payment_method.id
* transactions.payments[0].payment_method.type
* transactions.payments[0].payment_method.qr_code
* transactions.payments[0].payment_method.qr_code_base64
* transactions.payments[0].payment_method.ticket_url
* last_updated_date
* created_date

==================================================
11. STATUS MAPPING
==================

Mapear Order Mercado Pago para PixPayment/Order internos.

Pending:

* status = created
* status = processing
* status = action_required
* status_detail = waiting_payment
* status_detail = waiting_transfer

Ação:

* PixPayment continua Pending.
* Order continua PendingPayment.
* Não confirmar reserva.

Paid:

* status = processed
* status_detail = accredited

Ação:

* PixPayment → Paid.
* Order → Paid.
* Inventory reservation → Confirmed.

Not paid/final negative:

* status = failed
* status = canceled
* status = expired

Ação:

* PixPayment → Failed/Canceled/Expired conforme enum existente.
* Order não vira Paid.
* Reserva não é confirmada.
* Cancelar reserva somente se a regra do módulo já for clara; se não, deixar para worker/expiração e documentar.

Refund/chargeback:

* refunded
* charged_back

Ação nesta etapa:

* Não implementar fluxo completo.
* Não marcar como Paid novo.
* Se já estava Paid, logar evento/status para tratamento futuro.
* Documentar como dívida.

Importante:

* Confirmar pagamento apenas quando Order status=processed e status_detail=accredited.
* Também conferir transaction payment Pix status=processed e status_detail=accredited, se disponível.
* Conferir payment_method.id = pix.
* Conferir payment_method.type = bank_transfer.
* Conferir total_amount/amount com valor do PixPayment/Order.
* Conferir external_reference com Order Shopflow.

==================================================
12. PROCESSAMENTO DO WEBHOOK
============================

Criar processor/application handler:

ProcessMercadoPagoOrderWebhookCommand
ou
MercadoPagoOrderWebhookProcessor

Fluxo:

1. Receber webhook.
2. Validar assinatura.
3. Extrair ProviderOrderId.
4. Consultar GET /v1/orders/{providerOrderId}.
5. Encontrar PixPayment por ProviderOrderId.
6. Conferir Provider = MercadoPago.
7. Conferir ExternalReference com Order Shopflow.
8. Conferir total_amount.
9. Conferir transaction Pix.
10. Mapear status.

Se approved:

* se PixPayment já Paid, retornar sucesso idempotente.
* se Order já Paid, não duplicar confirmação.
* confirmar reserva pelo serviço correto do Inventory.
* marcar PixPayment Paid.
* marcar Order Paid.
* salvar ProviderStatus/status_detail.
* salvar ProviderTransactionStatus/status_detail.
* salvar datas de atualização/aprovação, se houver.

Ordem recomendada:

* Validar tudo.
* Confirmar reserva via Inventory de forma idempotente.
* Marcar PixPayment Paid.
* Marcar Order Paid.
* Salvar alterações.
* Se a arquitetura exigir outra ordem por transação/módulos, documentar decisão.

Se pending:

* atualizar provider status.
* manter Pending.
* não confirmar reserva.

Se failed/canceled/expired:

* atualizar provider status.
* não confirmar reserva.
* marcar PixPayment conforme enum se seguro.
* não marcar Order Paid.

Se ProviderOrderId desconhecido:

* responder 200/202 seguro após assinatura válida.
* logar warning.
* não lançar erro que gere retry infinito, salvo se decisão explícita.

==================================================
13. INVENTORY CONFIRM RESERVATION
=================================

Usar serviço existente:

IInventoryReservationService.ConfirmReservationAsync

ou command/service equivalente.

Regras:

* Não manipular estoque diretamente via SQL.
* Confirmação deve ser idempotente.
* Se reserva já confirmada, tratar como sucesso.
* Se reserva expirada/cancelada e Mercado Pago approved chegou depois:

  * não recriar estoque;
  * não marcar pedido como Paid automaticamente sem estratégia;
  * logar erro operacional;
  * documentar limitação.
* Worker não deve expirar pedido já pago.

==================================================
14. ORDER E PIXPAYMENT DOMAIN
=============================

Verificar métodos existentes.

Se necessário criar:

PixPayment.MarkAsPaid(...)
PixPayment.MarkAsFailed(...)
PixPayment.MarkAsCanceled(...)
PixPayment.MarkAsExpired(...)
PixPayment.UpdateProviderStatus(...)

Order.MarkAsPaid(...)
Order.MarkAsCanceled(...)
Order.MarkAsExpired(...)

Regras:

* Apenas Pending/PendingPayment pode virar Paid.
* Paid é idempotente.
* Expired/Canceled não deve virar Paid automaticamente.
* Criar migrations se adicionar campos:

  * ProviderOrderId
  * ProviderTransactionId
  * ProviderStatus
  * ProviderStatusDetail
  * ProviderTransactionStatus
  * ProviderTransactionStatusDetail
  * TicketUrl
  * PaidAt
  * ProviderUpdatedAt
  * IdempotencyKey

==================================================
15. WEBHOOK EVENTS / AUDITORIA
==============================

Criar tabela simples se fizer sentido:

MercadoPagoWebhookEvent
ou
PixPaymentWebhookEvent

Campos:

* Id
* Provider = MercadoPago
* ProviderEventId, se existir
* ProviderOrderId
* RequestId
* Action
* Type
* LiveMode
* ReceivedAt
* ProcessedAt
* ProcessingStatus
* ErrorMessage limitado
* SignatureValid
* RawPayload opcional, sem dados sensíveis excessivos

Regras:

* Não processar duas vezes o mesmo evento se ProviderEventId existir.
* Mesmo com tabela de eventos, idempotência real deve vir do estado PixPayment/Order/Inventory.
* Não depender só da tabela de eventos.

Se decidir não criar tabela nesta etapa, justificar e garantir logs + idempotência por estado.

==================================================
16. CSRF / AUTH / RATE LIMIT
============================

Webhook:

* Não exige cookie.
* Não exige CSRF.
* Não exige Backoffice.
* Segurança = assinatura + GET /v1/orders/{id}.
* Excluir endpoint do CSRF middleware.
* Não aplicar rate limit agressivo.
* CORS não é relevante.

==================================================
17. TESTES OBRIGATÓRIOS
=======================

Criar testes unitários para:

1. Provider usa POST /v1/orders, não /v1/payments.
2. Provider envia Authorization Bearer.
3. Provider envia X-Idempotency-Key estável.
4. Payload usa type=online.
5. Payload usa transactions.payments[0].payment_method.id=pix.
6. Payload usa payment_method.type=bank_transfer.
7. Payload usa total_amount e amount com valor correto.
8. Resposta action_required/waiting_transfer mapeia PixPayment Pending.
9. qr_code mapeia copyPasteCode.
10. qr_code_base64 mapeia qrCode quando vier preenchido.
11. ticket_url é salvo em campo correto, não como imagem.
12. Provider salva ProviderOrderId ORD e ProviderTransactionId PAY.
13. Segunda chamada retorna PixPayment existente.

Signature validator:
14. Assinatura válida passa.
15. v1 inválido falha.
16. x-signature ausente falha.
17. x-request-id ausente falha.
18. manifesto usa data.id da query.
19. body.data.id não substitui query data.id silenciosamente.
20. data.id alfanumérico uppercase vira lowercase no manifesto.
21. timestamp antigo falha, se tolerância implementada.

Order client:
22. GET /v1/orders/{id} com Authorization.
23. 401/403/404 tratados.

Webhook/application:
24. Webhook assinatura inválida retorna 401.
25. Webhook válido + Order processed/accredited:
- PixPayment Paid.
- Order Paid.
- Reserva confirmada.
26. Webhook duplicado approved é idempotente.
27. Webhook action_required/waiting_transfer mantém Pending.
28. Webhook failed/canceled/expired não marca Paid.
29. Valor divergente não marca Paid.
30. external_reference divergente não marca Paid.
31. payment_method diferente de pix não marca Paid.
32. Webhook não exige CSRF.
33. Webhook não exige admin/customer cookie.
34. Worker não expira pedido pago.

Não chamar API real nos testes automatizados.
Mockar HttpClient/HttpMessageHandler.

Executar:
dotnet build
dotnet test

==================================================
18. VALIDAÇÃO MANUAL EM HML/SANDBOX
===================================

Documentar passos:

1. Configurar no painel Mercado Pago:
   Evento: Order
   URL:
   https://api-hml.seudominio.com.br/api/payments/pix/webhooks/mercado-pago

2. Copiar WebhookSecret para:
   MercadoPago__WebhookSecret

3. Habilitar:
   MercadoPago__Enabled=true
   MercadoPago__Environment=Sandbox
   MercadoPago__AccessToken=<teste>

4. Criar pedido pelo frontend HML.

5. Gerar Pix real via:
   POST /api/payments/pix/orders/{orderId}

6. Verificar resposta:

   * provider MercadoPago
   * ProviderOrderId ORD salvo internamente
   * copyPasteCode preenchido
   * ticketUrl preenchido, se retornado
   * PixPayment Pending
   * Order PendingPayment

7. Para teste de aprovação automática Sandbox:

   * usar `MercadoPago__SandboxPayerFirstNameOverride=APRO`, se necessário;
   * garantir que isso nunca fique ativo em Production.

8. Aguardar webhook ou consultar logs.

9. Validar:

   * PixPayment Paid quando Mercado Pago retornar processed/accredited.
   * Order Paid.
   * reserva confirmada.
   * worker não expira pedido pago.

==================================================
19. DOCUMENTAÇÃO
================

Criar:

* docs/payments/MP-PIX-002-orders-provider-and-webhook.md

Atualizar:

* docs/payments-pix.md
* docs/orders.md
* docs/inventory.md, se existir
* docs/expiration-worker.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* deploy/.env.test.example
* deploy/.env.hml.example
* docker-compose.yml, se necessário
* docs/testing.md, se existir

Documentar:

* que Pix Mercado Pago usa Orders API.
* que não usamos /v1/payments.
* POST /v1/orders.
* GET /v1/orders/{id}.
* evento Webhook Order.
* mapeamento ORD/PAY.
* mapeamento action_required/waiting_transfer.
* mapeamento processed/accredited.
* assinatura x-signature.
* data.id da query no manifesto.
* idempotência.
* como QR/copia-e-cola são retornados.
* limitação de teste APRO.
* que frontend não foi alterado.
* rollback.
* variáveis de ambiente.

==================================================
20. BUILD E TESTES
==================

Executar:

dotnet build
dotnet test

Se Docker/env examples forem alterados:

docker compose build api worker

Não pular testes.
Não remover testes existentes.
Se quebrar teste antigo, corrigir causa real.

==================================================
21. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Se havia implementação anterior com /v1/payments e como foi corrigida.
3. Provider Orders implementado.
4. Endpoint webhook criado.
5. Campos ProviderOrderId/ProviderTransactionId salvos.
6. Como POST /v1/orders é chamado.
7. Como GET /v1/orders/{id} é chamado.
8. Como QR/copia-e-cola/ticketUrl são mapeados.
9. Como assinatura é validada.
10. Como status action_required/waiting_transfer é tratado.
11. Como status processed/accredited é tratado.
12. Como PixPayment vira Paid.
13. Como Order vira Paid.
14. Como reserva é confirmada.
15. Como idempotência foi garantida.
16. Migrations criadas, se houver.
17. Configurações/envs adicionadas.
18. Testes criados.
19. Resultado build/test.
20. Docs atualizadas.
21. Limitações conhecidas.
22. Próximo passo recomendado.

Critérios de aceite:

* Fluxo Pix Mercado Pago usa /v1/orders.
* Nenhum fluxo principal de Pix usa /v1/payments.
* POST /api/payments/pix/orders/{orderId} cria Order Mercado Pago.
* Resposta retorna copyPasteCode real.
* ProviderOrderId ORD é salvo.
* ProviderTransactionId PAY é salvo.
* Webhook usa evento Order.
* Webhook valida x-signature.
* Webhook consulta GET /v1/orders/{id}.
* Só processed/accredited marca PixPayment Paid.
* Só processed/accredited marca Order Paid.
* Só processed/accredited confirma reserva.
* action_required/waiting_transfer mantém Pending.
* failed/canceled/expired não viram Paid.
* Webhook é idempotente.
* Webhook não exige CSRF.
* Webhook não exige cookie.
* AccessToken e WebhookSecret não são expostos.
* dotnet build passa.
* dotnet test passa.
* Docs refletem o estado real.
