# Guest order access (legado)

Tracking público de pedidos **não foi removido**.

- `GET /api/orders/public/{orderNumber}` (`t` / `token` / `X-ORDER-ACCESS-TOKEN`)
- `GET /api/orders/guest/{orderId}/status`
- claim pós-Pix (`docs/orders/guest-order-claim.md`, EMAIL-002)

Pedidos antigos sem `CustomerUserId` continuam acessíveis por token.

## Pedidos novos

Em loja `Closed` / `AllowGuest=false`:

- `POST /api/orders/from-checkout-session` exige customer autenticado e aprovado;
- o pedido nasce com `CustomerUserId`;
- `guestAccessToken` **não** é emitido para pedido vinculado a customer nem quando guest checkout está desligado.

A coluna `CustomerUserId` permanece nullable no banco.

Detalhe do token: `docs/security/SEC-006-guest-order-access-token.md`.
