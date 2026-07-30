# Delivery / Fulfillment (Fase 2 — backend)

Preferência de entrega no checkout + status logístico separado de `OrderStatus` / Pix.

Design: [`docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md`](../architecture/DELIVERY-FULFILLMENT-DESIGN.md).

## Enums (API)

| Enum | Valores |
|------|---------|
| `DeliveryMethod` | `Carrier` (Transportadora), `ExcursionBus` (Ônibus), `Correios` |
| `FulfillmentStatus` | `AwaitingShipment` → `Shipped` → `Delivered` |

## Checkout — `POST /api/checkout/sessions`

Campos **opcionais** (retrocompatível):

```json
{
  "preferredDeliveryMethod": "Carrier",
  "preferredDeliveryDate": "2026-08-03",
  "customerOrderNote": "Enviar junto com pedido anterior"
}
```

### Data preferida — mínimo 2 dias úteis

Âncora MVP: data UTC do dia da criação da sessão (`DateOnly` de `DateTime.UtcNow`).

- Segunda → quarta; sexta → terça; sábado/domingo → terça.
- Dias úteis = seg–sex (sem feriados).
- Code: `DELIVERY_DATE_TOO_SOON`

Na criação do pedido, os três campos são copiados da `CheckoutSession`.

## Order — campos

| Campo | Notas |
|-------|--------|
| `PreferredDeliveryMethod` / `PreferredDeliveryDate` / `CustomerOrderNote` | Preferência do cliente |
| `InternalOrderNote` | **Só admin** (max 2000) |
| `FulfillmentStatus` | Default `AwaitingShipment` (incl. pedidos antigos via migration) |
| `FinalDeliveryMethod` / `TrackingCode` / `ShippedAt` / `DeliveredAt` | Preenchidos no envio/entrega |
| `FulfillmentUpdatedAt` / `FulfillmentUpdatedByAdminId` | Auditoria admin |

## Admin endpoints (Backoffice + CSRF)

| Método | Rota |
|--------|------|
| POST | `/api/admin/orders/{orderId}/fulfillment/ship` |
| POST | `/api/admin/orders/{orderId}/fulfillment/deliver` |
| PUT | `/api/admin/orders/{orderId}/internal-note` |
| GET | `/api/admin/orders?fulfillmentStatus=AwaitingShipment` |

Ship exige `OrderStatus.Paid`. Deliver exige `FulfillmentStatus.Shipped` (code `ORDER_MUST_BE_SHIPPED_BEFORE_DELIVERED`). Ship já `Shipped` é idempotente (atualiza tracking/método). Deliver já `Delivered` é idempotente.

## DTOs

- **Admin** detail: inclui `internalOrderNote` + todos os campos de fulfillment.
- **Customer / Guest**: objeto `delivery` (`OrderDeliveryInfoDto`) **sem** `internalOrderNote` / `fulfillmentUpdatedByAdminId`.

## Fora desta fase

DeliveryBatch, bulk, WhatsApp, chat, frete, feriados, frontend.
