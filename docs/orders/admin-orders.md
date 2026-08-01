# Admin Orders (MVP)

Backend mínimo para o lojista listar e abrir pedidos no Backoffice.

## Endpoints

| Método | Rota | Auth |
|--------|------|------|
| GET | `/api/admin/orders` | Backoffice (cookie admin + role Owner + `is_staff`) |
| GET | `/api/admin/orders/{orderId}` | Backoffice |
| POST | `/api/admin/orders/{orderId}/fulfillment/ship` | Backoffice + CSRF |
| POST | `/api/admin/orders/{orderId}/fulfillment/deliver` | Backoffice + CSRF |
| PUT | `/api/admin/orders/{orderId}/internal-note` | Backoffice + CSRF |

GET não exige CSRF (padrão do projeto). Mutations cookie-admin exigem CSRF (`GET /api/auth/csrf` → header `X-CSRF-TOKEN`).

Detalhe inclui `items[].salesDisplay` (snapshot de lote/pacote; null em Unit/pedidos antigos). Ver `docs/orders/order-item-sales-snapshot.md`.

Fulfillment (envio/entrega): ver `docs/orders/delivery-fulfillment-phase-2.md`.  
Remessa agrupada: ver `docs/orders/delivery-batch-phase-3.md` (`GET .../delivery-batch-candidates`, `/api/admin/delivery-batches`).

List/detail podem incluir `deliveryBatchId` / `deliveryBatchNumber` quando o pedido está em uma remessa.

## Listagem — query params

| Param | Default | Notas |
|-------|---------|--------|
| `page` | 1 | ≥ 1 |
| `pageSize` | 20 | 1–100 |
| `status` | — | `PendingPayment` \| `Paid` \| `Canceled` \| `Expired` |
| `paymentStatus` | — | Pix: `Pending` \| `Paid` \| `Canceled` \| `Expired` \| `Failed` (último Pix por order) |
| `fulfillmentStatus` | — | `AwaitingShipment` \| `Shipped` \| `Delivered` |
| `q` | — | e-mail, nome, telefone (contains), Guid do pedido ou `orderNumber` (ex. `10582` / `#10582`) |
| `createdFrom` / `createdTo` | — | `DateTimeOffset`; `from ≤ to` |
| `paidOnly` | — | bool |
| `sort` | `createdAt_desc` | também `createdAt_asc` |

List/detail incluem `orderNumber` (string amigável gerada na criação do pedido).

Listagem também expõe `fulfillmentStatus`, preferências de entrega, `shippedAt` / `deliveredAt` / `trackingCode`.
Detalhe inclui `customerOrderNote`, `internalOrderNote`, método final e auditoria de fulfillment.

Filtro operacional típico (pendentes de envio): `status=Paid&fulfillmentStatus=AwaitingShipment`.

## Pagamento Pix (resumo seguro)

Quando existir `PixPayment` para o pedido, usa o **mais recente** (`CreatedAt` desc).

Inclui: id, provider, status, provider order/payment/transaction ids e status fields, `paidAt`, `expiresAt`.

**Omitido de propósito:** `CopyPasteCode`, `QrCode`, `QrCodeImageUrl`, `TicketUrl`, guest access token/hash, webhook raw, `x-signature`, AccessToken, WebhookSecret.

## Exemplo curl

```bash
# Após login admin (cookies + CSRF cookie no jar)
curl -sS -b cookies.txt \
  'https://api.example/api/admin/orders?page=1&pageSize=20&status=Paid&fulfillmentStatus=AwaitingShipment'

TOKEN=$(curl -sS -b cookies.txt -c cookies.txt https://api.example/api/auth/csrf | jq -r .token)

curl -sS -b cookies.txt -H "X-CSRF-TOKEN: $TOKEN" -H 'Content-Type: application/json' \
  -d '{"finalDeliveryMethod":"Carrier","trackingCode":"ABC123"}' \
  "https://api.example/api/admin/orders/{orderId}/fulfillment/ship"
```

## Diferença Customer

Customer orders (`/api/customer/orders`) exigem CustomerCookie e só listam pedidos com `CustomerUserId` do autenticado — nunca por e-mail. Ver `docs/orders/customer-orders.md`.
`internalOrderNote` **nunca** aparece em customer/guest.

Fulfillment (envio/entrega): ver `docs/orders/delivery-fulfillment-phase-2.md`.  
Remessa agrupada: ver `docs/orders/delivery-batch-phase-3.md` (`GET .../delivery-batch-candidates`, `/api/admin/delivery-batches`).

List/detail podem incluir `deliveryBatchId` / `deliveryBatchNumber` quando o pedido está em uma remessa.

## Pós-MVP (não neste escopo)

- Frontend admin de fulfillment / remessa
- Cancelamento e reembolso
- Nota fiscal
- Timeline de eventos
- Exportação CSV / relatórios
