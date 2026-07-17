Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, segurança Backoffice, e-commerce e APIs administrativas.

Contexto atual do projeto:
O Shopflow já possui:

* Catálogo com produtos, SKUs e imagens.
* Estoque com reservas internas.
* CheckoutSession com reserva de estoque.
* Orders criadas a partir de CheckoutSession.
* PaymentsPix com Mercado Pago Orders API.
* Worker de expiração.
* Worker de reconciliação Mercado Pago Pix funcionando:

  * consulta GET /v1/orders/{ProviderOrderId};
  * quando Mercado Pago retorna processed/accredited:

    * marca PixPayment como Paid;
    * marca Order como Paid;
    * confirma reserva de estoque.
* Admin auth real:

  * cookie HttpOnly;
  * CSRF;
  * policy Backoffice;
  * role Owner + claim is_staff=true.
* Frontend admin já existe para catálogo/estoque, mas ainda falta visão de pedidos.

Objetivo deste prompt:
Implementar o backend mínimo de Admin Orders para o MVP.

Não mexer no frontend neste prompt.
Não implementar customer orders ainda.
Não implementar envio, cancelamento, reembolso, nota fiscal ou logística avançada agora.
Foco: permitir que o lojista veja pedidos e detalhes operacionais.

==================================================

1. ESCOPO
   ==================================================

Criar endpoints administrativos protegidos por Backoffice:

1. GET /api/admin/orders

Lista paginada de pedidos.

2. GET /api/admin/orders/{orderId}

Detalhe completo operacional do pedido.

Ambos devem exigir:

* usuário autenticado admin;
* policy Backoffice;
* cookie admin;
* CSRF apenas onde aplicável, mas como são GET, não devem exigir token CSRF se o padrão do projeto não exige GET CSRF.

==================================================
2. GET /api/admin/orders
========================

Criar query handler, endpoint e DTO para listar pedidos.

Parâmetros sugeridos:

* page: int, default 1
* pageSize: int, default 20, máximo 100
* status: opcional
* paymentStatus: opcional
* q: opcional, busca por:

  * e-mail do cliente
  * nome do cliente
  * telefone
  * id do pedido, se for Guid válido
* createdFrom: opcional DateTime/DateOnly
* createdTo: opcional DateTime/DateOnly
* paidOnly: opcional bool
* sort: opcional, default createdAt_desc

Ordenação padrão:

* CreatedAt desc

Retorno sugerido:

{
"items": [
{
"id": "guid",
"status": "PendingPayment|Paid|Canceled|Expired|...",
"customerFullName": "...",
"customerEmail": "...",
"customerPhone": "...",
"subtotal": 0,
"shippingAmount": 0,
"total": 0,
"createdAt": "...",
"paidAt": "...",
"itemsCount": 0,
"payment": {
"id": "guid",
"provider": "MercadoPago|Fake",
"status": "Pending|Paid|Canceled|Expired|Failed",
"providerOrderId": "ORD...",
"providerPaymentId": "PAY...",
"providerStatus": "processed",
"providerStatusDetail": "accredited",
"providerTransactionStatus": "processed",
"providerTransactionStatusDetail": "accredited",
"paidAt": "...",
"expiresAt": "..."
}
}
],
"page": 1,
"pageSize": 20,
"totalItems": 0,
"totalPages": 0
}

Observações:

* Se não houver pagamento Pix associado, payment pode ser null.
* Não retornar CopyPasteCode, QR Code, QR Image, ticketUrl, guest access token, token hash, webhook raw, headers ou dados internos sensíveis.
* Admin pode ver PII do pedido porque é Backoffice, mas não deve receber segredos técnicos.

==================================================
3. GET /api/admin/orders/{orderId}
==================================

Criar query handler, endpoint e DTO para detalhe do pedido.

Retorno sugerido:

{
"id": "guid",
"status": "PendingPayment|Paid|Canceled|Expired|...",
"createdAt": "...",
"updatedAt": "...",
"paidAt": "...",
"customer": {
"fullName": "...",
"email": "...",
"phone": "..."
},
"shippingAddress": {
"street": "...",
"number": "...",
"complement": "...",
"neighborhood": "...",
"city": "...",
"state": "...",
"zipCode": "..."
},
"amounts": {
"subtotal": 0,
"shippingAmount": 0,
"total": 0
},
"items": [
{
"id": "guid",
"skuId": "guid",
"skuCode": "...",
"productName": "...",
"quantity": 1,
"unitPrice": 0,
"subtotal": 0
}
],
"payment": {
"id": "guid",
"provider": "MercadoPago",
"status": "Paid",
"providerOrderId": "ORD...",
"providerPaymentId": "PAY...",
"providerStatus": "processed",
"providerStatusDetail": "accredited",
"providerTransactionId": "PAY...",
"providerTransactionStatus": "processed",
"providerTransactionStatusDetail": "accredited",
"providerApprovedAt": "...",
"providerUpdatedAt": "...",
"paidAt": "...",
"expiresAt": "..."
}
}

