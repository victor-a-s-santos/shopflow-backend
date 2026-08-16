# Store access e aprovação de clientes — Fase 1 (backend)

Implementação da Fase 1 do ADR `docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md`.

Não inclui frontend (Fase 2) nem e-mails Brevo de aprovação (Fase 3).

## Configuração

```env
StoreAccess__Mode=PrivateCatalogApprovedOnly
CustomerAccess__RequireApproval=true
Checkout__AllowGuestCheckout=false
```

| Ambiente | Modo | Guest checkout | RequireApproval |
|----------|------|----------------|-----------------|
| `appsettings.json` (TESTE/HML/PROD deste cliente) | `PrivateCatalogApprovedOnly` | `false` | `true` |
| `appsettings.Development.json` / testes de regressão | `PublicCatalogAndGuestCheckout` | `true` | `false` |

Modos: `PublicCatalogAndGuestCheckout`, `PublicCatalogLoginCheckout`, `PublicCatalogApprovedCheckout`, `PrivateCatalogApprovedOnly`. Valor desconhecido falha fechado para `PrivateCatalogApprovedOnly`.

## Contratos

### Política pública

`GET /api/store/access` (anônimo)

```json
{
  "mode": "PrivateCatalogApprovedOnly",
  "allowGuestCheckout": false,
  "requireApprovedCustomerToBrowse": true,
  "requireLoginForCheckout": true,
  "requireApprovedCustomerForCheckout": true
}
```

### Customer DTO (`register`, `login`, `/me`)

```json
{
  "accessStatus": "PendingApproval",
  "accessRequestedAt": "2026-08-16T13:00:00+00:00",
  "approvedAt": null
}
```

Status: `PendingApproval` | `Approved` | `Rejected` | `Suspended`.

Cadastro **não** emite cookie. Login com senha correta emite `CustomerCookie` mesmo se pendente — o frontend usa `accessStatus` para redirecionar. Catálogo privado e checkout continuam bloqueados no backend.

### Codes (ProblemDetails `code`)

| Code | HTTP | Quando |
|------|------|--------|
| `CUSTOMER_LOGIN_REQUIRED` | 401 | Catálogo privado sem sessão customer |
| `GUEST_CHECKOUT_DISABLED` | 401 | Checkout/pedido anônimo com `AllowGuestCheckout=false` |
| `CUSTOMER_ACCESS_NOT_APPROVED` | 403 | Pendente |
| `CUSTOMER_ACCESS_REJECTED` | 403 | Recusado |
| `CUSTOMER_ACCESS_SUSPENDED` | 403 | Suspenso |
| `CUSTOMER_ACCESS_INVALID_TRANSITION` | 409 | Transição admin inválida |

### Admin (Backoffice + CSRF nas mutações)

```
GET  /api/admin/customers?status=&q=&page=&pageSize=
GET  /api/admin/customers/pending-count
GET  /api/admin/customers/{id}
POST /api/admin/customers/{id}/approve
POST /api/admin/customers/{id}/reject
POST /api/admin/customers/{id}/suspend
POST /api/admin/customers/{id}/reactivate
```

Body opcional: `{ "reason": "..." }` (máx. 512). Auditoria: admin id, data/hora, motivo.

Transições:

- approve: `PendingApproval` ou `Rejected` → `Approved`
- reject: `PendingApproval` → `Rejected`
- suspend: `Approved` → `Suspended`
- reactivate: `Suspended` ou `Rejected` → `Approved`

Staff nunca aparece nesta listagem.

## Proteções

Em `PrivateCatalogApprovedOnly`, exigem customer **Approved**:

- `GET /api/catalog/attributes|categories|products|products/{id}|products/by-slug/{slug}`
- `GET /api/inventory/skus/{skuId}` (vitrine; Backoffice continua com breakdown completo)
- `POST /api/checkout/sessions`
- `POST /api/orders/from-checkout-session`

Pedidos novos nesse modo nascem com `CustomerUserId`. Guest token **não** é emitido se o pedido tem customer ou se guest checkout está desligado. `GET /api/orders/public/{orderNumber}` permanece para pedidos legados.

## Migration

`AddCustomerAccessStatus` no schema `identity.users`. Clientes existentes → `Approved` (`AccessStatus=1`) com `ApprovedAt`/`AccessRequestedAt` = `CreatedAt`.
