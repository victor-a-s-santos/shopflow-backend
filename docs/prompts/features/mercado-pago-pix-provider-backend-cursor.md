Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, segurança de pagamentos, Pix e integração Mercado Pago Checkout Transparente.

Objetivo:
Implementar a primeira etapa da integração Pix real com Mercado Pago no módulo PaymentsPix do Shopflow.

Nesta etapa, queremos apenas:

* substituir o FakePixPaymentProvider por um MercadoPagoPixPaymentProvider configurável;
* criar pagamento Pix real no Mercado Pago via Checkout Transparente;
* salvar dados do provider;
* retornar QR Code / Pix Copia e Cola para o frontend;
* manter Order como PendingPayment;
* manter PixPayment como Pending;
* NÃO confirmar pagamento ainda;
* NÃO confirmar reserva de estoque ainda.

Contexto do projeto:
O Shopflow já possui:

* Catalog real.
* Inventory real.
* CheckoutSession real.
* Orders real, com Order PendingPayment.
* PaymentsPix MVP com IPixPaymentProvider e FakePixPaymentProvider.
* Frontend já chama POST /api/payments/pix/orders/{orderId}.
* Frontend já está preparado para exibir QR/copia-e-cola quando backend retornar valores não-null.
* Worker de expiração já existe.
* Admin Auth real.
* Customer Auth real.
* HML/teste com domínio e HTTPS.
* Mercado Pago criado com credenciais de teste.

Documentação Mercado Pago relevante:

* Checkout Transparente mantém pagamento dentro da loja.
* Pix pode ser criado via POST https://api.mercadopago.com/v1/payments.
* Usar Authorization: Bearer <Access Token>.
* Usar X-Idempotency-Key para evitar pagamentos duplicados.
* Payload deve usar payment_method_id = "pix".
* Resposta retorna status pending e dados de QR em point_of_interaction.transaction_data:

  * qr_code_base64
  * qr_code
  * ticket_url
* date_of_expiration pode definir validade do Pix.

Importante:

* Não colocar Access Token no código.
* Não commitar credenciais.
* Não expor Access Token no frontend.
* Não usar Public Key no backend como segredo.
* Não usar JWT/localStorage.
* Não implementar webhook nesta etapa.
* Não marcar PixPayment como Paid nesta etapa.
* Não marcar Order como Paid nesta etapa.
* Não confirmar reserva de estoque nesta etapa.
* Não implementar estorno/reembolso.
* Não implementar cartão.
* Não implementar Checkout Pro.
* Não redirecionar cliente para Mercado Pago como fluxo principal.
* Não mexer no frontend nesta etapa.
* Não quebrar Fake provider em Development/Test se MercadoPago estiver desabilitado.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

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
* PixPayment aggregate
* PaymentsPixDbContext/migrations
* endpoint POST /api/payments/pix/orders/{orderId}
* testes PaymentsPix atuais
* appsettings atuais
* docker-compose/deploy env examples

Seguir a arquitetura existente.
Não criar arquitetura paralela de pagamentos.

==================================================
2. CONFIGURAÇÃO
===============

Criar configuração:

MercadoPago:
Enabled: false
Environment: Sandbox
BaseUrl: https://api.mercadopago.com
AccessToken: ""
PublicKey: ""
WebhookSecret: ""
NotificationUrl: ""
PixExpirationMinutes: 30
TestPayerCpf: ""
TestPayerFirstName: "Test"
TestPayerLastName: "User"

Variáveis de ambiente esperadas:

MercadoPago__Enabled=true
MercadoPago__Environment=Sandbox
MercadoPago__BaseUrl=https://api.mercadopago.com
MercadoPago__AccessToken=APP_USR-...
MercadoPago__PublicKey=...
MercadoPago__WebhookSecret=...
MercadoPago__NotificationUrl=https://api-hml.seudominio.com.br/api/payments/pix/webhooks/mercado-pago
MercadoPago__PixExpirationMinutes=30
MercadoPago__TestPayerCpf=19119119100

Regras:

* AccessToken é obrigatório quando MercadoPago__Enabled=true.
* Em Production/HML com Enabled=true, falhar de forma clara se AccessToken estiver ausente.
* Em Development/Test com Enabled=false, continuar usando FakePixPaymentProvider.
* PublicKey pode ser documentada, mas não deve ser usada como segredo.
* WebhookSecret será usado na próxima etapa; apenas configurar/documentar agora.
* NotificationUrl pode ser enviado na criação do pagamento se o Mercado Pago aceitar notification_url no payload usado. Se implementar, usar somente quando configurado.
* Não commitar .env real.
* Atualizar deploy/.env.*.example sem secrets reais.

