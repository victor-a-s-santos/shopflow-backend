# Customer Orders (MVP)

Área logada “Meus pedidos” — somente pedidos vinculados a `Order.CustomerUserId`.

## Endpoints

| Método | Rota | Auth |
|--------|------|------|
| GET | `/api/customer/orders` | `Customer` (CustomerCookie) |
| GET | `/api/customer/orders/{orderId}` | `Customer` |

- Sem login → 401  
- Admin Backoffice cookie **não** autoriza  
- GuestOrderAccessToken **não** autoriza  

## Guest vs logado

| Situação | `CustomerUserId` | Aparece em `/api/customer/orders`? |
|----------|------------------|-------------------------------------|
| Checkout guest | `null` | Não |
| Checkout com CustomerCookie válido | Guid do customer | Sim |
| Pedido de outro customer | outro Guid | Não |

**Não** buscar pedido por e-mail — evita vazar pedido guest/outro usuário com o mesmo e-mail.

Associação ocorre em `POST /api/orders/from-checkout-session` (continua público): se houver CustomerCookie, grava `CustomerUserId`; caso contrário permanece guest + GuestOrderAccessToken.

Claim/importação de pedido guest = pós-MVP.

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

- Frontend customer orders  
- Claim de pedido guest  
- Segunda via Pix / reabrir pagamento  
- Cancelamento / rastreio / timeline / e-mail  
