Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET, ASP.NET Core, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, e-commerce B2B, pedidos, logística/fulfillment, operações administrativas e consistência transacional.

Objetivo:
Implementar a Fase 3 do módulo Delivery/Fulfillment no backend: agrupamento de pedidos pendentes de envio do mesmo cliente em uma remessa/entrega agrupada.

Contexto de negócio:
Shopflow está sendo usado para uma assessoria de compras, onde a venda é feita principalmente para lojistas e revendedores.

Clientes podem fazer várias compras intercaladas ao longo do dia/semana e receber tudo junto. O admin precisa conseguir identificar pedidos pagos e ainda não enviados do mesmo cliente, agrupá-los e movimentar todos juntos como enviados ou entregues.

Fase 2 já implementada:
- DeliveryMethod:
  - Carrier
  - ExcursionBus
  - Correios

- FulfillmentStatus:
  - AwaitingShipment
  - Shipped
  - Delivered

- Order já possui:
  - PreferredDeliveryMethod
  - PreferredDeliveryDate
  - CustomerOrderNote
  - InternalOrderNote
  - FulfillmentStatus
  - FinalDeliveryMethod
  - TrackingCode
  - ShippedAt
  - DeliveredAt
  - FulfillmentUpdatedAt
  - FulfillmentUpdatedByAdminId

- Admin já possui endpoints:
  - POST /api/admin/orders/{id}/fulfillment/ship
  - POST /api/admin/orders/{id}/fulfillment/deliver
  - PUT /api/admin/orders/{id}/internal-note

Decisão:
A Fase 3 deve criar conceito próprio de remessa/entrega agrupada.

Nome preferencial:
DeliveryBatch

Se o projeto preferir semanticamente ShipmentBatch, pode usar ShipmentBatch, mas escolha apenas um nome e mantenha consistência em código, docs, DTOs e endpoints.

Não implementar frontend neste prompt.
Não implementar chat.
Não implementar WhatsApp.
Não implementar cálculo de frete.
Não implementar rastreio automático.
Não implementar integração Correios/transportadora.
Não implementar status avançados.
Não misturar OrderStatus/PaymentStatus com FulfillmentStatus.

==================================================
1. ESCOPO DA FASE 3
==================================================

Implementar backend para:

1. Identificar pedidos candidatos para agrupamento:
   - pedidos pagos;
   - fulfillmentStatus = AwaitingShipment;
   - mesmo cliente;
   - não pertencentes a outro batch ativo/finalizado.

2. Criar DeliveryBatch com vários pedidos.

3. Expor detalhe/listagem de DeliveryBatch no admin.

4. Marcar DeliveryBatch como enviado:
   - atualiza status da batch;
   - atualiza todos os pedidos vinculados para FulfillmentStatus = Shipped;
   - registra ShippedAt, FinalDeliveryMethod, TrackingCode/referência.

5. Marcar DeliveryBatch como entregue:
   - batch precisa estar Shipped;
   - atualiza status da batch;
   - atualiza todos os pedidos vinculados para FulfillmentStatus = Delivered;
   - registra DeliveredAt.

6. Guardar observação interna da batch.

7. Preservar notas internas individuais dos pedidos.

8. Garantir transação: ou todos os pedidos são atualizados, ou nenhum.

==================================================
2. MODELAGEM SUGERIDA
==================================================

Criar entidade:

DeliveryBatch

Campos sugeridos:
- Id
- BatchNumber ou DeliveryBatchNumber
- CustomerUserId nullable
- CustomerName nullable
- CustomerEmail nullable
- CustomerEmailNormalized nullable
- CustomerPhone nullable
- CustomerPhoneNormalized nullable
- DeliveryMethod nullable
- Status
- TrackingCode nullable max 120
- InternalNote nullable max 2000
- ShippedAt nullable
- DeliveredAt nullable
- CreatedAt
- CreatedByAdminId nullable
- UpdatedAt nullable
- UpdatedByAdminId nullable

