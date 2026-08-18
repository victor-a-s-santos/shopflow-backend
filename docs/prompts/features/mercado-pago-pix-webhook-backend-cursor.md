Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, segurança de pagamentos, Pix, webhooks e Mercado Pago Checkout Transparente.

Objetivo:
Implementar a segunda etapa da integração Mercado Pago Pix no módulo PaymentsPix: receber webhook assinado do Mercado Pago, validar origem, consultar o pagamento real, atualizar PixPayment, atualizar Order e confirmar reserva de estoque.

Contexto:
O Shopflow já possui:

* Catalog real.
* Inventory real.
* CheckoutSession real.
* Orders real.
* PaymentsPix com IPixPaymentProvider.
* MercadoPagoPixPaymentProvider deve estar implementado na etapa anterior.
* POST /api/payments/pix/orders/{orderId} cria Pix real via Mercado Pago.
* PixPayment nasce Pending.
* Order nasce PendingPayment.
* Inventory mantém reserva de estoque.
* Worker de expiração existe.
* Admin Auth real.
* Customer Auth real.
* HML/teste com domínio HTTPS.
* Mercado Pago app criada com credenciais de teste.
* WebhookSecret configurável via env.
* MercadoPago__NotificationUrl apontando para:
  https://api-hml.seudominio.com.br/api/payments/pix/webhooks/mercado-pago

Documentação Mercado Pago relevante:

* Webhook envia headers:

  * x-signature
  * x-request-id
* Webhook inclui data.id como identificador do recurso.
* x-signature contém valores no formato:
  ts=...,v1=...
* Validação:

  * extrair ts e v1 de x-signature;
  * montar manifesto:
    id:<data.id>;request-id:<x-request-id>;ts:<ts>;
  * calcular HMAC-SHA256(secret, manifest) em hexadecimal;
  * comparar em tempo constante com v1;
  * se inválido, retornar 401.
* Mercado Pago espera HTTP 200 ou 201 em até 22 segundos.
* Após receber notificação, consultar o pagamento completo via:
  GET https://api.mercadopago.com/v1/payments/{id}
  Authorization: Bearer <AccessToken>

Importante:

* Não confiar apenas no payload do webhook.
* Sempre consultar o pagamento no Mercado Pago antes de marcar como Paid.
* Não expor Access Token.
* Não logar secrets.
* Não logar token de assinatura completo.
* Não implementar frontend.
* Não implementar cartão.
* Não implementar reembolso.
* Não implementar Checkout Pro.
* Não criar endpoint simulate-paid.
* Não marcar Paid sem confirmação do Mercado Pago.
* Não confirmar estoque se pagamento não estiver approved.
* Não quebrar Fake provider em Development/Test se MercadoPago estiver disabled.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Antes de implementar, leia:

* docs/prompts/00-project-context.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/payments-pix.md
* docs/payments/MP-PIX-001-provider-generation.md, se existir
* docs/orders.md
* docs/cart-checkout.md
* docs/expiration-worker.md
* módulo PaymentsPix completo
* MercadoPagoPixPaymentProvider
* IPixPaymentProvider
* PixPayment aggregate
* PaymentsPixDbContext/migrations
* Orders module
* Inventory reservation services
* Worker de expiração
* middleware CSRF atual
* Program.cs
* appsettings atuais
* docker-compose/deploy env examples
* testes PaymentsPix atuais
* testes Orders/Inventory relacionados

Seguir a arquitetura existente.
Não criar arquitetura paralela.

==================================================
2. ESCOPO DA FEATURE
====================

Implementar:

1. Endpoint de webhook:
   POST /api/payments/pix/webhooks/mercado-pago

2. Validador de assinatura Mercado Pago.

3. Client/serviço para consultar pagamento:
   GET /v1/payments/{id}

4. Processador idempotente de webhook.

5. Atualização do PixPayment conforme status real do Mercado Pago.

6. Quando pagamento estiver aprovado:

   * PixPayment → Paid
   * Order → Paid
   * Inventory reservation → Confirmed

7. Quando pagamento estiver rejeitado/cancelado/expirado, mapear com segurança:

   * PixPayment → Failed/Canceled/Expired, conforme enum existente
   * Order não deve virar Paid
   * reserva não deve ser confirmada

8. Logs seguros.

9. Testes unitários e integração.

10. Documentação.

==================================================
3. FORA DO ESCOPO
=================

Não implementar agora:

* Frontend.
* Simulador manual de pagamento.
* Cartão.
* Boleto.
* Reembolso.
* Estorno.
* Disputa/chargeback completo.
* Mercado Pago produção definitiva.
* Customer Order real.
* Guest Order Access Token.
* Notificação por e-mail.
* Admin Orders UI.
* Shipping.
* Alterações no checkout frontend.

