# SEC-006 — Guest Order Access Token

Token de acesso público limitado para pedidos de checkout convidado.

## Objetivo

Permitir que o frontend consulte o **status limitado** de um pedido sem login e sem usar endpoints Backoffice (`GET /api/orders/{id}`, `GET /api/payments/pix/...`).

## Geração

- Momento: `POST /api/orders/from-checkout-session` (pedido criado com sucesso), **somente** se guest checkout estiver permitido e o pedido não tiver `CustomerUserId`. Loja Closed / `AllowGuest=false` não emite token em pedidos novos. Tracking legado permanece (`docs/orders/guest-order-access.md`).
- Token bruto: 256 bits via `RandomNumberGenerator`, encoding **Base64Url**.
- Persistência: **apenas** HMAC-SHA256 hex (`GuestOrderAccess__TokenHashSecret`).
- O token bruto retorna **uma única vez** nos campos:
  - `guestAccessToken`
  - `guestAccessTokenExpiresAt`
- Idempotência: se o pedido já existe → **409** (sem reemitir token). O frontend deve guardar o token da primeira resposta `201`.
- GETs Backoffice **não** retornam `guestAccessToken`.

## Consulta pública

```
GET /api/orders/guest/{orderId}/status
Header: X-ORDER-ACCESS-TOKEN: <token>
```

- Público, sem cookie, sem CSRF (GET), sem Customer/Backoffice auth.
- Rate limit: `GuestOrderAccess__RateLimitPerMinute` (Dev ≥ 100/min).
- Token inválido/ausente/expirado/revogado → **401** com mensagem opaca `"Order access denied."` (sem enumerar existência do pedido).

## O que o DTO retorna

- `orderId`, `orderNumber`, `orderStatus`
- `payment` (status/provider/amount/expiresAt/paidAt/updatedAt) — sem ProviderOrderId/ProviderTransactionId
- `items` (nome, skuId, qty, preços; attributes/imageUrl null no snapshot atual)
- `totals`
- `customer` mascarado (`Vi***`, `v***@gmail.com`)
- `access.expiresAt` / `access.lastUsedAt`

## O que nunca retorna

- Endereço, telefone, documento
- TokenHash / guestAccessToken
- ProviderOrderId / ProviderTransactionId
- Payload bruto do gateway

## Configuração

```bash
GuestOrderAccess__Enabled=true
GuestOrderAccess__TokenTtlDays=30
GuestOrderAccess__TokenHashSecret=<secret-forte>
GuestOrderAccess__RateLimitPerMinute=30
```

Em HML/Production, `TokenHashSecret` é obrigatório quando Enabled=true.

## Frontend (próxima etapa)

1. Guardar `guestAccessToken` da criação do pedido (ex.: sessionStorage junto do Pix).
2. Polling/consulta em `GET /api/orders/guest/{orderId}/status` **ou** `GET /api/orders/public/{orderNumber}` com o header.
3. Hidratar `?t=` do e-mail em `/pedido/:orderNumber` (EMAIL-002). Não deixar o token só na query da API.
4. Mapear `orderStatus` + `payment.status` para UI (aguardando / aprovado / expirado / cancelado).

## Limitações

- Sem reenvio de token.
- Sem attributes/image no snapshot do Order (ainda).
- Token no link de e-mail usa query `t` na **loja**; a API prefere header (query `t`/`token` aceita para deep-link/teste).
