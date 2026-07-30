Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET, ASP.NET Core, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, e-commerce, pedidos, logística/fulfillment e backoffice operacional.

Objetivo:
Implementar a Fase 2 do módulo Delivery/Fulfillment no backend.

Contexto de negócio:
Shopflow está sendo usado para uma assessoria de compras, onde a venda é feita principalmente para lojistas e revendedores.

O cliente pode fazer várias compras intercaladas e deseja escolher uma preferência de entrega. O admin precisa acompanhar se o pedido já foi enviado ou entregue, sem misturar isso com pagamento.

Decisão arquitetural:
Status do pedido/pagamento e status de entrega são dimensões separadas.

Não usar OrderStatus para representar envio.
Não usar PaymentStatus para representar envio.
Criar FulfillmentStatus próprio.

Fase atual:
Implementar base por pedido.

Não implementar ainda:
- DeliveryBatch;
- agrupamento de pedidos;
- seleção em massa;
- chat nativo;
- WhatsApp;
- integração Correios/transportadora;
- cálculo de frete;
- rastreamento automático;
- status avançados;
- feriados;
- mudança no fluxo Pix;
- mudança na reserva de estoque.

==================================================
1. ESCOPO DA FASE 2
==================================================

Implementar no backend:

1. DeliveryMethod preferido pelo cliente:
   - Transportadora
   - Ônibus de excursão
   - Correios

2. PreferredDeliveryDate:
   - data preferida pelo cliente;
   - mínimo 2 dias úteis após a compra/sessão;
   - MVP: dias úteis = segunda a sexta;
   - feriados ficam fora do escopo.

3. CustomerOrderNote:
   - observação do cliente no pedido;
   - capturada no checkout;
   - visível para admin e cliente.

4. InternalOrderNote:
   - observação interna do admin;
   - visível apenas para admin;
   - não expor em endpoints públicos/customer.

5. FulfillmentStatus:
   - AwaitingShipment
   - Shipped
   - Delivered

6. Admin actions:
   - marcar pedido como enviado;
   - marcar pedido como entregue;
   - atualizar observação interna do pedido.

7. DTOs:
   - admin deve ver todos os campos operacionais;
   - customer/guest deve ver apenas campos seguros;
   - internal notes nunca devem ir para customer/guest.

==================================================
2. AUDITORIA INICIAL
==================================================

Antes de implementar, auditar:

- Orders Domain:
  - Order entity
  - OrderItem
  - OrderStatus
  - DTOs de order
  - handlers de create order from checkout session
  - customer orders
  - guest order status
  - admin orders list/detail

- Checkout:
  - CheckoutSession
  - CheckoutSessionItem
  - CreateCheckoutSession endpoint/request
  - CreateOrderFromCheckoutSession handler

- PaymentsPix:
  - confirmar se marca Order como Paid sem interferir em delivery

- EF mappings/migrations:
  - schema orders
  - schema checkout
  - índices existentes

- Endpoints:
  - POST /api/checkout/sessions
  - POST /api/orders/from-checkout-session
  - GET /api/orders/guest/{orderId}/status
  - GET /api/customer/orders
  - GET /api/customer/orders/{orderId}
  - GET /api/admin/orders
  - GET /api/admin/orders/{orderId}

Objetivo da auditoria:
- identificar onde adicionar os campos;
- garantir compatibilidade com frontend atual;
- não quebrar payloads existentes;
- preservar pedidos antigos.

==================================================
3. MODELAGEM SUGERIDA
==================================================

Criar enums no módulo Orders, ou no local mais coerente com a arquitetura atual:

DeliveryMethod:
- Carrier
- ExcursionBus
- Correios

Labels de UI ficam no frontend/docs, mas os códigos devem ser estáveis.

FulfillmentStatus:
- AwaitingShipment
- Shipped
- Delivered

Campos sugeridos no Order:

Delivery/preference:
- PreferredDeliveryMethod nullable
- PreferredDeliveryDate nullable DateOnly
- CustomerOrderNote nullable string max 1000

Admin/internal:
- InternalOrderNote nullable string max 2000

Fulfillment:
- FulfillmentStatus required, default AwaitingShipment
- FinalDeliveryMethod nullable
- TrackingCode nullable string max 120
- ShippedAt nullable DateTimeOffset
- DeliveredAt nullable DateTimeOffset
- FulfillmentUpdatedAt nullable DateTimeOffset
- FulfillmentUpdatedByAdminId nullable Guid/string conforme IdentityAccess atual