==================================================
4. ENDPOINT WEBHOOK
===================

Criar endpoint:

POST /api/payments/pix/webhooks/mercado-pago

Regras:

* Público, sem autenticação por cookie.
* Excluído do CSRF.
* Deve validar assinatura Mercado Pago.
* Se assinatura inválida, retornar 401.
* Se payload inválido ou sem data.id, retornar 400 ou 202/200 seguro conforme decisão documentada.
* Se assinatura válida, processar de forma idempotente.
* Responder 200/201 rapidamente.
* Se processamento for síncrono, garantir que normalmente finalize em menos de 22s.
* Se optar por fila/tabela para processamento assíncrono, documentar e implementar de forma simples.

Payload típico esperado:
{
"action": "payment.updated",
"api_version": "v1",
"data": {
"id": "123456789"
},
"date_created": "...",
"id": 123,
"live_mode": false,
"type": "payment",
"user_id": "..."
}

Também considerar `data.id` vindo na query string, pois a validação oficial considera o valor `data.id` como parte do manifesto.

Implementar extração robusta de:

* data.id do query string `data.id`;
* fallback do body `data.id`, se necessário;
* x-signature;
* x-request-id.

Documentar a decisão.

==================================================
5. VALIDAÇÃO DE ASSINATURA
==========================

Criar serviço:

IMercadoPagoWebhookSignatureValidator
MercadoPagoWebhookSignatureValidator

Entrada:

* xSignature
* xRequestId
* dataId
* secret

Regras:

* Secret vem de MercadoPago__WebhookSecret.
* Se MercadoPago__Enabled=true e WebhookSecret ausente em HML/Prod, falhar de forma clara no startup ou no endpoint com erro seguro.
* Extrair `ts` e `v1` de x-signature.
* Montar manifesto exatamente:
  id:<data.id>;request-id:<x-request-id>;ts:<ts>;
* Calcular HMAC-SHA256(secret, manifest) em hexadecimal lowercase.
* Comparar com v1 em tempo constante.
* Se inválido, retornar false/lançar exceção controlada.
* Não logar secret.
* Não logar assinatura completa.
* Não usar comparação simples vulnerável a timing attack.

Adicionar tolerância de timestamp:

* Rejeitar assinatura com `ts` muito antigo, se possível.
* Sugestão: 5 a 10 minutos.
* Se implementar tolerância, documentar e testar.
* Se não implementar agora, registrar como dívida técnica.

==================================================
6. CONSULTA DO PAGAMENTO NO MERCADO PAGO
========================================

Criar serviço/client:

IMercadoPagoPaymentClient
MercadoPagoPaymentClient

Endpoint:
GET /v1/payments/{paymentId}

Headers:
Authorization: Bearer <AccessToken>
Content-Type: application/json

Regras:

* Usar HttpClient via IHttpClientFactory.
* Timeout razoável.
* Não logar AccessToken.
* Tratar 401/403/404/5xx.
* Mapear response para DTO interno.

Campos importantes da resposta:

* id
* status
* status_detail
* external_reference
* transaction_amount
* payment_method_id
* payment_type_id
* date_approved
* date_last_updated
* point_of_interaction.transaction_data.qr_code, se vier
* payer.email, se necessário para conferência

Status relevantes:

* approved → pagamento aprovado.
* pending → manter Pending.
* in_process → manter Pending/InProcess, se enum existir.
* rejected → Failed.
* cancelled → Canceled.
* refunded/charged_back → não tratar profundamente agora, mas não marcar Paid automaticamente.
* unknown/outros → logar e não confirmar venda.

==================================================
7. PROCESSAMENTO DO WEBHOOK
===========================

Criar serviço/application handler:

ProcessMercadoPagoPixWebhookCommand
ou
MercadoPagoWebhookProcessor

Entrada:

* providerPaymentId/dataId
* notificationId, se disponível
* requestId
* liveMode
* action/type
* receivedAt

Fluxo:

1. Validar assinatura.
2. Consultar GET /v1/payments/{providerPaymentId}.
3. Encontrar PixPayment por ProviderPaymentId.
4. Conferir Provider = MercadoPago.
5. Conferir ExternalReference/OrderId se disponível.
6. Conferir valor do pagamento contra Amount do PixPayment/Order.
7. Conferir payment_method_id = pix.
8. Conferir status.

Se status Mercado Pago = approved:

* se PixPayment já Paid, retornar sucesso idempotente.
* se Order já Paid, não duplicar confirmação.
* PixPayment.MarkAsPaid(...)
* Order.MarkAsPaid(...)
* Inventory ConfirmReservation usando fluxo correto existente.
* salvar datas:

  * PaidAt
  * ProviderApprovedAt/date_approved
  * ProviderStatus/status_detail

