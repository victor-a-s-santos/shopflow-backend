# DeliveryBatch (Fase 3 — backend)

Remessa/entrega agrupada de pedidos pagos e `AwaitingShipment` do mesmo cliente.

Design: [`docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md`](../architecture/DELIVERY-FULFILLMENT-DESIGN.md)  
Fase 2 (por pedido): [`docs/orders/delivery-fulfillment-phase-2.md`](./delivery-fulfillment-phase-2.md)

## Conceito

`DeliveryBatch` agrupa ≥ 2 pedidos elegíveis. Ship/deliver da remessa atualiza **todos** os pedidos vinculados na mesma transação (`SaveChanges` no `OrdersDbContext`).

Nome canônico: **DeliveryBatch** (não ShipmentBatch).

## Tabelas

| Tabela | Notas |
|--------|--------|
| `orders.delivery_batches` | remessa + cliente + status + tracking + nota interna |
| `orders.delivery_batch_orders` | join; **único** em `OrderId` (pedido em no máximo uma remessa) |

`BatchNumber` via sequence `orders.delivery_batches_batch_number_seq` (START 30000).

## Status

`DeliveryBatchStatus`: `AwaitingShipment` → `Shipped` → `Delivered` (separado de `FulfillmentStatus` do pedido).

## Elegibilidade do pedido

- `OrderStatus = Paid`
- `FulfillmentStatus = AwaitingShipment`
- não está em outra batch

## Mesmo cliente

1. Todos com o mesmo `CustomerUserId`, **ou**
2. Guest: mesmo `email` normalizado + `telefone` só dígitos (nunca só nome)

## Endereços diferentes

Detectados por fingerprint do endereço. Sem `confirmDifferentAddresses=true` → 409 `DELIVERY_BATCH_ADDRESS_MISMATCH` (+ resumos admin-safe).

## Endpoints admin (Backoffice + CSRF em mutations)

| Método | Rota |
|--------|------|
| GET | `/api/admin/orders/{orderId}/delivery-batch-candidates` |
| POST | `/api/admin/delivery-batches` |
| GET | `/api/admin/delivery-batches` |
| GET | `/api/admin/delivery-batches/{id}` |
| POST | `/api/admin/delivery-batches/{id}/ship` |
| POST | `/api/admin/delivery-batches/{id}/deliver` |
| PUT | `/api/admin/delivery-batches/{id}/internal-note` |

Create: mínimo 2 pedidos; status inicial `AwaitingShipment`.  
Ship: atualiza batch + `Order.MarkAsShipped(...)` **sem** sobrescrever `InternalOrderNote` do pedido.  
Deliver: exige batch/pedidos shipped; idem `MarkAsDelivered` sem sobrescrever nota do pedido.

## Admin Orders

List/detail incluem `deliveryBatchId` / `deliveryBatchNumber` quando o pedido está em remessa.

Customer/guest **não** recebem dados de DeliveryBatch.

## Fora desta fase

Frontend, bulk UI, cancelar/reabrir batch, WhatsApp/chat, frete, rastreio automático.