==================================================
3. PROVIDER MERCADO PAGO
========================

Criar:

MercadoPagoPixPaymentProvider

Implementar a interface atual:

IPixPaymentProvider

Regras:

* Usar HttpClient via IHttpClientFactory.
* Não usar HttpClient estático manual.
* Configurar BaseUrl.
* Enviar Authorization: Bearer <AccessToken>.
* Enviar X-Idempotency-Key por pagamento.
* Criar idempotency key estável com base no PixPaymentId ou OrderId.
* Não gerar uma idempotency key aleatória a cada retry do mesmo pagamento.
* Timeout razoável.
* Logs seguros sem token.
* Não logar payload com dados sensíveis completos.
* Mapear erros do Mercado Pago para exceções controladas do módulo.

Endpoint Mercado Pago:
POST /v1/payments

Payload mínimo esperado:

{
"transaction_amount": 100.00,
"description": "Pedido Shopflow <orderNumber>",
"payment_method_id": "pix",
"date_of_expiration": "...",
"payer": {
"email": "[cliente@email.com](mailto:cliente@email.com)",
"first_name": "...",
"last_name": "...",
"identification": {
"type": "CPF",
"number": "19119119100"
}
},
"external_reference": "<orderId ou orderNumber>",
"notification_url": "https://api-hml.../api/payments/pix/webhooks/mercado-pago"
}

Ajustar conforme contrato real recomendado pela documentação/API atual.

CPF/documento:

* Se o Order/Checkout ainda não possui CPF do cliente, usar CPF de teste apenas em Sandbox/HML via MercadoPago__TestPayerCpf.
* Em Production com MercadoPago Enabled, não usar CPF fake.
* Documentar que CPF/documento real deve ser coletado no checkout antes de produção.
* Se ambiente Production e não houver documento real, falhar ou deixar MercadoPago disabled, conforme decisão segura.

==================================================
4. DADOS A SALVAR NO PIXPAYMENT
===============================

Verificar PixPayment atual.

Se já existem campos suficientes, reutilizar.
Se não existirem, adicionar campos/migration para:

* Provider
* ProviderPaymentId
* ProviderStatus
* ProviderStatusDetail
* QrCode
* QrCodeBase64
* TicketUrl
* ExternalReference
* IdempotencyKey
* ExpiresAt
* RawProviderResponse, opcional e com cuidado
* UpdatedAt, se padrão existir

Regras:

* Não salvar Access Token.
* Não salvar dados sensíveis desnecessários.
* Não salvar payload completo com PII se não necessário.
* QrCode e QrCodeBase64 podem ser salvos para reexibição.
* ProviderPaymentId é obrigatório quando Mercado Pago criar pagamento.
* Status interno continua Pending.
* ProviderStatus deve refletir status Mercado Pago, ex.: pending.
* ProviderStatusDetail pode salvar pending_waiting_transfer.
* Provider = MercadoPago.

Se criar migration, atualizar tests.

==================================================
5. RESPOSTA DO ENDPOINT EXISTENTE
=================================

Manter endpoint:

POST /api/payments/pix/orders/{orderId}

Hoje ele retorna PixPaymentDto com campos como:

* paymentId
* orderId
* status
* provider
* amount
* qrCode
* qrCodeImageUrl
* copyPasteCode
* expiresAt
* createdAt
* message

Após Mercado Pago, retornar:

* status = Pending
* provider = MercadoPago
* amount = total do pedido
* qrCode = qr_code_base64 ou URL da imagem, conforme contrato atual
* copyPasteCode = qr_code
* qrCodeImageUrl = null se usar base64 direto, ou adaptar sem quebrar frontend
* expiresAt = date_of_expiration
* message = "Pix gerado. Aguardando pagamento."

Atenção:

* Frontend já espera QR/copia-e-cola quando valores não-null.
* Não alterar o contrato sem necessidade.
* Se mudar nomenclatura, atualizar docs e testes.

==================================================
6. IDEMPOTÊNCIA
===============

Regras:

* Um Order PendingPayment deve ter no máximo um PixPayment Pending ativo.
* Se já existir PixPayment Pending com provider MercadoPago e QR salvo, retornar o existente sem criar novo pagamento.
* Se chamada repetir por timeout, usar a mesma X-Idempotency-Key.
* Não criar múltiplos pagamentos no Mercado Pago para o mesmo pedido.
* Se provider retorna conflito/idempotência, mapear corretamente.

Testes obrigatórios:

* Duas chamadas para POST /payments/pix/orders/{orderId} não criam dois PixPayments.
* Duas chamadas não chamam Mercado Pago duas vezes, se PixPayment já existe completo.
* IdempotencyKey é estável.

==================================================
7. ERROS
========

Tratar:

* AccessToken ausente com Enabled=true.
* Mercado Pago 400/401/403.
* Timeout.
* Resposta sem qr_code.
* Resposta sem id.
* Order não encontrada.
* Order não PendingPayment.
* Order expirada/cancelada.
* PixPayment já Paid, no futuro.
* Valor inválido.

Mensagens:

* Não expor detalhes sensíveis para frontend.
* Logar detalhes técnicos seguros no backend.
* Retornar erro controlado.

==================================================
8. WEBHOOK — NÃO IMPLEMENTAR AINDA
==================================

Não implementar confirmação de pagamento nesta etapa.

Apenas preparar documentação/config para próxima fase.

Não criar:

* endpoint webhook
* validação x-signature
* consulta GET /v1/payments/{id}
* marcação Paid
* confirmação de reserva

Mas documentar próxima etapa:

* POST /api/payments/pix/webhooks/mercado-pago
* validar x-signature, x-request-id e data.id
* consultar pagamento no Mercado Pago
* atualizar PixPayment Paid
* atualizar Order Paid
* confirmar reserva no Inventory
* idempotência

==================================================
9. TESTES
=========

Criar testes unitários/integrados usando fake HttpMessageHandler ou mock HttpClient.

Testes:

1. Provider envia POST /v1/payments.
2. Provider envia Authorization Bearer.
3. Provider envia X-Idempotency-Key.
4. Payload usa payment_method_id pix.
5. Payload usa amount do Order.
6. Payload usa payer email.
7. Payload usa external_reference.
8. Payload usa date_of_expiration.
9. Resposta Mercado Pago pending mapeia QR/copia-e-cola.
10. Resposta sem QR falha controladamente.
11. 401 do Mercado Pago falha controladamente.
12. Endpoint POST /payments/pix/orders/{orderId} retorna provider MercadoPago quando enabled.
13. Endpoint continua Fake quando MercadoPago disabled.
14. Idempotência: segunda chamada retorna PixPayment existente.
15. Order permanece PendingPayment.
16. Inventory reservation não é confirmada.
17. PixPayment permanece Pending.
18. Não expõe AccessToken em logs/DTO.

Executar:

dotnet build
dotnet test

==================================================
10. DOCUMENTAÇÃO
================

Criar:

* docs/payments/MP-PIX-001-provider-generation.md

Atualizar:

* docs/payments-pix.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* deploy/.env.test.example
* deploy/.env.hml.example
* docker-compose.yml, se necessário
* docs/testing.md, se existir

Documentar:

* como habilitar Mercado Pago em HML;
* envs necessárias;
* que Access Token fica só no backend;
* que Public Key não é segredo, mas não precisa ser usada nesta etapa;
* como testar POST /payments/pix/orders/{orderId};
* que webhook ainda não confirma pagamento;
* que Order continua PendingPayment;
* que estoque continua reservado;
* que Worker expira se não houver confirmação;
* limitação de CPF/documento real para produção;
* próxima fase webhook.

==================================================
11. BUILD E TESTES
==================

Executar:

dotnet build
dotnet test

Não pular testes.
Não remover testes existentes.
Se algum teste quebrar, corrigir causa real.

==================================================
12. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Provider implementado.
3. Configurações adicionadas.
4. Env vars adicionadas.
5. Como habilitar/desabilitar Mercado Pago.
6. Como o provider chama /v1/payments.
7. Como X-Idempotency-Key foi implementado.
8. Como QR Code / copia-e-cola são mapeados.
9. Como ProviderPaymentId é salvo.
10. Migrations criadas, se houver.
11. Testes criados.
12. Resultado build/test.
13. Docs atualizadas.
14. Limitações.
15. Próximo passo recomendado.

Critérios de aceite:

* MercadoPagoPixPaymentProvider existe.
* Fake provider continua funcionando quando MercadoPago disabled.
* Enabled=true exige AccessToken.
* POST /api/payments/pix/orders/{orderId} cria Pix real no Mercado Pago.
* Resposta retorna QR/copia-e-cola reais.
* PixPayment fica Pending.
* Order fica PendingPayment.
* Estoque não é confirmado.
* Idempotência evita pagamentos duplicados.
* AccessToken não é exposto.
* dotnet build passa.
* dotnet test passa.
* Docs refletem estado real.
