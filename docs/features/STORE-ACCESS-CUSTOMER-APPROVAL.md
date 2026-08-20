# Store access e aprovação de clientes — Fase 1 (backend) + Fase 3 (e-mails)

Implementação da Fase 1 do ADR `docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md` e da Fase 3 (Brevo/outbox).

Não inclui frontend (Fase 2) nem login visual unificado.

Detalhes por área:

- `docs/customer/customer-approval.md`
- `docs/catalog/store-access.md`
- `docs/checkout/checkout-session.md`
- `docs/orders/guest-order-access.md`

## Configuração

```env
StoreAccess__Mode=Closed
CustomerAccess__RequireApproval=true
Checkout__AllowGuest=false
Checkout__AllowGuestCheckout=false
```

| Ambiente | `mode` público | Modo interno | Guest | RequireApproval |
|----------|----------------|--------------|-------|-----------------|
| `appsettings.json` (TESTE/HML/PROD deste cliente) | `Closed` | `PrivateCatalogApprovedOnly` | `false` | efetivo `true` |
| `appsettings.Development.json` / testes de regressão | `Open` | `PublicCatalogAndGuestCheckout` | `true` | `false` |

Modos internos (ADR): `PublicCatalogAndGuestCheckout`, `PublicCatalogLoginCheckout`, `PublicCatalogApprovedCheckout`, `PrivateCatalogApprovedOnly`.

Aliases de configuração:

- `StoreAccess__Mode=Closed` → `PrivateCatalogApprovedOnly`
- `StoreAccess__Mode=Open` → `PublicCatalogAndGuestCheckout`
- `Checkout__AllowGuest` → mesmo efeito de `Checkout__AllowGuestCheckout`
- filtro admin `status=Pending` → `PendingApproval`

Valor de modo desconhecido falha fechado para `PrivateCatalogApprovedOnly` (`Closed`). Ausência de `Checkout:AllowGuest` / `AllowGuestCheckout` trata guest como **desligado** (default seguro). Development define os dois como `true`.

Loja `Closed` (ou `PublicCatalogApprovedCheckout`) força cadastro `Pending` mesmo se `CustomerAccess:RequireApproval=false`.

## Contratos

### Política pública

`GET /api/store/access` (anônimo)

```json
{
  "mode": "Closed",
  "storeAccessMode": "PrivateCatalogApprovedOnly",
  "allowGuest": false,
  "allowGuestCheckout": false,
  "requireApprovedCustomerToBrowse": true,
  "requireLoginForCheckout": true,
  "requireApprovedCustomerForCheckout": true
}
```

### Customer DTO (`register`, `login`, `/me`)

```json
{
  "approvalStatus": "Pending",
  "accessStatus": "PendingApproval",
  "approvalRequestedAt": "2026-08-16T13:00:00+00:00",
  "accessRequestedAt": "2026-08-16T13:00:00+00:00",
  "approvedAt": null,
  "message": "Cadastro enviado para aprovação."
}
```

`message` só no register. Status públicos: `Pending` | `Approved` | `Rejected` | `Suspended`. Canônicos: `PendingApproval` | `Approved` | `Rejected` | `Suspended`.

Cadastro **não** emite cookie. Login com senha correta emite `CustomerCookie` mesmo se pendente/recusado/suspenso. Catálogo privado e checkout continuam bloqueados no backend.

Senha forte obrigatória no register/reset (mín. 8, maiúscula, minúscula, dígito, especial). Ver `docs/customer/customer-auth.md`.

`ICustomerAccessNotifier` enfileira e-mails de pendência/aprovação no outbox Brevo (`docs/customer/customer-approval-emails.md`). Falha de e-mail não quebra o cadastro.

### Codes (ProblemDetails `code`)

| Code | HTTP | Quando |
|------|------|--------|
| `STORE_ACCESS_REQUIRES_LOGIN` | 401 | Catálogo Closed sem sessão customer |
| `STORE_ACCESS_REQUIRES_APPROVAL` | 403 | Catálogo Closed com customer não aprovado |
| `CUSTOMER_LOGIN_REQUIRED` | 401 | Checkout exige login (modo Open com guest off por política de login) |
| `GUEST_CHECKOUT_DISABLED` | 401 | Checkout/pedido anônimo com `AllowGuest=false` |
| `CUSTOMER_APPROVAL_PENDING` | 403 | Checkout com cadastro em análise |
| `CUSTOMER_ACCESS_REJECTED` | 403 | Recusado |
| `CUSTOMER_ACCESS_SUSPENDED` | 403 | Suspenso |
| `CUSTOMER_ACCESS_NOT_APPROVED` | 403 | Fallback não aprovado |
| `CUSTOMER_APPROVAL_INVALID_STATUS` | 400/409 | Filtro ou transição admin inválida |
| `CUSTOMER_APPROVAL_REASON_TOO_LONG` | 400 | Motivo > 1000 |
| `CUSTOMER_NOT_FOUND` | 404 | Customer admin inexistente |

Mensagens PT-BR (exemplos): “Para comprar, entre com uma conta aprovada.”; “Seu cadastro ainda está em análise.”; “O checkout como convidado está desabilitado.”; “Esta loja está disponível apenas para clientes aprovados.”

### Admin (Backoffice + CSRF nas mutações)

```
GET  /api/admin/customers/approvals?status=&q=&page=&pageSize=&createdFrom=&createdTo=&sort=
GET  /api/admin/customers/approvals/count          → { "pending": n, "pendingCount": n }
GET  /api/admin/customers?status=&q=&page=&pageSize=&createdFrom=&createdTo=&sort=
GET  /api/admin/customers/pending-count            (alias do count)
GET  /api/admin/customers/{id}
POST /api/admin/customers/{id}/approve
POST /api/admin/customers/{id}/reject
POST /api/admin/customers/{id}/suspend
POST /api/admin/customers/{id}/reactivate
```

`/approvals` lista **Pending** por default (`status=all` lista todos). `q` busca nome/e-mail/telefone. `sort`: `createdAt`, `email`, `name`, `requestedAt` (prefixo `-` = desc).

Body opcional: `{ "reason": "..." }` (máx. 1000). Auditoria: admin id, data/hora, motivo. DTO não expõe hash/tokens.

Transições:

- approve: `PendingApproval` / `Rejected` / `Suspended` → `Approved`
- reject: `PendingApproval` → `Rejected`
- suspend: `Approved` → `Suspended`
- reactivate: `Suspended` / `Rejected` → `Approved`

Staff nunca aparece nesta listagem. Customer comum não acessa.

## Proteções

Em `Closed` / `PrivateCatalogApprovedOnly`, exigem customer **Approved** (staff Backoffice continua autorizado):

- `GET /api/catalog/attributes|categories|products|products/{id}|products/by-slug/{slug}`
- `GET /api/inventory/skus/{skuId}` (vitrine)
- `POST /api/checkout/sessions`
- `POST /api/orders/from-checkout-session`

Públicos: health, auth, register, forgot/reset, csrf, CEP, guest tracking legado.

Pedidos novos em Closed nascem com `CustomerUserId`. Guest token **não** é emitido se o pedido tem customer ou se guest checkout está desligado. `GET /api/orders/public/{orderNumber}` permanece para pedidos antigos.

## Migration

`AddCustomerAccessStatus` — clientes existentes → `Approved`.  
`ExpandCustomerAccessDecisionReason` — motivo até 1000 caracteres.