Status da batch:

DeliveryBatchStatus:
- AwaitingShipment
- Shipped
- Delivered

Pode reutilizar FulfillmentStatus se isso fizer sentido no padrão do projeto, mas a recomendação é criar DeliveryBatchStatus para deixar claro que é status da remessa agrupada.

Relacionamento:
- DeliveryBatch possui vários pedidos.
- Pedido pertence a no máximo uma DeliveryBatch.

Implementação possível:
A) Adicionar DeliveryBatchId nullable em Order.
ou
B) Criar tabela join DeliveryBatchOrders com índice único em OrderId.

Recomendação:
Usar tabela join `orders.delivery_batch_orders` com índice único em `OrderId`, porque preserva rastreabilidade e evita acoplamento excessivo na tabela orders.

Tabelas sugeridas:
- orders.delivery_batches
- orders.delivery_batch_orders

delivery_batch_orders:
- DeliveryBatchId
- OrderId
- CreatedAt

Índices:
- DeliveryBatch.Status + CreatedAt
- DeliveryBatch.CustomerUserId + Status
- DeliveryBatch.CustomerEmailNormalized + Status
- DeliveryBatchOrders.OrderId unique
- DeliveryBatchOrders.DeliveryBatchId

BatchNumber:
- criar número operacional amigável, semelhante a OrderNumber se existir padrão.
- Exemplo UI futura: Remessa #30001.
- Se o projeto já usa sequence para OrderNumber, seguir padrão parecido.
- Se isso aumentar demais o escopo, usar Id técnico no backend, mas documentar que frontend terá menos UX.
- Recomendação: implementar BatchNumber.

==================================================
3. IDENTIDADE DO CLIENTE PARA AGRUPAMENTO
==================================================

Critério para agrupar pedidos do mesmo cliente:

1. Se todos os pedidos possuem CustomerUserId:
   - todos devem ter o mesmo CustomerUserId.

2. Se pedidos são convidados/guest:
   - usar CustomerEmailNormalized + CustomerPhoneNormalized.
   - nunca agrupar apenas por nome.

3. Não permitir misturar:
   - customerUserId diferente;
   - customer logado com guest de outro e-mail;
   - guest sem e-mail confiável;
   - pedidos com e-mails diferentes.

4. Se algum pedido não tiver identidade suficiente:
   - retornar erro controlado.

Codes sugeridos:
- DELIVERY_BATCH_CUSTOMER_MISMATCH
- DELIVERY_BATCH_CUSTOMER_IDENTITY_REQUIRED

==================================================
4. ELEGIBILIDADE DOS PEDIDOS
==================================================

Um pedido pode entrar em DeliveryBatch quando:

- existe;
- OrderStatus = Paid;
- FulfillmentStatus = AwaitingShipment;
- não está cancelado;
- não está expirado;
- não está pending payment;
- não está em outro DeliveryBatch;
- pertence ao mesmo cliente dos demais selecionados.

Se o projeto usa customerStatus em DTO, não usar como fonte de verdade no domínio.
Usar status real do Order.

Erros sugeridos:
- DELIVERY_BATCH_ORDER_NOT_FOUND
- DELIVERY_BATCH_ORDER_NOT_PAID
- DELIVERY_BATCH_ORDER_ALREADY_SHIPPED
- DELIVERY_BATCH_ORDER_ALREADY_DELIVERED
- DELIVERY_BATCH_ORDER_ALREADY_IN_BATCH
- DELIVERY_BATCH_ORDER_NOT_ELIGIBLE

Regra:
- criação da batch deve ser all-or-nothing.
- se qualquer pedido for inválido, não criar batch.

==================================================
5. ENDEREÇOS DIFERENTES
==================================================

Pedidos do mesmo cliente podem ter endereços diferentes.

