# Customer Orders (MVP)

Área logada “Meus pedidos” — somente pedidos vinculados a `Order.CustomerUserId`.

## Endpoints

| Método | Rota | Auth |
|--------|------|------|
| GET | `/api/customer/orders` | `Customer` (CustomerCookie) |
| GET | `/api/customer/orders/{orderId}` | `Customer` |
| POST | `/api/customer/orders/guest/{orderId}/create-account` | Anônimo + GuestOrderAccessToken (body) |
| POST | `/api/customer/orders/guest/{orderId}/claim` | `Customer` + GuestOrderAccessToken (body) |

List/detail incluem `orderNumber` (string amigável). Detalhe inclui `items[].salesDisplay` (snapshot lote/pacote). Ver [`order-item-sales-snapshot.md`](./order-item-sales-snapshot.md), fluxo guest: [`post-pix-guest-flow.md`](./post-pix-guest-flow.md), [`guest-order-claim.md`](./guest-order-claim.md).

- Sem login nas GETs → 401  
- Admin Backoffice cookie **não** autoriza GETs  
- GuestOrderAccessToken **sozinho** não autoriza listagem — só claim/create-account  

## Guest vs logado

| Situação | `CustomerUserId` | Aparece em `/api/customer/orders`? |
|----------|------------------|-------------------------------------|
| Checkout guest | `null` | Não |
| Checkout com CustomerCookie válido | Guid do customer | Sim |
| Guest após claim/create-account | Guid do customer | Sim |
| Pedido de outro customer | outro Guid | Não |

**Não** buscar pedido por e-mail — evita vazar pedido guest/outro usuário com o mesmo e-mail.

Associação no create: `POST /api/orders/from-checkout-session` grava `CustomerUserId` se houver CustomerCookie.  
Associação pós-Pix (guest): ver `docs/orders/guest-order-claim.md`.

## Listagem — query params

| Param | Default | Limite |
|-------|---------|--------|
| `page` | 1 | ≥ 1 |
| `pageSize` | 10 | 1–50 |
| `status` | — | OrderStatus |
| `paymentStatus` | — | PixPaymentStatus (último Pix) |
| `createdFrom` / `createdTo` | — | from ≤ to |
| `sort` | `createdAt_desc` | ou `createdAt_asc` |

## Pagamento (resumo seguro)

Somente: `status`, `provider`, `paidAt`, `expiresAt`.

**Omitido:** providerOrderId/PaymentId/TransactionId, QR, copia-e-cola, ticketUrl, tokens, secrets.

Detalhe inexistente / de outro customer / guest → **404** (não 403).

## Diferença vs Admin

Ver `docs/orders/admin-orders.md` — admin vê PII + IDs Mercado Pago; customer não.

## Pós-MVP

- Frontend customer orders + wiring do claim  
- Segunda via Pix / reabrir pagamento  
- Cancelamento / rastreio / timeline / e-mail transacional  

