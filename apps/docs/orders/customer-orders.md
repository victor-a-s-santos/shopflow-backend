# Customer Orders — experiência do consumidor

Área logada “Meus pedidos” e acompanhamento guest seguro.

## Endpoints

| Método | Rota | Auth |
|--------|------|------|
| GET | `/api/customer/orders` | `Customer` (CustomerCookie) |
| GET | `/api/customer/orders/{orderId}` | `Customer` |
| GET | `/api/orders/public/{orderNumber}` | Anônimo + GuestOrderAccessToken |
| GET | `/api/orders/guest/{orderId}/status` | Anônimo + GuestOrderAccessToken (legado; preferir `/public`) |
| POST | `/api/customer/orders/guest/{orderId}/create-account` | Anônimo + GuestOrderAccessToken (body) |
| POST | `/api/customer/orders/guest/{orderId}/claim` | `Customer` + GuestOrderAccessToken (body) |

### Consulta pública (`/api/orders/public/{orderNumber}`)

Credencial: header `X-ORDER-ACCESS-TOKEN` (preferencial), query `?t=` (links de e-mail EMAIL-001) ou `?token=` (alias legado).

**Risco da query string:** tokens podem aparecer em logs de proxy, histórico do browser e `Referer`. Mitigações: rate limit por IP, token de alta entropia armazenado só como hash, resposta idêntica para número inexistente e token inválido (`INVALID_GUEST_ORDER_TOKEN`), GUID sozinho **não** autoriza acesso público.

Rate limit: política `guest-order-status` (`GuestOrderAccess:RateLimitPerMinute`).

## Contratos consumer

### Status público (`customerStatus`)

| Código | Quando |
|--------|--------|
| `AwaitingPayment` | Pedido aguardando pagamento |
| `Confirmed` | Pedido ou Pix pagos |
| `Canceled` | Pedido cancelado |
| `Expired` | Pedido ou Pix expirados |

`Preparing` / `Shipped` / `Delivered` **não** são retornados — o domínio ainda não tem logística.

`status` / `orderStatus` continuam expondo o enum de domínio para compatibilidade; o frontend deve preferir `customerStatus`.

### Pagamento (resumo)

| Campo | Regra |
|-------|--------|
| `status` | PixPaymentStatus (`Pending`, `Paid`, …) |
| `method` | Sempre `"Pix"` (nunca nome técnico do provider) |
| `paidAt` | Quando aprovado |
| `expiresAt` | **Somente** se `status == Pending` |

**Omitido:** `provider`, provider IDs, QR, copia-e-cola, secrets.

### Listagem — query params

| Param | Default | Valores |
|-------|---------|---------|
| `page` | 1 | ≥ 1 |
| `pageSize` | 10 | 1–50 |
| `status` | — | `AwaitingPayment`/`Confirmed`/`Canceled`/`Expired` **ou** OrderStatus legado |
| `paymentStatus` | — | PixPaymentStatus |
| `createdFrom` / `createdTo` | — | from ≤ to (inclusive nos limites do read model) |
| `sort` | `createdAt_desc` | ou `createdAt_asc` |

## Guest vs logado

| Situação | `CustomerUserId` | Aparece em `/api/customer/orders`? |
|----------|------------------|-------------------------------------|
| Checkout guest | `null` | Não |
| Checkout com CustomerCookie válido | Guid do customer | Sim |
| Guest após claim/create-account | Guid do customer | Sim |

**Não** buscar pedido por e-mail. Vinculação futura exige prova de titularidade (token / claim).

## OrderNumber

- Sequence Postgres `orders.orders_order_number_seq` (início 10000)
- Migration `20260719111727_AddOrderNumberToOrders` (inclui backfill)
- Imutável; único; indexado

## Diferença vs Admin

Ver [`admin-orders.md`](./admin-orders.md). Admin pode ver provider técnico e IDs Mercado Pago; customer/guest não.

## Fora desta etapa

Cadastro/login completos além do cookie customer já existente, perfil, CRUD de endereços, e-mail transacional, logística/rastreio — ver prompt `docs/prompts/features/customer-orders-and-account-experience.md`.