Regra recomendada:
- backend deve detectar se os endereços de entrega dos pedidos selecionados são diferentes;
- por padrão, não bloquear de forma absoluta;
- mas exigir confirmação explícita quando houver divergência.

Create request deve aceitar:

confirmDifferentAddresses: boolean

Se houver endereços diferentes e confirmDifferentAddresses=false:
- retornar 409 ProblemDetails;
- code: DELIVERY_BATCH_ADDRESS_MISMATCH;
- incluir detalhe seguro com orderNumbers e resumo dos endereços, se o padrão permitir.

Se confirmDifferentAddresses=true:
- permitir criação da batch.

Critério:
- frontend futuro poderá mostrar alerta:
  "Os pedidos selecionados possuem endereços diferentes. Confirme antes de agrupar."

Não vazar dados sensíveis além do necessário para o admin.

==================================================
6. ENDPOINTS ADMIN
==================================================

Todos os endpoints devem ser protegidos por Backoffice.
Mutations devem respeitar CSRF conforme padrão admin atual.

Criar endpoints:

1. Buscar candidatos de agrupamento a partir de um pedido:

GET /api/admin/orders/{orderId}/delivery-batch-candidates

Retorna:
- pedido base;
- cliente identificado;
- pedidos pagos e aguardando envio do mesmo cliente;
- exclui pedidos já em batch;
- inclui o próprio pedido base, se elegível;
- indica se há endereços diferentes.

DTO sugerido:

{
  "baseOrderId": "...",
  "customer": {
    "customerUserId": "...",
    "name": "...",
    "email": "...",
    "phone": "..."
  },
  "hasDifferentDeliveryAddresses": false,
  "orders": [
    {
      "orderId": "...",
      "orderNumber": "10012",
      "createdAt": "...",
      "total": 123.45,
      "fulfillmentStatus": "AwaitingShipment",
      "preferredDeliveryMethod": "Carrier",
      "preferredDeliveryDate": "2026-08-03",
      "addressSummary": "São Paulo/SP - CEP 02310-000"
    }
  ]
}

2. Criar batch:

POST /api/admin/delivery-batches

Payload:

{
  "orderIds": ["..."],
  "deliveryMethod": "Carrier",
  "trackingCode": "ABC123",
  "internalNote": "Enviar juntos pela transportadora",
  "confirmDifferentAddresses": false
}

Regras:
- orderIds obrigatório;
- mínimo 2 pedidos recomendado.
- Se quiser permitir batch com 1 pedido por consistência operacional, documentar. Recomendação: exigir mínimo 2.
- deliveryMethod opcional no create;
- trackingCode opcional max 120;
- internalNote opcional max 2000;
- não marcar como enviado automaticamente ao criar, salvo se o projeto decidir. Recomendação: criar como AwaitingShipment.

Resposta:
- DeliveryBatchDetailDto.

3. Listar batches:

GET /api/admin/delivery-batches

Filtros:
- page
- pageSize
- status
- q
- customerEmail
- createdFrom
- createdTo
- sort

Sorts mínimos:
- createdAt_desc
- createdAt_asc

q deve buscar:
- batchNumber;
- customerName;
- customerEmail;
- customerPhone;
- orderNumber, se viável sem N+1.

Paginação server-side igual padrão Admin Products/Orders.

4. Detalhe da batch:

GET /api/admin/delivery-batches/{id}

Retorna:
- dados da batch;
- pedidos vinculados;
- totais;
- cliente;
- status;
- endereço/resumo;
- notas.

5. Marcar batch como enviada:

POST /api/admin/delivery-batches/{id}/ship

Payload:

{
  "deliveryMethod": "Carrier",
  "trackingCode": "ABC123",
  "internalNote": "Enviado pela transportadora"
}

Regras:
- batch precisa estar AwaitingShipment ou Shipped idempotente se quiser atualizar tracking.
- todos os pedidos precisam ainda estar elegíveis para envio.
- atualizar batch:
  - Status = Shipped
  - DeliveryMethod
  - TrackingCode
  - InternalNote, se informado
  - ShippedAt
  - UpdatedAt
  - UpdatedByAdminId

