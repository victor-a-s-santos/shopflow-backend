# SEC-005 — Customer Identity Backend

> Data: 2026-06-30  
> Escopo: autenticação de cliente no módulo `IdentityAccess` (extensão da Fase 1/2)  
> Objetivo: cookie HttpOnly separado do admin, checkout convidado preservado, sem JWT/localStorage.

---

## 1. Resumo

O Shopflow estende o módulo `IdentityAccess` com endpoints de cliente, role `Customer`, policy `Customer` e cookie `CustomerCookie` independente do cookie admin (`Identity.Application`).

| Item | Valor |
|------|-------|
| JWT | Não utilizado |
| Token no frontend | Não — apenas cookie HttpOnly |
| Schema DB | Reutiliza `identity` (sem migration nova) |
| Checkout convidado | Inalterado — endpoints públicos |

---

## 2. Endpoints

Base: `/api/auth/customer`

| Método | Path | Auth | CSRF | Rate limit |
|--------|------|------|------|------------|
| `POST` | `/register` | Público | Não | 5/min (100/min em Development) |
| `POST` | `/login` | Público | Não | 10/min por IP |
| `POST` | `/logout` | Policy `Customer` | **Sim** | — |
| `GET` | `/me` | Policy `Customer` | Não | — |
| `POST` | `/forgot-password` | Público | Não | 5/min (100/min em Development) |
| `POST` | `/reset-password` | Público | Não | 5/min (100/min em Development) |
| `POST` | `/confirm-email` | Público | Não | — |

CSRF compartilhado: `GET /api/auth/csrf` → header `X-CSRF-TOKEN`.

### Register (201)

Cria usuário com role `Customer`, `is_customer=true`, `is_staff=false`. **Não faz login automático** (MVP). Retorna `emailConfirmed: false`. Token de confirmação é gerado e enviado via `IIdentityEmailSender` (log em Development).

### Login (200)

Autentica via scheme `CustomerCookie`. Usuários `IsStaff` ou sem role `Customer` recebem 401 genérico. Login permitido **sem** e-mail confirmado (MVP documentado).

### Logout (204)

Sign-out apenas do scheme `CustomerCookie`. Não invalida sessão admin.

### Me (200 / 401)

Retorna dados básicos do cliente autenticado pelo cookie customer. Cookie admin **não** autentica este endpoint.

### Forgot / reset / confirm

- Forgot: resposta genérica sempre (não vaza existência de conta).
- Reset: valida token Identity; atualiza `SecurityStamp`; não autentica automaticamente.
- Confirm: confirma e-mail via token Identity.

---

## 3. Cookie customer

| Ambiente | Nome | Secure | SameSite |
|----------|------|--------|----------|
| Development | `shopflow_customer_dev` | SameAsRequest | Lax |
| hml/prod | `__Host-shopflow_customer` | Always | Lax |

Propriedades: HttpOnly, Path=/, sem Domain (obrigatório com `__Host-`).

Configuração: `appsettings` → `CustomerAuth` (`SessionDays`, nomes dev/prod).

Sessão: sliding expiration, default **30 dias**.

---

## 4. Separação admin × customer

### Schemes

| Papel | Scheme ASP.NET | Cookie |
|-------|----------------|--------|
| Admin | `Identity.Application` | `shopflow_admin_dev` / `__Host-shopflow_admin` |
| Customer | `CustomerCookie` | `shopflow_customer_dev` / `__Host-shopflow_customer` |

### Policies

| Policy | Scheme | Requisitos |
|--------|--------|------------|
| `Backoffice` | Admin | Role `Owner`, claim `is_staff=true` |
| `Customer` | Customer | Role `Customer`, claim `is_customer=true` |

### Middleware `CookiePrincipalMiddleware`

O scheme padrão de autenticação é o admin. Clientes com apenas cookie customer não eram reconhecidos em `HttpContext.User` antes do endpoint. O middleware resolve o principal a partir do path:

- `/api/auth/admin/*` → admin cookie
- `/api/auth/customer/*` → customer cookie
- Demais rotas (ex.: `/api/auth/csrf`) → customer, depois admin