Se o projeto preferir Value Objects:
- OrderDeliveryPreference
- OrderFulfillment
- OrderNotes

Pode usar Value Object se estiver alinhado com o padrão atual do domínio.
Caso contrário, usar campos diretos com encapsulamento na entidade Order.

Importante:
- existing orders devem receber FulfillmentStatus = AwaitingShipment.
- campos de preferência podem ser nullable para retrocompatibilidade.
- não tornar DeliveryMethod obrigatório no backend ainda, porque o frontend atual pode não enviar.

==================================================
4. CHECKOUT SESSION
==================================================

Adicionar campos opcionais na CheckoutSession para capturar preferência antes do pedido:

- PreferredDeliveryMethod nullable
- PreferredDeliveryDate nullable DateOnly
- CustomerOrderNote nullable string max 1000

Atualizar request de criação de checkout session para aceitar:

{
  "preferredDeliveryMethod": "Carrier" | "ExcursionBus" | "Correios",
  "preferredDeliveryDate": "2026-08-03",
  "customerOrderNote": "Enviar junto com pedido anterior"
}

Nomes podem ser ajustados ao padrão atual do projeto, mas devem ser documentados.

Regras:
- todos opcionais no backend nesta fase;
- se preferredDeliveryDate vier preenchida, validar mínimo 2 dias úteis;
- se preferredDeliveryMethod vier preenchido, validar enum;
- se customerOrderNote vier preenchida, trim e limite 1000;
- se string vier vazia, persistir como null.

Ao criar Order a partir da CheckoutSession:
- copiar PreferredDeliveryMethod;
- copiar PreferredDeliveryDate;
- copiar CustomerOrderNote.

Não alterar reserva de estoque.
Não alterar cálculo de preço.
Não alterar SalesRule.
Não alterar Pix.

==================================================
5. REGRA DE DATA — MÍNIMO 2 DIAS ÚTEIS
==================================================

Regra:
A data preferida deve ser no mínimo 2 dias úteis após a data de criação da compra/sessão.

MVP:
- dias úteis = segunda a sexta;
- sábado e domingo não contam;
- feriados fora do escopo.

Exemplos:
- compra na segunda → primeira data permitida: quarta;
- compra na terça → primeira data permitida: quinta;
- compra na quarta → primeira data permitida: sexta;
- compra na quinta → primeira data permitida: segunda;
- compra na sexta → primeira data permitida: terça;
- compra no sábado → primeira data permitida: terça;
- compra no domingo → primeira data permitida: terça.

Criar helper/service testável:

BusinessDayCalculator
ou
DeliveryDatePolicy

Métodos sugeridos:
- AddBusinessDays(DateOnly startDate, int businessDays)
- GetMinimumPreferredDeliveryDate(DateOnly purchaseDate)
- IsValidPreferredDeliveryDate(DateOnly purchaseDate, DateOnly preferredDate)

Usar TimeProvider/Clock existente se houver.
Se o projeto tiver padrão de relógio, seguir o padrão atual.
Se não houver, criar abstração simples apenas se necessário.

Não usar DateTime.Now solto em vários lugares.

Validação:
- se preferredDeliveryDate < minimumDate, retornar ValidationProblem/ProblemDetails com campo preferredDeliveryDate;
- mensagem amigável em português ou code estável.

Código sugerido:
DELIVERY_DATE_TOO_SOON

Mensagem sugerida:
"A data preferida de entrega deve ser de pelo menos 2 dias úteis após a compra."

==================================================
6. ORDER ENTITY — COMPORTAMENTOS DE FULFILLMENT
==================================================

Adicionar métodos na entidade Order, seguindo padrão DDD atual:

- SetDeliveryPreference(...)
- SetCustomerOrderNote(...)
- SetInternalOrderNote(...)
- MarkAsShipped(...)
- MarkAsDelivered(...)

Regras de MarkAsShipped:
- pedido precisa estar pago/confirmado para ser enviado;
- não permitir envio de pedido PendingPayment, Canceled ou Expired;
- se já Delivered, não permitir voltar para Shipped;
- se já Shipped, operação pode ser idempotente ou retornar erro controlado; escolher e documentar.
- setar FulfillmentStatus = Shipped;
- setar ShippedAt;
- setar FinalDeliveryMethod, se informado;
- setar TrackingCode, se informado;
- setar FulfillmentUpdatedAt;
- setar FulfillmentUpdatedByAdminId, se disponível.