- atualizar todos os pedidos:
  - MarkAsShipped(...)
  - FinalDeliveryMethod
  - TrackingCode
  - ShippedAt
  - FulfillmentUpdatedAt
  - FulfillmentUpdatedByAdminId

Resposta:
- DeliveryBatchDetailDto atualizado.

6. Marcar batch como entregue:

POST /api/admin/delivery-batches/{id}/deliver

Payload:

{
  "internalNote": "Cliente confirmou recebimento"
}

Regras:
- batch precisa estar Shipped.
- todos os pedidos precisam estar Shipped.
- atualizar batch:
  - Status = Delivered
  - InternalNote, se informado
  - DeliveredAt
  - UpdatedAt
  - UpdatedByAdminId

- atualizar todos os pedidos:
  - MarkAsDelivered(...)

Resposta:
- DeliveryBatchDetailDto atualizado.

7. Atualizar nota interna da batch:

PUT /api/admin/delivery-batches/{id}/internal-note

Payload:

{
  "internalNote": "Segurar até sexta"
}

Resposta:
- DeliveryBatchDetailDto atualizado ou 204.
Recomendação:
- retornar detail atualizado.

==================================================
7. DTOs
==================================================

Criar DTOs admin:

DeliveryBatchListItemDto:
- id
- batchNumber
- status
- customerName
- customerEmail
- customerPhone
- orderCount
- totalAmount
- deliveryMethod
- trackingCode
- createdAt
- shippedAt
- deliveredAt
- hasDifferentDeliveryAddresses

DeliveryBatchDetailDto:
- id
- batchNumber
- status
- customer
- orderCount
- totalAmount
- deliveryMethod
- trackingCode
- internalNote
- createdAt
- createdByAdminId
- updatedAt
- updatedByAdminId
- shippedAt
- deliveredAt
- hasDifferentDeliveryAddresses
- orders: [
   {
     orderId
     orderNumber
     createdAt
     total
     status
     paymentStatus
     fulfillmentStatus
     preferredDeliveryMethod
     preferredDeliveryDate
     customerOrderNote
     addressSummary
   }
 ]

DeliveryBatchCandidatesDto:
- baseOrderId
- customer
- hasDifferentDeliveryAddresses
- orders[]

Não expor esses endpoints fora do admin nesta fase.

Customer/guest não precisam conhecer DeliveryBatch agora.
Eles verão entrega através dos campos já existentes do Order.

==================================================
8. PROBLEM DETAILS / CODES
==================================================

Usar padrão atual de ProblemDetails.

Codes sugeridos:
- DELIVERY_BATCH_ORDER_IDS_REQUIRED
- DELIVERY_BATCH_MIN_ORDERS_REQUIRED
- DELIVERY_BATCH_ORDER_NOT_FOUND
- DELIVERY_BATCH_ORDER_NOT_PAID
- DELIVERY_BATCH_ORDER_NOT_ELIGIBLE
- DELIVERY_BATCH_ORDER_ALREADY_IN_BATCH
- DELIVERY_BATCH_CUSTOMER_MISMATCH
- DELIVERY_BATCH_CUSTOMER_IDENTITY_REQUIRED
- DELIVERY_BATCH_ADDRESS_MISMATCH
- DELIVERY_BATCH_CANNOT_BE_SHIPPED
- DELIVERY_BATCH_CANNOT_BE_DELIVERED
- DELIVERY_BATCH_MUST_BE_SHIPPED_BEFORE_DELIVERED
- DELIVERY_BATCH_ALREADY_DELIVERED
- TRACKING_CODE_TOO_LONG
- INTERNAL_NOTE_TOO_LONG
- INVALID_DELIVERY_METHOD

