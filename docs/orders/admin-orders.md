# Admin Orders (MVP)

Backend mínimo para o lojista listar e abrir pedidos no Backoffice.

## Endpoints

| Método | Rota | Auth |
|--------|------|------|
| GET | `/api/admin/orders` | Backoffice (cookie admin + role Owner + `is_staff`) |
| GET | `/api/admin/orders/{orderId}` | Backoffice |

GET não exige CSRF (padrão do projeto).

## Listagem — query params

| Param | Default | Notas |
|-------|---------|--------|
| `page` | 1 | ≥ 1 |
| `pageSize` | 20 | 1–100 |
| `status` | — | `PendingPayment` \| `Paid` \| `Canceled` \| `Expired` |
| `paymentStatus` | — | Pix: `Pending` \| `Paid` \| `Canceled` \| `Expired` \| `Failed` (último Pix por order) |
| `q` | — | e-mail, nome, telefone (contains), Guid do pedido ou `orderNumber` (ex. `10582` / `#10582`) |

List/detail incluem `orderNumber` (string amigável gerada na criação do pedido).
| `createdFrom` / `createdTo` | — | `DateTimeOffset`; `from ≤ to` |
| `paidOnly` | — | bool |
| `sort` | `createdAt_desc` | também `createdAt_asc` |

## Pagamento Pix (resumo seguro)

Quando existir `PixPayment` para o pedido, usa o **mais recente** (`CreatedAt` desc).

Inclui: id, provider, status, provider order/payment/transaction ids e status fields, `paidAt`, `expiresAt`.

**Omitido de propósito:** `CopyPasteCode`, `QrCode`, `QrCodeImageUrl`, `TicketUrl`, guest access token/hash, webhook raw, `x-signature`, AccessToken, WebhookSecret.

## Exemplo curl

```bash
# Após login admin (cookies + CSRF cookie no jar)
curl -sS -b cookies.txt \
  'https://api.example/api/admin/orders?page=1&pageSize=20&status=Paid'

curl -sS -b cookies.txt \
  "https://api.example/api/admin/orders/{orderId}"
```

## Diferença Customer

Customer orders (`/api/customer/orders`) exigem CustomerCookie e só listam pedidos com `CustomerUserId` do autenticado — nunca por e-mail. Ver `docs/orders/customer-orders.md`.

## Pós-MVP (não neste escopo)

- ~~Frontend admin~~ → `apps/web` (`/admin/orders`)
- Customer orders
- Envio / fulfillment / rastreio
- Cancelamento e reembolso
- Nota fiscal
- Timeline de eventos
- Exportação CSV / relatórios