Regras de MarkAsDelivered:
- pedido precisa estar pago/confirmado;
- não permitir entrega de pedido PendingPayment, Canceled ou Expired;
- se já Delivered, operação pode ser idempotente ou retornar erro controlado; escolher e documentar.
- preferencialmente permitir Delivered a partir de Shipped.
- Se o negócio precisar permitir entrega direta a partir de AwaitingShipment, documentar a decisão.
- Recomendação MVP:
  - permitir Delivered a partir de Shipped;
  - se tentar Delivered sem Shipped, retornar erro:
    ORDER_MUST_BE_SHIPPED_BEFORE_DELIVERED.
- setar FulfillmentStatus = Delivered;
- setar DeliveredAt;
- setar FulfillmentUpdatedAt;
- setar FulfillmentUpdatedByAdminId, se disponível.

Não criar transição reversa nesta fase.
Não criar cancelamento de fulfillment nesta fase.

==================================================
7. ADMIN ENDPOINTS
==================================================

Criar endpoints protegidos por Backoffice:

1. Marcar como enviado:

POST /api/admin/orders/{orderId}/fulfillment/ship

Payload sugerido:

{
  "finalDeliveryMethod": "Carrier",
  "trackingCode": "ABC123",
  "internalNote": "Enviado pela transportadora combinada com a cliente"
}

Campos:
- finalDeliveryMethod opcional;
- trackingCode opcional max 120;
- internalNote opcional max 2000.

Resposta:
- retornar AdminOrderDetailDto atualizado
  ou DTO específico de fulfillment.
Recomendação:
- retornar AdminOrderDetailDto atualizado para facilitar frontend.

2. Marcar como entregue:

POST /api/admin/orders/{orderId}/fulfillment/deliver

Payload sugerido:

{
  "internalNote": "Cliente confirmou recebimento pelo WhatsApp"
}

Resposta:
- retornar AdminOrderDetailDto atualizado.

3. Atualizar observação interna:

PUT /api/admin/orders/{orderId}/internal-note

Payload:

{
  "internalNote": "Segurar envio até sexta"
}

Resposta:
- retornar AdminOrderDetailDto atualizado
  ou 204.
Recomendação:
- retornar AdminOrderDetailDto atualizado.

Regras:
- todos exigem Backoffice;
- todos exigem CSRF se o padrão admin atual exigir para mutations;
- order inexistente → 404;
- usuário sem permissão → 403;
- transição inválida → ProblemDetails 400/409 com code estável;
- não expor internalNote em endpoints customer/guest.

==================================================
8. ADMIN ORDERS LIST / DETAIL
==================================================

Atualizar Admin Order DTOs.

AdminOrderListItem deve incluir:
- fulfillmentStatus;
- preferredDeliveryMethod;
- preferredDeliveryDate;
- shippedAt;
- deliveredAt;
- trackingCode, se fizer sentido na lista;
- customerOrderNote? opcional, talvez só detail;
- internalOrderNote? preferencialmente só detail.

AdminOrderDetail deve incluir:
- preferredDeliveryMethod;
- preferredDeliveryDate;
- customerOrderNote;
- internalOrderNote;
- fulfillmentStatus;
- finalDeliveryMethod;
- trackingCode;
- shippedAt;
- deliveredAt;
- fulfillmentUpdatedAt;
- fulfillmentUpdatedByAdminId, se existir.

Adicionar filtro opcional em GET /api/admin/orders:
- fulfillmentStatus

Valores:
- all omitido/default
- AwaitingShipment
- Shipped
- Delivered

Não quebrar filtros existentes:
- page
- pageSize
- status
- paymentStatus
- q
- createdFrom
- createdTo
- paidOnly
- sort

Regra operacional importante:
Para listar pedidos pendentes de envio no futuro, frontend poderá usar:
- status=Paid
- fulfillmentStatus=AwaitingShipment

==================================================
9. CUSTOMER / GUEST DTOS
==================================================

Atualizar DTOs públicos/cliente com campos seguros de entrega.

Guest order status:
GET /api/orders/guest/{orderId}/status

Customer orders:
GET /api/customer/orders
GET /api/customer/orders/{orderId}

Campos seguros:
- preferredDeliveryMethod;
- preferredDeliveryDate;
- customerOrderNote;
- fulfillmentStatus;
- finalDeliveryMethod;
- trackingCode;
- shippedAt;
- deliveredAt.

Não expor:
- internalOrderNote;
- fulfillmentUpdatedByAdminId;
- dados técnicos internos;
- notas internas.

Se preferir agrupar em objeto:

delivery:
{
  "preferredMethod": "Carrier",
  "preferredDate": "2026-08-03",
  "customerNote": "...",
  "fulfillmentStatus": "AwaitingShipment",
  "finalMethod": "Carrier",
  "trackingCode": "ABC123",
  "shippedAt": "...",
  "deliveredAt": "..."
}

A estrutura pode seguir o padrão atual dos DTOs.
Documentar no api-contracts.

==================================================
10. MIGRATIONS / EF
==================================================

Criar migration.

Sugestão de nome:
AddOrderDeliveryFulfillment

Adicionar campos necessários em:
- checkout.checkout_sessions, se houver schema/tabela própria;
- orders.orders.

Campos em orders:
- PreferredDeliveryMethod nullable
- PreferredDeliveryDate nullable
- CustomerOrderNote nullable
- InternalOrderNote nullable
- FulfillmentStatus not null default AwaitingShipment
- FinalDeliveryMethod nullable
- TrackingCode nullable
- ShippedAt nullable
- DeliveredAt nullable
- FulfillmentUpdatedAt nullable
- FulfillmentUpdatedByAdminId nullable

Campos em checkout sessions:
- PreferredDeliveryMethod nullable
- PreferredDeliveryDate nullable
- CustomerOrderNote nullable

Ajustar EF mappings:
- max lengths;
- enum conversion conforme padrão atual;
- default para FulfillmentStatus;
- índices se necessário.

Índices sugeridos:
- Orders FulfillmentStatus + CreatedAt
- Orders CustomerUserId + FulfillmentStatus + CreatedAt, se útil
- Não exagerar nos índices nesta fase.

==================================================
11. PROBLEM DETAILS / CODES
==================================================

Usar ProblemDetails no padrão atual.

Codes sugeridos:
- DELIVERY_DATE_TOO_SOON
- INVALID_DELIVERY_METHOD
- ORDER_NOT_PAID_FOR_SHIPMENT
- ORDER_CANNOT_BE_SHIPPED
- ORDER_CANNOT_BE_DELIVERED
- ORDER_MUST_BE_SHIPPED_BEFORE_DELIVERED
- INTERNAL_NOTE_TOO_LONG
- CUSTOMER_ORDER_NOTE_TOO_LONG
- TRACKING_CODE_TOO_LONG

Mensagens amigáveis:
- "A data preferida de entrega deve ser de pelo menos 2 dias úteis após a compra."
- "Este pedido ainda não pode ser marcado como enviado."
- "Este pedido ainda não pode ser marcado como entregue."
- "O pedido precisa estar marcado como enviado antes de ser entregue."
- "A observação interna deve ter no máximo 2000 caracteres."
- "A observação do cliente deve ter no máximo 1000 caracteres."

==================================================
12. COMPATIBILIDADE
==================================================

Manter compatibilidade com frontend atual:

- CreateCheckoutSession deve continuar funcionando sem os novos campos.
- CreateOrderFromCheckoutSession deve continuar funcionando para sessões antigas.
- Orders antigos devem ter FulfillmentStatus default AwaitingShipment.
- DTOs antigos podem ganhar campos novos nullable.
- Não tornar obrigatório deliveryMethod no backend nesta fase.
- Não quebrar Cypress/FE atual.
- Não quebrar Pix.

==================================================
13. TESTES UNITÁRIOS
==================================================

Criar/ajustar testes:

BusinessDayCalculator / DeliveryDatePolicy:
1. segunda + 2 úteis = quarta.
2. terça + 2 úteis = quinta.
3. quarta + 2 úteis = sexta.
4. quinta + 2 úteis = segunda.
5. sexta + 2 úteis = terça.
6. sábado + 2 úteis = terça.
7. domingo + 2 úteis = terça.
8. data antes do mínimo é inválida.
9. data igual ao mínimo é válida.
10. data depois do mínimo é válida.

Order domain:
11. pedido pago pode ser marcado como enviado.
12. pedido pending payment não pode ser enviado.
13. pedido cancelado/expirado não pode ser enviado.
14. pedido enviado pode ser marcado como entregue.
15. pedido não enviado não pode ser entregue, se essa for a decisão final.
16. pedido entregue não pode voltar para enviado.
17. MarkAsShipped salva shippedAt/status.
18. MarkAsDelivered salva deliveredAt/status.
19. InternalOrderNote é normalizada/limitada.
20. CustomerOrderNote é normalizada/limitada.