Mensagens PT-BR:
- "Selecione pelo menos dois pedidos para criar uma entrega agrupada."
- "Todos os pedidos selecionados precisam pertencer ao mesmo cliente."
- "Todos os pedidos precisam estar pagos e aguardando envio."
- "Um ou mais pedidos já pertencem a uma entrega agrupada."
- "Os pedidos selecionados possuem endereços diferentes. Confirme antes de agrupar."
- "Esta entrega agrupada não pode ser marcada como enviada."
- "A entrega agrupada precisa estar marcada como enviada antes de ser entregue."
- "A observação interna deve ter no máximo 2000 caracteres."
- "O código/rastreamento deve ter no máximo 120 caracteres."

==================================================
9. CONSISTÊNCIA E TRANSAÇÃO
==================================================

Operações críticas devem ser transacionais:

- criar batch + vincular pedidos;
- ship batch + atualizar todos os pedidos;
- deliver batch + atualizar todos os pedidos.

Regras:
- não permitir atualização parcial.
- se um pedido falhar, rollback total.
- usar padrão de UnitOfWork/DbContext transaction existente.

Concorrência:
- considerar cenário onde dois admins tentam agrupar/enviar os mesmos pedidos.
- índice único em OrderId no vínculo ajuda a impedir duplicidade.
- tratar DbUpdateException com erro amigável, se necessário.

==================================================
10. INTEGRAÇÃO COM ADMIN ORDERS
==================================================

Atualizar AdminOrderDetailDto/ListItemDto, se necessário, para informar vínculo com batch:

Campos sugeridos:
- deliveryBatchId nullable
- deliveryBatchNumber nullable

Somente admin precisa disso.

Objetivo:
- frontend futuro mostra:
  "Este pedido está na Remessa #30001"

Não expor deliveryBatchId para customer/guest nesta fase.

==================================================
11. TESTES UNITÁRIOS
==================================================

Criar/ajustar testes:

Domínio/serviço:
1. cria batch com dois pedidos pagos e AwaitingShipment do mesmo customerUserId.
2. cria batch com pedidos guest do mesmo email+telefone.
3. rejeita pedidos de customerUserId diferente.
4. rejeita guest com email diferente.
5. rejeita agrupamento apenas por nome.
6. rejeita pedido PendingPayment.
7. rejeita pedido Canceled.
8. rejeita pedido Expired.
9. rejeita pedido já Shipped.
10. rejeita pedido já Delivered.
11. rejeita pedido já em outra batch.
12. exige mínimo 2 pedidos.
13. detecta endereços diferentes.
14. endereços diferentes sem confirmação retorna erro.
15. endereços diferentes com confirmação cria batch.
16. trackingCode acima de 120 retorna erro.
17. internalNote acima de 2000 retorna erro.
18. batch criada fica AwaitingShipment.
19. ship batch atualiza batch para Shipped.
20. ship batch atualiza todos os pedidos para Shipped.
21. ship batch salva trackingCode e deliveryMethod.
22. ship já Shipped é idempotente ou atualiza tracking conforme decisão.
23. deliver batch Shipped atualiza batch para Delivered.
24. deliver batch atualiza todos os pedidos para Delivered.
25. deliver batch AwaitingShipment retorna erro.
26. delivered batch não volta para shipped.
27. operação de ship/deliver é all-or-nothing.
28. internal note da batch não sobrescreve internal note individual do pedido, salvo se decisão contrária for documentada.

Queries/DTOs:
29. candidates retorna apenas pedidos elegíveis.
30. candidates exclui pedidos já em batch.
31. candidates inclui base order se elegível.
32. list pagina corretamente.
33. list filtra por status.
34. list busca por batchNumber/customer/orderNumber.
35. detail retorna pedidos vinculados.
36. admin order detail inclui deliveryBatchId/batchNumber.
37. customer/guest DTO não expõe deliveryBatch.

==================================================
12. TESTES HTTP / INTEGRATION
==================================================