Isso garante que tokens CSRF sejam emitidos para o usuário autenticado correto.

### Regras de negócio

- Staff/Owner **não** fazem login como customer (bloqueio em `CustomerLoginService`).
- Logout customer não derruba admin e vice-versa.
- Endpoints `Backoffice` (Catalog escrita, Inventory admin, Orders GET, etc.) rejeitam cookie customer.

---

## 5. Roles e claims

| Constante | Valor |
|-----------|-------|
| `AuthRoles.Customer` | `Customer` |
| `AuthClaims.IsCustomer` | `is_customer=true` |
| `AuthPolicies.Customer` | Policy para endpoints customer |

Seed garante existência da role `Customer` no startup.

---

## 6. CSRF

Middleware `CsrfProtectionMiddleware` — mesma regra da Fase 1/2:

- Mutations com cookie admin **ou** customer autenticado exigem `X-CSRF-TOKEN`.
- Excluídos: login/register customer, forgot/reset/confirm, admin login, webhooks.

Antiforgery cookie: `shopflow_csrf_dev` (dev) / `__Host-shopflow_csrf` (prod), SameSite=Lax.

---

## 7. Rate limiting

| Policy | Endpoints | Limite (prod) | Limite (dev) |
|--------|-----------|---------------|--------------|
| `customer-login` | login | 10/min IP | 10/min IP |
| `customer-register` | register | 5/min IP | 100/min IP |
| `customer-forgot-password` | forgot-password | 5/min IP | 100/min IP |
| `customer-reset-password` | reset-password | 5/min IP | 100/min IP |

Limites elevados em Development evitam flakiness nos testes de integração.

---

## 8. Password policy / lockout

Reutiliza configuração Identity existente:

- Mínimo 8 caracteres, 1 dígito, 1 minúscula.
- Lockout: 5 tentativas → 15 minutos.

---

## 9. E-mail (pendente)

Abstração `IIdentityEmailSender`:

- **Production/hml:** `LoggingIdentityEmailSender` (log seguro, sem envio real).
- **Testes:** `CapturingIdentityEmailSender` captura tokens para reset/confirm.

Envio real de e-mail fica para fase **Notifications**.

---

## 10. Checkout convidado

**Nenhuma alteração** nos fluxos públicos:

- `POST /api/checkout/sessions`
- `POST /api/orders/from-checkout-session`
- `POST /api/payments/pix/orders/{orderId}`

Registro/login customer não é pré-requisito para comprar. Pedidos não são vinculados a `CustomerId` nesta fase.

---

## 11. Migrations

Nenhuma migration nova. Tabelas `identity.AspNetUsers`, roles e claims já existentes (`InitialIdentityAccess`). Campos `FullName`, `PhoneNumber`, `EmailConfirmed` em `ShopflowUser` são reutilizados.

---

## 12. Testes

Projeto: `Vls.Shopflow.IdentityAccess.IntegrationTests` (30 casos).

Cobertura principal:

- Register, login, me, logout (com/sem CSRF)
- Forgot/reset/confirm com `CapturingIdentityEmailSender`
- Separação admin/customer (me cruzado, Catalog admin bloqueado)
- Checkout público sem cookie customer
- Rate limit tolerante em Development

Execução: `dotnet test tests/Vls.Shopflow.IdentityAccess.IntegrationTests` (requer Postgres; `SHOPFLOW_TEST_DB` opcional).

---

## 13. Limitações conhecidas

| Item | Status |
|------|--------|
| Frontend customer auth | Pendente — UI continua visual-only |
| Account / meus pedidos | Pendente |
| Guest Order Access Token | Pendente (Fase 4) |
| E-mail real (confirm/reset) | Pendente |
| Mercado Pago / webhook | Pausado até HML/domínio |
| Vinculação pedido ↔ customer | Pendente |
| MFA | Fora de escopo |

---

## 14. Próximo passo recomendado

1. **Frontend:** integrar `authService` com endpoints customer + CSRF (cookie credentials).
2. **Guest Order Access Token** + Account orders.
3. **Notifications** para e-mail de confirmação/reset em produção.

Ver também: [README-identity-security-roadmap.md](./README-identity-security-roadmap.md).
