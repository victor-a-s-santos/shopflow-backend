Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, ASP.NET Core Identity, cookies HttpOnly, segurança de dados de cliente e APIs de e-commerce.

Contexto atual:
O Shopflow já possui:

* Customer Identity com cookie HttpOnly separado do Backoffice.
* Admin Identity Backoffice separado.
* Checkout convidado funcionando.
* Orders criadas a partir de CheckoutSession.
* PaymentsPix com Mercado Pago.
* Worker de reconciliação Mercado Pago funcionando:

  * consulta GET /v1/orders/{ProviderOrderId};
  * quando processed/accredited:

    * PixPayment vira Paid;
    * Order vira Paid;
    * reserva de estoque é confirmada.
* GuestOrderAccessToken para status de pedido convidado.
* Admin Orders backend pronto:

  * GET /api/admin/orders
  * GET /api/admin/orders/{orderId}
* Admin Orders frontend pronto.

Objetivo deste prompt:
Implementar o backend mínimo de Customer Orders para MVP.

Endpoints esperados:

1. GET /api/customer/orders
2. GET /api/customer/orders/{orderId}

Ambos devem ser acessíveis somente pelo cliente autenticado via CustomerCookie.

Não mexer no frontend neste prompt.
Não implementar admin.
Não implementar cancelamento, reembolso, envio, nota fiscal ou tracking.
Não listar pedidos guest automaticamente por e-mail.
Não usar GuestOrderAccessToken para área logada.

==================================================

1. REGRA DE SEGURANÇA PRINCIPAL
   ==================================================

Customer Orders deve retornar somente pedidos vinculados ao CustomerUserId autenticado.

Não usar apenas e-mail para buscar pedidos, porque isso pode vazar pedido antigo de guest ou pedido feito por outra pessoa usando o mesmo e-mail.

Regra correta:

* Pedido feito enquanto cliente estava logado → aparece em “Meus pedidos”.
* Pedido guest → não aparece automaticamente na conta.
* Claim/importação de pedido guest por token pode ser pós-MVP.

Se Orders ainda não tiver CustomerUserId:

* adicionar campo nullable CustomerUserId em orders.orders.
* criar migration.
* preencher esse campo ao criar Order quando a requisição tiver CustomerCookie válido.
* manter nullable para pedidos guest.

Se CheckoutSession ainda não tiver CustomerUserId:

* avaliar se precisa adicionar também em checkout.sessions.
* Para MVP, é aceitável gravar CustomerUserId no Order no momento de POST /api/orders/from-checkout-session, desde que o usuário esteja autenticado como customer nessa chamada.
* Se a arquitetura do projeto favorecer, adicionar também CustomerUserId em CheckoutSession para rastreabilidade.

==================================================
2. ASSOCIAÇÃO DO PEDIDO AO CLIENTE LOGADO
=========================================

Atualizar o fluxo de criação de Order:

Endpoint atual:
POST /api/orders/from-checkout-session

Comportamento novo:

* Continua público para guest checkout.
* Se a requisição tiver CustomerCookie válido:

  * resolver CustomerUserId do usuário autenticado customer;
  * salvar CustomerUserId no Order.
* Se não houver customer autenticado:

  * Order.CustomerUserId = null;
  * fluxo guest permanece igual;
  * GuestOrderAccessToken continua funcionando.

Cuidados:

* Não exigir login nesse endpoint.
* Não quebrar checkout guest.
* Não usar admin cookie como customer.
* Não associar pedido a customer se a autenticação for Backoffice.
* Usar somente o principal/esquema/policy customer correto.

==================================================
3. GET /api/customer/orders
===========================

Criar query handler, endpoint e DTO para lista de pedidos do cliente logado.

Auth:

* Customer authenticated only.
* Admin Backoffice não deve acessar por estar logado como admin.
* Guest/sem login recebe 401.

Parâmetros:

* page: int, default 1
* pageSize: int, default 10, máximo 50
* status: opcional
* paymentStatus: opcional
* createdFrom: opcional
* createdTo: opcional
* sort: default createdAt_desc

Retorno sugerido:

{
"items": [
{
"id": "guid",
"status": "PendingPayment|Paid|Canceled|Expired",
"createdAt": "...",
"paidAt": "...",
"subtotal": 0,
"shippingAmount": 0,
"total": 0,
"itemsCount": 2,
"firstItemName": "Camiseta...",
"payment": {
"status": "Pending|Paid|Canceled|Expired|Failed",
"provider": "MercadoPago",
"paidAt": "...",
"expiresAt": "..."
}
}
],
"page": 1,
"pageSize": 10,
"totalItems": 0,
"totalPages": 0
}

Não retornar:

* providerOrderId;
* providerPaymentId;
* providerTransactionId;
* QR Code;
* copia-e-cola;
* ticketUrl;
* GuestAccessToken;
* token hash;
* webhook raw;
* AccessToken;
* WebhookSecret.

==================================================
4. GET /api/customer/orders/{orderId}
=====================================

Retornar detalhe do pedido somente se:

* Order.CustomerUserId == CustomerUserId autenticado.

Se pedido não existe ou pertence a outro customer:

* retornar 404, não 403, para não revelar existência.