Não retornar:

* CopyPasteCode;
* QrCode;
* QrCodeImageUrl;
* TicketUrl, salvo se o projeto decidir explicitamente que admin pode abrir o link, mas para MVP melhor não expor;
* GuestAccessToken;
* hash de token;
* webhook raw;
* x-signature;
* MercadoPago AccessToken;
* WebhookSecret;
* dados de DataProtection;
* cookies.

==================================================
4. INTEGRAÇÃO COM ORDERS E PAYMENTSPIX
======================================

A listagem e o detalhe devem cruzar os dados de:

* orders.orders
* orders.order_items
* payments_pix.pix_payments

Pode ser via:

* EF Core queries;
* read model;
* projection direta;
* repository/query service conforme padrão do projeto.

Evitar N+1.

Para listagem:

* projetar direto para DTO quando possível.
* trazer payment summary por OrderId.

Para detalhe:

* trazer itens e payment associado.

Se houver múltiplos PixPayments para o mesmo OrderId:

* preferir o mais recente por CreatedAt desc;
* ou, se existir regra atual de unicidade, respeitar a regra existente.
* documentar a decisão.

==================================================
5. SEGURANÇA
============

Todos os endpoints devem ser Backoffice-only.

Verificar padrão existente em:

* admin catalog endpoints;
* admin inventory endpoints;
* auth admin.

Critérios:

* usuário não autenticado recebe 401.
* usuário autenticado customer não acessa.
* usuário autenticado sem Backoffice não acessa.
* admin Backoffice acessa.

Não criar endpoint público.

Não reaproveitar GuestOrderAccessToken.

Não vazar PII em endpoint público.

==================================================
6. VALIDAÇÃO
============

Validar:

* page >= 1
* pageSize entre 1 e 100
* orderId Guid válido pela rota
* status/paymentStatus, se inválidos, retornar 400 ou ignorar conforme padrão do projeto. Preferir 400 com erro claro.
* createdFrom <= createdTo, se ambos forem enviados.

==================================================
7. TESTES OBRIGATÓRIOS
======================

Criar/ajustar testes unitários/integrados conforme padrão existente.

Cobrir:

1. GET /api/admin/orders exige Backoffice.
2. GET /api/admin/orders/{id} exige Backoffice.
3. Lista retorna pedidos paginados.
4. Lista ordena por CreatedAt desc.
5. Lista filtra por status.
6. Lista filtra por paymentStatus.
7. Lista busca por e-mail.
8. Lista busca por nome.
9. Lista busca por telefone.
10. Lista busca por orderId quando q é Guid.
11. Lista inclui payment summary quando existe PixPayment.
12. Lista retorna payment null quando não existe PixPayment.
13. Detalhe retorna customer, shipping, amounts, items e payment.
14. Detalhe 404 quando pedido não existe.
15. DTO não retorna CopyPasteCode, QrCode, QrCodeImageUrl, GuestAccessToken, token hash, WebhookSecret ou AccessToken.
16. Customer autenticado não acessa endpoints admin.
17. Admin acessa endpoints admin.
18. Paginação respeita limite máximo de pageSize 100.

Não chamar Mercado Pago real em testes.

==================================================
8. DOCUMENTAÇÃO
===============

Atualizar/criar documentação:

* docs/orders/admin-orders.md

Atualizar se existirem:

* docs/ai-context/shopflow-current-state.md
* docs/ai-context/backend-next-actions.md
* docs/ai-context/technical-debt.md

Documentar:

* endpoints criados;
* parâmetros;
* exemplos de resposta;
* segurança Backoffice;
* campos omitidos por segurança;
* pendências pós-MVP:

  * mudança manual de status;
  * envio;
  * cancelamento;
  * reembolso;
  * nota fiscal;
  * fulfillment;
  * timeline de eventos do pedido.

==================================================
9. NÃO FAZER AGORA
==================

Não implementar:

* tela frontend;
* customer orders;
* e-mail;
* envio;
* transportadora;
* rastreamento;
* nota fiscal;
* reembolso;
* cancelamento manual;
* edição de endereço;
* timeline avançada;
* exportação CSV;
* relatórios financeiros;
* dashboard.

==================================================
10. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Endpoints implementados.
3. DTOs criados.
4. Como testar manualmente com curl/http.
5. Testes criados/alterados.
6. Resultado dotnet build.
7. Resultado dotnet test.
8. Pendências para o próximo prompt frontend.

Critérios de aceite:

* GET /api/admin/orders protegido por Backoffice.
* GET /api/admin/orders/{orderId} protegido por Backoffice.
* Admin consegue listar pedidos.
* Admin consegue ver detalhe do pedido.
* Dados de pagamento Pix aparecem de forma segura.
* Não vaza segredos/tokens/QR/copia-e-cola.
* Testes passam.
* Build passa.