Se status Mercado Pago = pending/in_process:

* atualizar ProviderStatus/status_detail.
* manter PixPayment Pending.
* manter Order PendingPayment.
* não confirmar reserva.

Se status Mercado Pago = rejected/cancelled:

* marcar PixPayment Failed/Canceled se enum existir.
* se não existir, adicionar com cuidado.
* Order pode permanecer PendingPayment ou virar Canceled conforme regra existente. Decisão recomendada:

  * rejected/cancelled → Order Canceled se não houver outro pagamento ativo.
* cancelar reserva apenas se for decisão segura e coerente com worker.
* Documentar decisão.

Se status Mercado Pago = expired:

* PixPayment Expired.
* Order Expired/Canceled.
* cancelar reserva se ainda pendente.

Importante:

* Não confirmar reserva antes de salvar/garantir estado Paid.
* Evitar transação distribuída complexa entre módulos.
* Garantir idempotência.
* Se confirmar reserva falhar depois de marcar Paid, próxima execução deve conseguir recuperar ou deve haver log/dívida clara.
* Preferir orquestração robusta e idempotente.

==================================================
8. INVENTORY CONFIRM RESERVATION
================================

Usar serviço existente:

IInventoryReservationService.ConfirmReservationAsync

ou command/service equivalente já implementado.

Regras:

* Não manipular estoque diretamente por SQL se existe serviço de domínio.
* Não duplicar confirmação.
* Se reserva já confirmada, tratar como idempotente.
* Se reserva expirada/cancelada, logar erro crítico e não marcar Order Paid sem estratégia.
* Se o worker expirou a reserva antes do webhook approved, tratar com cuidado:

  * não recriar estoque;
  * logar caso inconsistente;
  * documentar limitação.

Decisão esperada:

* Pagamento approved antes da expiração confirma reserva.
* Se pagamento chegar depois da expiração, não confirmar automaticamente sem análise; registrar falha operacional.

==================================================
9. ORDER PAID
=============

Verificar aggregate Order atual.

Se já existe método para Paid, usar.
Se não existe, criar:

Order.MarkAsPaid(paymentId, paidAt)

Regras:

* Apenas PendingPayment pode virar Paid.
* Paid é idempotente.
* Canceled/Expired não deve virar Paid automaticamente sem decisão explícita.
* Registrar paidAt, se campo existir ou for adicionado.
* Criar migration se necessário.

==================================================
10. PIXPAYMENT PAID
===================

Verificar aggregate PixPayment atual.

Se já existe método para Paid, usar.
Se não existe, criar:

PixPayment.MarkAsPaid(providerStatus, statusDetail, approvedAt)

Regras:

* Pending pode virar Paid.
* Paid é idempotente.
* Expired/Canceled/Failed não deve virar Paid automaticamente sem decisão documentada.
* Salvar ProviderStatus.
* Salvar ProviderStatusDetail.
* Salvar PaidAt/ApprovedAt.

Criar migration se necessário.

==================================================
11. IDEMPOTÊNCIA / EVENTOS
==========================

Webhooks podem chegar mais de uma vez.

Criar tabela de eventos se fizer sentido:

MercadoPagoWebhookEvent
ou
PixPaymentWebhookEvent

Campos sugeridos:

* Id
* Provider
* ProviderEventId
* ProviderPaymentId
* RequestId
* Action
* Type
* LiveMode
* ReceivedAt
* ProcessedAt
* ProcessingStatus
* ErrorMessage limitado
* SignatureValid bool
* RawPayload opcional, sem dados sensíveis excessivos

Regras:

* Não processar duas vezes o mesmo evento se ProviderEventId existir.
* Mesmo sem ProviderEventId confiável, idempotência por ProviderPaymentId + status.
* Não depender apenas da tabela de eventos para idempotência.
* Estados de PixPayment/Order/Reservation devem ser idempotentes.

Se criar tabela for excesso para esta etapa, documentar e garantir idempotência por estado. Minha recomendação: criar tabela simples de eventos para auditoria e troubleshooting.

==================================================
12. CSRF / AUTH / RATE LIMIT
============================

Webhook:

* Não exige cookie.
* Não exige CSRF.
* Não exige Backoffice.
* Segurança vem da assinatura + consulta confirmatória ao Mercado Pago.

Rate limit:

* Não aplicar rate limit agressivo que possa bloquear Mercado Pago.
* Se houver rate limit global, excluir webhook ou criar política adequada.

CORS:

* Webhook não depende de CORS.

==================================================
13. TESTES OBRIGATÓRIOS
=======================

Criar testes unitários para:

