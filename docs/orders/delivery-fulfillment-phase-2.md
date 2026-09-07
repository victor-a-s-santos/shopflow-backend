# Delivery / Fulfillment (Fase 2 — backend)

Preferência de entrega no checkout + status logístico separado de `OrderStatus` / Pix.

Design: [`docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md`](../architecture/DELIVERY-FULFILLMENT-DESIGN.md).

## Enums (API)

| Enum | Valores |
|------|---------|
| `DeliveryMethod` | `Carrier` (Transportadora), `ExcursionBus` (Ônibus), `Correios` |
| `FulfillmentStatus` | `AwaitingShipment` → `Shipped` → `Delivered` (sem rename) |

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
| `StockConfirmedAt` / `StockConfirmedByAdminUserId` / `StockConfirmationNote` / `StockConfirmationUpdatedAt` | Confirmação **física** no fornecedor. Distinto da disponibilidade do site. |

## Admin endpoints (Backoffice + CSRF)

| Método | Rota |
|--------|------|
| POST | `/api/admin/orders/{orderId}/fulfillment/confirm-stock` |
| POST | `/api/admin/orders/{orderId}/fulfillment/ship` |
| POST | `/api/admin/orders/{orderId}/fulfillment/deliver` |
| PUT | `/api/admin/orders/{orderId}/internal-note` |
| GET | `/api/admin/orders?fulfillmentStatus=AwaitingShipment` |

## Confirmação de estoque físico (VIP)

Disponibilidade do site **não** é “Em estoque”. Para VIP Assessoria o admin confirma a peça no fornecedor antes de separar.

Interpretação (labels de UI; enums internos inalterados):

| Estado interno | Label operacional |
|----------------|-------------------|
| Paid + `AwaitingShipment` + `StockConfirmedAt` null | Aguardando confirmação de estoque |
| Paid + `AwaitingShipment` + `StockConfirmedAt` preenchido | Em estoque |
| `Shipped` | Separado |
| `Delivered` | Entregue |

Config: `Fulfillment:RequireStockConfirmation` (`Fulfillment__RequireStockConfirmation`).

- **Este cliente (TESTE/HML/PROD):** `true` em `appsettings.json`.
- **Default do options class:** `false` — lojas futuras com fluxo tradicional (Paid → Separado) não quebram se a seção estiver ausente.

Quando `true`, `POST .../fulfillment/ship` exige `StockConfirmedAt` (409 `ORDER_STOCK_CONFIRMATION_REQUIRED`: “Confirme o estoque antes de marcar o pedido como separado.”). Quando `false`, o fluxo antigo continua.

`confirm-stock` exige pedido `Paid`. Já confirmado é idempotente (não duplica e-mail). `Shipped` sem confirmação preenche para compatibilidade. `Delivered` sem confirmação → 409 `ORDER_CANNOT_CONFIRM_STOCK_AFTER_DELIVERED`. Não pago → 409 `ORDER_MUST_BE_PAID_BEFORE_STOCK_CONFIRMATION`.

Migration `AddOrderStockConfirmation` backfill: pedidos já `Shipped`/`Delivered` recebem `StockConfirmedAt = COALESCE(ShippedAt, FulfillmentUpdatedAt)`. Paid + AwaitingShipment permanecem sem confirmação.

Ship exige `OrderStatus.Paid`. Deliver exige `FulfillmentStatus.Shipped` (code `ORDER_MUST_BE_SHIPPED_BEFORE_DELIVERED`). Ship já `Shipped` é idempotente (atualiza tracking/método). Deliver já `Delivered` é idempotente.

## DTOs

- **Admin** detail: inclui `internalOrderNote` + fulfillment + `stockConfirmedAt` / `stockConfirmedByAdminUserId` / `stockConfirmationNote` + `canConfirmStock` / `canMarkAsSeparated`.
- **Customer / Guest**: objeto `delivery` (`OrderDeliveryInfoDto`) com `stockConfirmedAt` para timeline; **sem** `internalOrderNote`, `fulfillmentUpdatedByAdminId`, admin id de estoque ou nota interna.

## Fora desta fase

~~DeliveryBatch~~ → [`delivery-batch-phase-3.md`](./delivery-batch-phase-3.md). Ainda fora: bulk UI/frontend, WhatsApp, chat, frete, feriados.
