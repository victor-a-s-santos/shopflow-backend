Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core Identity, cookies HttpOnly, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL e segurança de e-commerce B2B.

Objetivo:
Implementar a **Fase 1 — Backend Customer Approval** do ADR `docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md`.

A loja atual (Vip Assessoria / lojistas e revendedores) passa a ser **privada com aprovação administrativa**. Checkout convidado fica atrás de configuração. Não fundir admin e customer.

Nomes canônicos do ADR (use estes no código e nos contratos JSON):

```text
StoreAccess:Mode
  PublicCatalogAndGuestCheckout
  PublicCatalogLoginCheckout
  PublicCatalogApprovedCheckout
  PrivateCatalogApprovedOnly   ← cliente atual

Checkout:AllowGuestCheckout    (bool)

CustomerAccess:RequireApproval (bool)

CustomerAccessStatus
  PendingApproval
  Approved
  Rejected
  Suspended
```

Aliases aceitos na **configuração** (rascunho Open/Closed do PR #4, não são o contrato JSON):

```text
StoreAccess__Mode=Closed  → PrivateCatalogApprovedOnly
StoreAccess__Mode=Open    → PublicCatalogAndGuestCheckout
Checkout__AllowGuest      → mesmo efeito de Checkout__AllowGuestCheckout
status=Pending            → PendingApproval (filtro admin)
```

Cliente atual (TESTE/HML/PROD):

```env
StoreAccess__Mode=PrivateCatalogApprovedOnly
CustomerAccess__RequireApproval=true
Checkout__AllowGuestCheckout=false
```

==================================================
1. LEITURA OBRIGATÓRIA
==================================================

Antes de implementar, leia:

* docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md
* docs/security/SEC-005-customer-identity-backend.md
* docs/security/FE-SEC-003-admin-auth-separation.md
* docs/cart-checkout.md
* docs/orders.md
* docs/features/EMAIL-001-transactional-email-outbox-brevo.md
* docs/features/EMAIL-002-guest-order-link-validation.md
* módulo IdentityAccess (ShopflowUser, CustomerAuthServices, policies, cookies)
* CatalogEndpoints (GETs públicos da vitrine)
* CheckoutEndpoints / OrdersEndpoints (create session / from-checkout-session)
* AdminOrdersEndpoints (padrão Backoffice + CSRF)
* testes IdentityAccess / Orders / Checkout
* Program.cs ProblemDetails

Siga a arquitetura existente. Não crie um segundo sistema de auth.

==================================================
2. ESCOPO (Fase 1 backend)
==================================================

Implementar:

1. Config `StoreAccess:Mode` + `Checkout:AllowGuestCheckout` + `CustomerAccess:RequireApproval`.
2. `GET /api/store/access` anônimo (sem secrets).
3. Campo `CustomerAccessStatus` em `ShopflowUser` (não reutilizar `EmailConfirmed`, `IsActive`, `IsStaff`).
4. Migration: clientes existentes → `Approved`.
5. Register: `PendingApproval` + `AccessRequestedAt=now` quando `RequireApproval=true`. Sem login automático.
6. Login/`/me`/register devolvem `accessStatus`, `accessRequestedAt`, `approvedAt`.
7. Pendente/recusado/suspenso **pode autenticar** (senha ok + `IsActive`) para o FE redirecionar. Compra e catálogo privado continuam bloqueados no backend.
8. Gates backend:
   * catálogo completo (preços/SKUs) em `PrivateCatalogApprovedOnly` exige customer `Approved`;
   * `POST /api/checkout/sessions` e `POST /api/orders/from-checkout-session` respeitam a política;
   * guest checkout bloqueado quando `AllowGuestCheckout=false`;
   * novos pedidos no modo privado com `CustomerUserId`; sem `guestAccessToken` novo nesse modo.
9. Admin Backoffice + CSRF nas mutações:
   * listar (filtro status, busca nome/e-mail/telefone);
   * `pending-count`;
   * detalhe;
   * approve / reject / suspend / reactivate;
   * auditoria: admin id, data/hora, motivo.
10. ProblemDetails `code`:
    * `CUSTOMER_LOGIN_REQUIRED`
    * `CUSTOMER_ACCESS_NOT_APPROVED`
    * `CUSTOMER_ACCESS_REJECTED`
    * `CUSTOMER_ACCESS_SUSPENDED`
    * `GUEST_CHECKOUT_DISABLED`
11. Testes + docs.

`GET /api/store/access` resposta:

```json
{
  "mode": "PrivateCatalogApprovedOnly",
  "allowGuestCheckout": false,
  "requireApprovedCustomerToBrowse": true,
  "requireLoginForCheckout": true,
  "requireApprovedCustomerForCheckout": true
}
```

Admin:

```text
GET  /api/admin/customers?status=&q=&page=&pageSize=
GET  /api/admin/customers/pending-count
GET  /api/admin/customers/{id}
POST /api/admin/customers/{id}/approve
POST /api/admin/customers/{id}/reject
POST /api/admin/customers/{id}/suspend
POST /api/admin/customers/{id}/reactivate
```

Não devolver `guestAccessToken`, hashes nem staff nesta listagem.

Guest tracking legado (`GET /api/orders/public/{orderNumber}`, EMAIL-002 `?t=`) **permanece**. Não apagar infra guest.

==================================================
3. FORA DO ESCOPO
==================================================

Não implementar agora:

* Frontend (login unificado, `/admin/login` redirect, guards, tela de aprovações, badge).
* E-mails Brevo de cadastro/aprovação (Fase 3).
* Fusão de cookies/schemes/policies admin+customer.
* Um único `POST /api/auth/login`.
* Remoção de `/admin/*`.
* Remoção de guest tracking / reemissão de token.
* Multi-tenant SaaS.
* Notification center.
* Aprovação por CNPJ.
* Login social / 2FA.

==================================================
4. REGRAS DURAS
==================================================

* Policy `Backoffice` nas rotas admin; policy `Customer` nas rotas customer.
* Nenhuma rota admin aceita customer comum.
* Staff não autentica em `/api/auth/customer/login`.
* Frontend não é a única barreira do catálogo privado.
* Development/testes de regressão podem usar `PublicCatalogAndGuestCheckout` + `AllowGuestCheckout=true` + `RequireApproval=false` para não quebrar specs antigas.
* `appsettings.json` / TESTE / HML deste cliente: modo privado.

==================================================
5. TESTES MÍNIMOS
==================================================

* Register com aprovação → `PendingApproval`, sem cookie.
* Login pendente devolve status; catálogo/checkout 403 `CUSTOMER_ACCESS_NOT_APPROVED`.
* Anônimo em modo privado: catálogo 401; checkout 401 `GUEST_CHECKOUT_DISABLED`.
* Admin lista pendentes, aprova, recusa, suspende, reativa (CSRF).
* Customer não acessa `/api/admin/customers`.
* Aprovado lê catálogo.
* Recusado/suspenso não compra.
* Pedido vinculado a customer não emite `guestAccessToken`.
* Alias `Closed`/`Open`/`AllowGuest`/`Pending` funcionam na config/filtro.

==================================================
6. DOCS
==================================================

Atualizar:

* docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md (status Fase 1)
* docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md
* docs/security/SEC-005-customer-identity-backend.md (ponteiro)
* deploy/.env.test.example e .env.hml.example
* .env.example

Não estimar calendário. Não misturar com merge de identity admin+customer.