1. Signature validator aceita assinatura válida.
2. Signature validator rejeita v1 inválido.
3. Signature validator rejeita header ausente.
4. Signature validator monta manifesto correto.
5. Comparação é feita em tempo constante, se testável.
6. Payment client chama GET /v1/payments/{id} com Authorization.
7. Payment approved mapeia status interno correto.
8. Payment pending não marca Paid.
9. Payment rejected não confirma estoque.

Criar testes de integração/application para:

10. Webhook com assinatura inválida retorna 401.
11. Webhook com assinatura válida e pagamento approved:

    * PixPayment vira Paid.
    * Order vira Paid.
    * Reserva é confirmada.
12. Webhook approved duplicado é idempotente.
13. Webhook pending mantém Order PendingPayment.
14. Webhook de payment id desconhecido não quebra e retorna resposta segura.
15. Webhook com valor divergente não marca Paid.
16. Webhook com external_reference divergente não marca Paid.
17. Webhook com payment_method_id diferente de pix não marca Paid.
18. Webhook não exige CSRF.
19. Webhook não exige admin/customer cookie.
20. Worker não reverte Paid depois.

Mockar Mercado Pago via HttpMessageHandler/fake client.
Não chamar API real nos testes automatizados.

Executar:

dotnet build
dotnet test

==================================================
14. DOCUMENTAÇÃO
================

Criar:

* docs/payments/MP-PIX-002-webhook-confirmation.md

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

* endpoint webhook.
* URL para cadastrar no Mercado Pago.
* headers necessários.
* validação x-signature.
* consulta GET /v1/payments/{id}.
* status mapping.
* idempotência.
* quando Order vira Paid.
* quando Inventory confirma reserva.
* o que acontece com Pending/Rejeitado/Expirado.
* como testar em Sandbox.
* como usar logs.
* limitações.
* rollback.
* que frontend não foi alterado.

==================================================
15. CONFIGURAÇÃO / ENV
======================

Revisar/adicionar envs:

MercadoPago__Enabled=true
MercadoPago__Environment=Sandbox
MercadoPago__BaseUrl=https://api.mercadopago.com
MercadoPago__AccessToken=
MercadoPago__PublicKey=
MercadoPago__WebhookSecret=
MercadoPago__NotificationUrl=https://api-hml.seudominio.com.br/api/payments/pix/webhooks/mercado-pago
MercadoPago__WebhookSignatureToleranceMinutes=10

Regras:

* WebhookSecret obrigatório quando MercadoPago Enabled em HML/Prod.
* AccessToken obrigatório quando MercadoPago Enabled.
* Não commitar secrets reais.
* Atualizar .env.example com placeholders.

==================================================
16. BUILD E TESTES
==================

Executar:

dotnet build
dotnet test

Se Docker for alterado:

docker compose build api worker
docker compose up -d api worker

Não pular testes.
Não remover testes existentes.
Se quebrar teste antigo, corrigir causa real.

==================================================
17. VALIDAÇÃO MANUAL EM HML/SANDBOX
===================================

Documentar passos:

1. Configurar no painel Mercado Pago:
   URL webhook:
   https://api-hml.seudominio.com.br/api/payments/pix/webhooks/mercado-pago

2. Evento:
   payments / payment.updated

3. Secret:
   copiar para MercadoPago__WebhookSecret

4. Criar pedido no frontend HML.

5. Gerar Pix real.

6. Pagar com fluxo de teste/sandbox conforme Mercado Pago.

7. Verificar:

   * PixPayment Paid
   * Order Paid
   * estoque reservado confirmado
   * worker não expira pedido pago
   * logs sem erro

==================================================
18. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Endpoint webhook criado.
3. Como assinatura é validada.
4. Como GET /v1/payments/{id} é chamado.
5. Como status Mercado Pago é mapeado.
6. Como PixPayment vira Paid.
7. Como Order vira Paid.
8. Como reserva é confirmada.
9. Como idempotência foi garantida.
10. Se tabela de eventos foi criada.
11. Migrations criadas, se houver.
12. Configurações/envs adicionadas.
13. Testes criados.
14. Resultado build/test.
15. Docs atualizadas.
16. Limitações conhecidas.
17. Próximo passo recomendado.

Critérios de aceite:

* Webhook existe em /api/payments/pix/webhooks/mercado-pago.
* Assinatura inválida retorna 401.
* Assinatura válida consulta pagamento real no Mercado Pago.
* Somente status approved marca PixPayment Paid.
* Somente status approved marca Order Paid.
* Somente status approved confirma reserva.
* Pending não confirma estoque.
* Rejected/cancelled/expired não viram Paid.
* Webhook é idempotente.
* Webhook não exige CSRF.
* Webhook não exige cookie admin/customer.
* AccessToken e WebhookSecret não são expostos.
* dotnet build passa.
* dotnet test passa.
* Docs refletem estado real.