Checkout/order creation:
21. checkout session aceita delivery fields opcionais.
22. preferredDeliveryDate inválida retorna erro.
23. preferredDeliveryDate válida é salva.
24. order copia delivery fields da checkout session.
25. sessão antiga/sem campos cria order normalmente.

Admin endpoints:
26. ship exige Backoffice.
27. deliver exige Backoffice.
28. internal-note exige Backoffice.
29. ship order inexistente retorna 404.
30. deliver order inexistente retorna 404.
31. ship pending payment retorna ProblemDetails.
32. deliver antes de shipped retorna ProblemDetails.
33. internalNote não aparece em customer/guest DTO.
34. fulfillmentStatus aparece em admin/customer/guest DTO.
35. admin list filtra fulfillmentStatus.

==================================================
14. TESTES DE INTEGRAÇÃO / HTTP
==================================================

Se o projeto tiver testes HTTP/integration:

1. POST /api/checkout/sessions com delivery fields retorna session com dados.
2. POST /api/checkout/sessions com preferredDeliveryDate menor que mínimo retorna 400.
3. POST /api/orders/from-checkout-session copia delivery fields.
4. GET /api/admin/orders/{id} retorna delivery/fulfillment/internalNote.
5. GET /api/customer/orders/{id} não retorna internalNote.
6. GET /api/orders/guest/{id}/status não retorna internalNote.
7. POST /api/admin/orders/{id}/fulfillment/ship muda status para Shipped.
8. POST /api/admin/orders/{id}/fulfillment/deliver muda status para Delivered.
9. GET /api/admin/orders?fulfillmentStatus=AwaitingShipment filtra corretamente.
10. Mutations admin respeitam CSRF/padrão atual.

==================================================
15. DOCUMENTAÇÃO
==================================================

Atualizar:

- docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md
- docs/orders/admin-orders.md, se existir
- docs/orders/customer-orders.md, se existir
- docs/orders/guest-order-access.md, se existir
- docs/checkout/checkout-session.md, se existir
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Atualizar também contrato para frontend, se existir:
- apps/web/docs/ai-context/api-contracts.md
ou documento equivalente no monorepo.

Documentar:
- novos campos de checkout;
- novos campos de order;
- enum DeliveryMethod;
- enum FulfillmentStatus;
- regra dos 2 dias úteis;
- endpoints admin ship/deliver/internal-note;
- filtro fulfillmentStatus em admin orders;
- internalOrderNote é admin-only;
- batch/agrupamento fica Fase 3;
- WhatsApp/chat ficam fora desta fase.

==================================================
16. NÃO FAZER
==================================================

Não implementar:
- DeliveryBatch;
- agrupamento de pedidos;
- bulk action;
- seleção em massa;
- chat;
- WhatsApp;
- rastreamento automático;
- integração Correios/transportadora;
- cálculo de frete;
- preço de entrega;
- tabela de feriados;
- status avançados como Em Separação, Pronto para Envio, Problema na Entrega;
- frontend.

Não alterar:
- Pix;
- PaymentsPix;
- Inventory;
- Product;
- Catalog;
- SalesRules;
- Cart;
- ProductCard;
- Storefront;
- Customer auth;
- Admin auth.

Não usar:
- OrderStatus para representar entrega;
- PaymentStatus para representar entrega;
- dados fake;
- endpoint público para admin.

==================================================
17. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Enums criados.
3. Campos adicionados em CheckoutSession.
4. Campos adicionados em Order.
5. Migration criada.
6. Como funciona a regra de 2 dias úteis.
7. Endpoints admin criados.
8. DTOs admin atualizados.
9. DTOs customer/guest atualizados.
10. Filtro fulfillmentStatus implementado ou justificativa se ficou pendente.
11. ProblemDetails/codes criados.
12. Testes unitários criados/alterados.
13. Testes HTTP/integration criados/alterados.
14. Resultado dotnet build.
15. Resultado dotnet test afetado.
16. Docs atualizadas.
17. Pendências para Fase 3.

Critérios de aceite:
- backend aceita preferência de entrega no checkout;
- data preferida respeita mínimo de 2 dias úteis;
- order copia dados de entrega da checkout session;
- FulfillmentStatus é separado de OrderStatus/PaymentStatus;
- admin consegue marcar pedido como enviado;
- admin consegue marcar pedido como entregue;
- internalOrderNote não vaza para cliente/convidado;
- admin list/detail expõem dados de fulfillment;
- customer/guest veem apenas dados seguros;
- pedidos antigos continuam funcionando;
- Pix continua funcionando;
- build/testes passam.