Se houver padrão de testes HTTP:

1. GET /api/admin/orders/{id}/delivery-batch-candidates exige Backoffice.
2. POST /api/admin/delivery-batches exige Backoffice + CSRF.
3. GET /api/admin/delivery-batches exige Backoffice.
4. GET /api/admin/delivery-batches/{id} exige Backoffice.
5. POST /api/admin/delivery-batches/{id}/ship exige Backoffice + CSRF.
6. POST /api/admin/delivery-batches/{id}/deliver exige Backoffice + CSRF.
7. PUT /api/admin/delivery-batches/{id}/internal-note exige Backoffice + CSRF.
8. create batch retorna 201/200 com detail.
9. create batch com pedidos inválidos retorna ProblemDetails.
10. ship batch atualiza orders.
11. deliver batch atualiza orders.
12. customer/guest endpoints não retornam batch internamente.

Se não houver suite HTTP adequada:
- criar testes unitários/handler;
- documentar que HTTP/integration não foi criado.

==================================================
13. DOCUMENTAÇÃO
==================================================

Atualizar/criar:

- docs/orders/delivery-batch-phase-3.md
- docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md
- docs/orders/delivery-fulfillment-phase-2.md
- docs/orders/admin-orders.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md
- apps/web/docs/ai-context/api-contracts.md, se o monorepo tiver acesso/espelho do contrato frontend

Documentar:
- conceito DeliveryBatch;
- critérios de elegibilidade;
- critério de mesmo cliente;
- não agrupar apenas por nome;
- tratamento de endereço diferente;
- endpoints admin;
- DTOs;
- transições AwaitingShipment → Shipped → Delivered;
- customer/guest não veem batch;
- Fase 4 frontend será seleção/agrupar/marcar em massa;
- WhatsApp/chat continuam fora.

==================================================
14. NÃO FAZER
==================================================

Não implementar:
- frontend;
- bulk UI;
- seleção em massa;
- chat;
- WhatsApp;
- rastreamento automático;
- cálculo de frete;
- integração Correios/transportadora;
- status avançados;
- feriados;
- cancelamento de batch;
- reabrir batch entregue;
- desvincular pedido da batch, salvo se for trivial e seguro, mas não é obrigatório nesta fase.

Não alterar:
- Pix;
- PaymentsPix;
- Inventory;
- Catalog;
- Product;
- SalesRules;
- Checkout items/preço;
- Customer auth;
- Admin auth.

Não usar:
- OrderStatus para representar envio;
- PaymentStatus para representar envio;
- dados fake;
- endpoint público para admin.

==================================================
15. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Nome final escolhido: DeliveryBatch ou ShipmentBatch.
2. Arquivos alterados.
3. Entidades/enums criados.
4. Migration criada.
5. Campos/tabelas criados.
6. Endpoints admin criados.
7. DTOs criados.
8. Regras de elegibilidade implementadas.
9. Regra de mesmo cliente implementada.
10. Regra de endereços diferentes implementada.
11. Como ship batch atualiza pedidos.
12. Como deliver batch atualiza pedidos.
13. Como transações foram tratadas.
14. ProblemDetails/codes criados.
15. Testes unitários criados/alterados.
16. Testes HTTP/integration criados ou justificativa.
17. Resultado dotnet build.
18. Resultado dotnet test afetado.
19. Docs atualizadas.
20. Pendências para frontend Fase 4.

Critérios de aceite:
- admin consegue criar DeliveryBatch com pedidos elegíveis do mesmo cliente;
- pedidos inelegíveis são rejeitados;
- não agrupa pedidos de clientes diferentes;
- detecta endereços diferentes e exige confirmação;
- batch enviada marca todos os pedidos como enviados;
- batch entregue marca todos os pedidos como entregues;
- operação é transacional;
- pedido não entra em duas batches;
- customer/guest não recebem dados internos da batch;
- build/testes passam.