Retorno sugerido:

{
"id": "guid",
"status": "Paid",
"createdAt": "...",
"updatedAt": "...",
"paidAt": "...",
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
"provider": "MercadoPago",
"status": "Paid",
"paidAt": "...",
"expiresAt": "..."
}
}

Não retornar:

* dados internos do Mercado Pago;
* providerOrderId;
* providerPaymentId;
* providerTransactionId;
* QR Code;
* copia-e-cola;
* ticketUrl;
* GuestAccessToken;
* token hash;
* webhook raw;
* secrets.

==================================================
5. CUSTOMERUSERID
=================

Verificar qual é o tipo de ID do customer no IdentityAccess.

Provavelmente Guid.

Criar campo:

Order.CustomerUserId nullable

Migration:
AddCustomerUserIdToOrders

Índice recomendado:

* IX_orders_CustomerUserId_CreatedAt
  ou equivalente no schema orders.

Se houver separação de schemas/tabelas, respeitar padrão existente.

Não criar FK rígida se os módulos estiverem desacoplados e o projeto evitar FK cross-module.
Se o projeto já usa FK cross-module com Identity, seguir padrão existente.

==================================================
6. AUTORIZAÇÃO
==============

Usar policy/esquema customer existente.

Critérios:

* Sem login → 401.
* Customer autenticado → acessa seus próprios pedidos.
* Customer A não vê pedido de Customer B.
* Admin Backoffice não acessa endpoint customer por estar logado como admin.
* GuestOrderAccessToken não dá acesso a /api/customer/orders.

==================================================
7. VALIDAÇÕES
=============

* page >= 1
* pageSize entre 1 e 50
* createdFrom <= createdTo
* status inválido retorna 400
* paymentStatus inválido retorna 400
* orderId Guid válido pela rota

==================================================
8. TESTES OBRIGATÓRIOS
======================

Criar/ajustar testes conforme padrão do projeto.

Cobrir:

1. POST /api/orders/from-checkout-session continua funcionando para guest.
2. Guest order fica com CustomerUserId null.
3. POST /api/orders/from-checkout-session com CustomerCookie válido grava CustomerUserId no Order.
4. Admin cookie não grava CustomerUserId como customer.
5. GET /api/customer/orders sem login retorna 401.
6. GET /api/customer/orders com customer retorna apenas pedidos daquele CustomerUserId.
7. GET /api/customer/orders não retorna pedidos guest do mesmo e-mail.
8. GET /api/customer/orders não retorna pedidos de outro customer.
9. GET /api/customer/orders pagina corretamente.
10. GET /api/customer/orders filtra por status.
11. GET /api/customer/orders filtra por paymentStatus.
12. GET /api/customer/orders/{orderId} retorna detalhe do próprio pedido.
13. GET /api/customer/orders/{orderId} retorna 404 para pedido de outro customer.
14. GET /api/customer/orders/{orderId} retorna 404 para pedido guest não vinculado.
15. DTO customer não retorna ProviderOrderId, ProviderPaymentId, ProviderTransactionId, CopyPasteCode, QrCode, QrCodeImageUrl, TicketUrl, GuestAccessToken, token hash, WebhookSecret ou AccessToken.
16. Customer vê payment status Paid quando PixPayment está Paid.
17. Customer vê payment status Pending quando PixPayment está Pending.
18. Build e testes passam.

Não chamar Mercado Pago real nos testes.

==================================================
9. DOCUMENTAÇÃO
===============

Criar/atualizar:

* docs/orders/customer-orders.md
* docs/orders/admin-orders.md, se precisar referenciar diferença admin/customer
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/backend-next-actions.md
* docs/ai-context/technical-debt.md

Documentar:

* endpoints customer;
* diferença entre pedido guest e pedido logado;
* por que não buscar pedido por e-mail;
* campos omitidos por segurança;
* pendências pós-MVP:

  * claim/importação de pedido guest;
  * segunda via de pagamento Pix;
  * cancelamento pelo cliente;
  * rastreio;
  * timeline de pedido;
  * e-mail transacional.

==================================================
10. NÃO FAZER AGORA
===================

Não implementar:

* frontend customer orders;
* admin;
* claim de pedido guest;
* cancelamento pelo cliente;
* reembolso;
* envio;
* rastreamento;
* nota fiscal;
* timeline;
* e-mails;
* segunda via de Pix;
* reabrir pagamento expirado;
* edição de endereço;
* avaliação de produto.

==================================================
11. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Migration criada.
3. Campo CustomerUserId adicionado ou reaproveitado.
4. Endpoints implementados.
5. DTOs criados.
6. Como testar manualmente.
7. Testes criados/alterados.
8. Resultado dotnet build.
9. Resultado dotnet test.
10. Pendências para frontend customer orders.

Critérios de aceite:

* Pedido guest continua funcionando.
* Pedido feito com customer logado fica vinculado ao CustomerUserId.
* Customer lista somente próprios pedidos.
* Customer vê detalhe somente de pedido próprio.
* Pedido guest não aparece automaticamente por e-mail.
* Endpoints são CustomerCookie-only.
* Não vaza campos sensíveis.
* Build passa.
* Testes passam.
