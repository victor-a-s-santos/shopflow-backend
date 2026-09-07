Você está atuando como backend engineer sênior do projeto Shopflow, especialista em .NET, EF Core, Orders, Fulfillment, DeliveryBatch, Notifications/EMAIL-001, Clean Architecture e regras operacionais de e-commerce/assessoria de compras.

Objetivo:
Implementar uma etapa real de confirmação de estoque físico no fluxo do pedido.

Contexto:
A cliente VIP Assessoria trabalha com estoque do fornecedor. Mesmo que o site tenha disponibilidade, ela precisa retirar/confirmar fisicamente a peça com o fornecedor antes de considerar o pedido "Em estoque".

Hoje o fluxo visual está:

Pedido realizado
Pagamento aprovado
Em estoque
Separado
Entregue

Mas "Em estoque" está sendo tratado apenas como label visual automático para AwaitingShipment.

Isso está incorreto para este cliente.

Novo fluxo desejado:

Pedido realizado
Pagamento aprovado
Aguardando confirmação de estoque físico
Admin clica "Confirmar em estoque"
Em estoque
Admin clica "Marcar como separado"
Separado
Admin clica "Marcar como entregue"
Entregue

Importante:
Não renomear enums existentes sem necessidade.
Não quebrar pedidos antigos.
Não quebrar DeliveryBatch.
Não quebrar Brevo/EMAIL-001.
Não quebrar Pix.
Não quebrar StoreAccess.
Não alterar R2.
Não alterar checkout.

==================================================
1. DECISÃO DE MODELAGEM
==================================================

Implementar "Em estoque" como milestone real do pedido, não apenas label.

Recomendação:
Adicionar campos no Order:

- StockConfirmedAt
- StockConfirmedByAdminUserId
- StockConfirmationUpdatedAt, se necessário
- StockConfirmationNote, opcional, se simples

Manter FulfillmentStatus existente se possível:

- AwaitingShipment
- Shipped
- Delivered

Interpretação para VIP:

- AwaitingShipment + StockConfirmedAt null:
  "Aguardando confirmação de estoque"

- AwaitingShipment + StockConfirmedAt preenchido:
  "Em estoque"

- Shipped:
  "Separado"

- Delivered:
  "Entregue"

Assim evitamos quebrar muita coisa e adicionamos a etapa que faltava.

Adicionar configuração:

Fulfillment__RequireStockConfirmation=true

Para este cliente:
- TESTE/HML/PROD: true

Default seguro:
- false ou documentado conforme decisão do projeto, para preservar lojas futuras com fluxo tradicional.

==================================================
2. MIGRATION
==================================================

Criar migration adicionando campos de confirmação de estoque.

Sugestão:

orders.orders:
- StockConfirmedAt nullable timestamp
- StockConfirmedByAdminUserId nullable
- StockConfirmationNote nullable, max 1000 se implementar
- StockConfirmationUpdatedAt nullable, se necessário

Backfill:
- Pedidos já Shipped/Delivered devem receber StockConfirmedAt = ShippedAt ou FulfillmentUpdatedAt, se disponível, para não ficarem inconsistentes visualmente.
- Pedidos Paid + AwaitingShipment podem permanecer sem StockConfirmedAt para exigir confirmação manual, ou seguir regra documentada. Para TESTE, faz sentido exigir confirmação.

Não tornar campos obrigatórios.

==================================================
3. ENDPOINT ADMIN
==================================================

Criar endpoint Backoffice + CSRF:

POST /api/admin/orders/{orderId}/fulfillment/confirm-stock

Payload opcional:

{
  "note": "opcional"
}

Regras:
- exige Backoffice.
- exige CSRF.
- order inexistente -> 404.
- pedido precisa estar Paid.
- se pedido não está pago -> erro ORDER_MUST_BE_PAID.
- se já Delivered -> erro ou idempotente conforme padrão.
- se já Shipped/Separado -> se StockConfirmedAt já existe, idempotente; se não existe, preencher para compatibilidade.
- se AwaitingShipment e StockConfirmedAt null -> preencher StockConfirmedAt e admin id.
- se já confirmado -> idempotente, não duplicar evento/e-mail.

Code sugerido:
- ORDER_STOCK_ALREADY_CONFIRMED
- ORDER_STOCK_CONFIRMATION_REQUIRED
- ORDER_MUST_BE_PAID_BEFORE_STOCK_CONFIRMATION
- ORDER_CANNOT_CONFIRM_STOCK_AFTER_DELIVERED

Mensagem PT-BR:
- "Estoque confirmado."
- "Este pedido já teve o estoque confirmado."
- "O pagamento precisa estar aprovado antes de confirmar o estoque."
- "Confirme o estoque antes de marcar o pedido como separado."

==================================================
4. BLOQUEAR "SEPARADO" ANTES DE CONFIRMAR ESTOQUE
==================================================

Quando Fulfillment__RequireStockConfirmation=true:

Endpoint atual de marcar como enviado/separado:

POST /api/admin/orders/{id}/fulfillment/ship

deve exigir:

- Order.StockConfirmedAt preenchido

Se não estiver preenchido:
- retornar 409 ou 400 com code:
  ORDER_STOCK_CONFIRMATION_REQUIRED

Mensagem:
"Confirme o estoque antes de marcar o pedido como separado."

Quando RequireStockConfirmation=false:
- comportamento antigo continua.

==================================================
5. DTOs
==================================================

Atualizar DTOs seguros de pedido:

Admin Order Detail:
- stockConfirmedAt
- stockConfirmedByAdminUserId, se já expõe ids admin em outros campos
- stockConfirmationNote, se implementado
- canConfirmStock, opcional
- canMarkAsSeparated, opcional

Customer/Guest Order Detail:
- stockConfirmedAt pode ser exposto como dado operacional seguro.
- Não expor admin id.
- Não expor note interna se ela for administrativa.

Delivery/fulfillment safe DTO deve permitir ao frontend montar timeline:

- payment approved
- stock confirmed
- separated
- delivered

Não expor dados internos sensíveis.

==================================================
6. EMAIL-001 / BREVO
==================================================

Como o projeto já possui e-mails de movimentação do pedido, avaliar adicionar e-mail transacional para estoque confirmado.

Evento sugerido:
- OrderStockConfirmed

Outbox idempotency key:
- order:{orderId}:stock-confirmed

Assunto:
Pedido #{orderNumber} confirmado em estoque

Conteúdo:
"Seu pedido foi confirmado em estoque e agora seguirá para separação."

Regras:
- falha de enqueue/e-mail não quebra a ação admin.
- não duplicar e-mail em ação idempotente.
- não incluir nota interna.
- não incluir admin id.
- não incluir dados sensíveis.

Se for decidido não enviar e-mail nesta fase:
- documentar como pendência.
Mas recomendação: implementar, pois é uma movimentação relevante do pedido.

==================================================
7. DELIVERY BATCH
==================================================

Avaliar impacto nas remessas.

Regras:
- DeliveryBatch deve continuar agrupando pedidos pagos e elegíveis.
- Se RequireStockConfirmation=true, pedido só deve ser elegível para remessa/separação se StockConfirmedAt estiver preenchido.
- Não permitir marcar remessa como separada/enviada se pedidos não tiverem estoque confirmado, caso esse fluxo use o mesmo ship interno.
- Se o fluxo atual de batch ship chama MarkAsShipped, ele deve respeitar o mesmo bloqueio.

Mensagem:
"Todos os pedidos da remessa precisam ter estoque confirmado antes de marcar como separado."

==================================================
8. TESTES BACKEND
==================================================

Criar/ajustar testes:

1. pedido Paid + AwaitingShipment + sem StockConfirmedAt permite ConfirmStock.
2. ConfirmStock preenche StockConfirmedAt.
3. ConfirmStock registra admin id.
4. ConfirmStock é idempotente.
5. pedido não pago não permite ConfirmStock.
6. RequireStockConfirmation=true bloqueia Ship antes de ConfirmStock.
7. RequireStockConfirmation=true permite Ship depois de ConfirmStock.
8. RequireStockConfirmation=false preserva fluxo antigo.
9. Delivered não permite confirmação indevida ou trata compatibilidade conforme decisão.
10. DTO admin retorna stockConfirmedAt.
11. DTO customer retorna stockConfirmedAt sem admin id.
12. EMAIL-001 enfileira order:{id}:stock-confirmed, se implementado.
13. ConfirmStock repetido não duplica outbox.
14. Batch não marca separado com pedido sem StockConfirmedAt quando config true.
15. Pedidos antigos Shipped/Delivered ficam compatíveis.

==================================================
9. DOCUMENTAÇÃO
==================================================

Atualizar:

- docs/orders/delivery-fulfillment-phase-2.md
- docs/orders/order-emails.md, se adicionar e-mail
- docs/orders/delivery-batch-phase-3.md, se impactar batch
- docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md

Documentar:
- Fulfillment__RequireStockConfirmation
- novo endpoint confirm-stock
- diferença entre disponibilidade do site e confirmação física no fornecedor
- fluxo VIP:
  Pagamento aprovado → Confirmar em estoque → Separado → Entregue
- status interno permanece compatível
- e-mail de estoque confirmado, se implementado

==================================================
10. NÃO FAZER
==================================================

Não fazer:
- não quebrar pedidos antigos;
- não remover AwaitingShipment/Shipped/Delivered sem necessidade;
- não alterar checkout;
- não alterar Pix;
- não alterar StoreAccess;
- não alterar R2;
- não alterar admin/customer auth;
- não expor nota interna para cliente;
- não duplicar e-mails;
- não depender apenas do frontend para bloquear separação antes da confirmação.

==================================================
11. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Migration criada.
3. Campos adicionados ao Order.
4. Config adicionada.
5. Endpoint confirm-stock criado.
6. Regra que bloqueia separado antes de estoque confirmado.
7. DTOs atualizados.
8. Impacto em DeliveryBatch.
9. EMAIL-001 criado ou decisão documentada.
10. Testes criados/alterados.
11. Resultado dotnet build.
12. Resultado dotnet test.
13. Docs atualizadas.
14. Pendências para frontend.

Critérios de aceite:
- admin consegue confirmar estoque.
- pedido só pode ser marcado como separado após confirmar estoque quando config=true.
- cliente consegue ver a etapa "Em estoque" confirmada no acompanhamento.
- pedidos antigos não quebram.
- e-mails não duplicam.
- DeliveryBatch respeita a regra.
- build/testes passam.