# FE-SEC-003 — Separação Admin Auth × Customer Auth (frontend)

> **Status:** Decisão aprovada  
> **Escopo:** Frontend Shopflow (`apps/web`) — rotas, guards, sessão e contratos de API  
> **Fora de escopo:** Alterações de backend, produção final, novas implementações neste documento

Este documento define como o frontend deve manter **dois fluxos de autenticação independentes**: compradores (customer) na vitrine e operadores (admin) no backoffice. A implementação atual segue estas regras; qualquer mudança futura deve preservá-las.

**Referências relacionadas:**

- Integração admin: [apps/web/docs/security/FE-SEC-001-admin-auth-integration.md](../apps/web/docs/security/FE-SEC-001-admin-auth-integration.md)
- Integração customer: [apps/web/docs/security/FE-SEC-002-customer-auth-integration.md](../apps/web/docs/security/FE-SEC-002-customer-auth-integration.md)
- Backend (cookies, policies): [SEC-005-customer-identity-backend.md](./SEC-005-customer-identity-backend.md)

---

## 1. Rotas esperadas

O roteamento React Router deve respeitar a separação abaixo. Rotas públicas da loja **nunca** redirecionam visitantes para `/admin/login`.

### Vitrine — customer (comprador)

| Rota | Acesso | Propósito |
|------|--------|-----------|
| `/login` | Público | Login de **customer** (`Login.tsx`) |
| `/register` | Público | Cadastro de customer |
| `/forgot-password` | Público | Recuperação de senha customer |
| `/account` | Protegido (`CustomerRouteGuard`) | Visão geral da conta |
| `/account/orders` | Protegido | Pedidos do customer |
| `/account/orders/:id` | Protegido | Detalhe do pedido |
| `/account/addresses` | Protegido | Endereços |
| `/account/profile` | Protegido | Perfil |

Rotas públicas da loja (`/`, `/product/:slug`, `/cart`, `/checkout`) **não exigem** login customer.

### Backoffice — admin (operador)

| Rota | Acesso | Propósito |
|------|--------|-----------|
| `/admin/login` | Público | Login de **admin** (`AdminLogin.tsx`) |
| `/admin` | Protegido (`AdminRouteGuard`) | Dashboard |
| `/admin/products` | Protegido | Listagem de produtos |
| `/admin/products/new` | Protegido | Criar produto |
| `/admin/products/:id/edit` | Protegido | Editar produto |
| `/admin/inventory` | Protegido | Estoque / reservas |

### Regras de roteamento

- `/login` e `/register` são **exclusivos** do fluxo customer — nunca reutilizar para admin.
- `/admin/login` é **exclusivo** do fluxo admin — nunca reutilizar para customer.
- Customer autenticado que acessa `/admin/*` sem sessão admin deve cair em `/admin/login`, **não** em `/login`.
- Visitante em `/account/*` redireciona para `/login?redirect=...` (path codificado).
- Visitante em `/admin/*` (exceto `/admin/login`) redireciona para `/admin/login`.
- Admin autenticado que acessa `/admin/login` deve ser redirecionado para `/admin` (dashboard).

---

## 2. Diferença entre CustomerAuth e AdminAuth

São dois **domínios de identidade** distintos, com contextos React, services e cookies separados no backend.

| Aspecto | CustomerAuth | AdminAuth |
|---------|--------------|-----------|
| **Usuário** | Comprador cadastrado na loja | Operador do backoffice (seed ou staff) |
| **Contexto React** | `AuthContext` (`AuthProvider`) | `AdminAuthContext` (`AdminAuthProvider`) |
| **Service** | `customerAuthService.ts` | `adminAuthService.ts` |
| **Tela de login** | `/login` | `/admin/login` |
| **Área protegida** | `/account/*` | `/admin/*` |
| **Cookie HttpOnly** | `shopflow_customer_dev` (dev) / `__Host-shopflow_customer` (hml/prod) | `shopflow_admin_dev` (dev) / `__Host-shopflow_admin` (hml/prod) |
| **Scheme backend** | `CustomerCookie` | `Identity.Application` (admin) |
| **Role esperada** | `Customer` + claim `is_customer=true` | `Owner` (ou staff) + claim `is_staff=true` |
| **Seed admin** | Não se aplica | Usuário criado por `SHOPFLOW_ADMIN_EMAIL` / `SHOPFLOW_ADMIN_PASSWORD` |

### Usuário seed admin ≠ customer

O usuário provisionado no startup da API via `SHOPFLOW_ADMIN_EMAIL` e `SHOPFLOW_ADMIN_PASSWORD`:

- Tem `IsStaff = true` e role de backoffice (ex.: `Owner`).
- **Não** possui perfil de customer válido para login na vitrine.
- Deve autenticar **somente** em `POST /api/auth/admin/login` (tela `/admin/login`).
- Se as mesmas credenciais forem usadas em `/login` → `POST /api/auth/customer/login` retorna **401** com mensagem genérica (`Invalid email or password.`), pois `CustomerLoginService` rejeita `user.IsStaff`.

O backend já valida `/api/auth/admin/login` (testes de integração em `AdminAuthIntegrationTests`).

---

## 3. Endpoints esperados — admin

Base: `{VITE_API_BASE_URL}` → ex.: `https://api-teste.vipassessoriadigital.com.br/api`

| Método | Path relativo | CSRF | Uso no frontend |
|--------|---------------|------|-----------------|
| `POST` | `/auth/admin/login` | Não | `adminAuthService.adminLogin` — tela `/admin/login` |
| `POST` | `/auth/admin/logout` | **Sim** | `adminAuthService.adminLogout` — header `AdminLayout` |
| `GET` | `/auth/admin/me` | Não | `adminAuthService.getCurrentAdmin` — bootstrap `AdminAuthContext` |
| `GET` | `/auth/csrf` | Não | Compartilhado — token para mutations autenticadas |

Mutations de negócio admin (catalog escrita, inventory admin, etc.) usam policy `Backoffice` no backend e exigem cookie **admin**, não customer.

---

## 4. Endpoints esperados — customer

| Método | Path relativo | CSRF | Uso no frontend |
|--------|---------------|------|-----------------|
| `POST` | `/auth/customer/register` | Não | `customerAuthService.registerCustomer` — `/register` |
| `POST` | `/auth/customer/login` | Não | `customerAuthService.loginCustomer` — `/login` |
| `POST` | `/auth/customer/logout` | **Sim** | `customerAuthService.logoutCustomer` — área `/account` |
| `GET` | `/auth/customer/me` | Não | `customerAuthService.getCurrentCustomer` — bootstrap `AuthContext` |
| `POST` | `/auth/customer/forgot-password` | Não | `/forgot-password` |
| `POST` | `/auth/customer/reset-password` | Não | Service pronto; tela pendente |
| `POST` | `/auth/customer/confirm-email` | Não | Service pronto; tela pendente |
| `GET` | `/auth/csrf` | Não | Compartilhado com admin |

Register retorna **201** e **não** faz auto-login — o usuário deve entrar manualmente em `/login`.

---

## 5. Risco de misturar os dois fluxos

Misturar customer e admin no frontend ou na sessão do navegador introduz falhas de segurança e UX.

| Risco | Consequência |
|-------|--------------|
| Usar `/login` para admin | Credenciais seed falham silenciosamente (401); operador acha que o sistema está quebrado |
| Usar `/admin/login` para customer | Customer recebe 401; possível vazamento de que a área admin existe |
| Um único contexto de auth | Estado inconsistente: UI mostra “logado” na loja com cookie admin (ou vice-versa) |
| Reutilizar cookie/token entre fluxos | `/auth/customer/me` ignora cookie admin; `/auth/admin/me` ignora cookie customer — guards falham de forma confusa |
| Guard admin redirecionando vitrine para `/admin/login` | Cliente comum exposto ao backoffice; UX hostil |
| Guard customer redirecionando admin para `/login` | Operador perde contexto; tentativa de acessar `/account` com sessão errada |
| Persistir JWT em `localStorage` | XSS pode roubar sessão; contradiz modelo HttpOnly do backend |
| Logout único derrubando ambos | Operador testando loja e admin perde uma sessão ao sair da outra |
| CSRF token de um fluxo em logout do outro | 400 no logout; sessão “fantasma” no browser |

**Princípio:** cada fluxo tem rota de login, service, context, guard e cookie **próprios**. O único componente compartilhado aceitável é `csrfService` (cache em memória do token), desde que cada logout limpe o cache após a mutation correspondente.

---

## 6. Regras para guards

### `CustomerRouteGuard`

**Arquivo:** `apps/web/src/components/auth/CustomerRouteGuard.tsx`  
**Protege:** `/account/*` (wrapper em `App.tsx`)

| Estado | Comportamento |
|--------|---------------|
| `isLoading` | Spinner (`data-cy="customer-auth-loading"`) |
| Não autenticado | `Navigate` → `/login?redirect={pathname}` |
| Autenticado | Renderiza `<Outlet />` |

**Fonte de verdade:** `AuthContext` → `GET /auth/customer/me` no mount.  
**401 em `/auth/customer/me`:** trata como visitante; **sem** redirect global na vitrine (apenas nas rotas `/account/*`).

### `AdminRouteGuard`

**Arquivo:** `apps/web/src/components/admin/AdminRouteGuard.tsx`  
**Protege:** `/admin/*` exceto `/admin/login` (rota irmã pública em `App.tsx`)

| Estado | Comportamento |
|--------|---------------|
| `isLoading` | Spinner (`data-cy="admin-auth-loading"`) |
| `isForbidden` (403 em `/auth/admin/me`) | Tela “Sem permissão” + botão Sair |
| Não autenticado | `Navigate` → `/admin/login` com `state.from` |
| Autenticado | Renderiza `<Outlet />` |

**Fonte de verdade:** `AdminAuthContext` → `GET /auth/admin/me` no mount.

### Redirect global 401 (mutations admin)

**Arquivo:** `apps/web/src/services/api.ts` — função `maybeRedirectAdminUnauthorized`

- Só redireciona para `/admin/login` se o path atual começa com `/admin` **e** não é `/admin/login`.
- Requisições da vitrine (`/`, `/cart`, `/checkout`, `/account`) **nunca** disparam redirect para admin por 401.

---

## 7. Regras para storage / sessão

| Regra | Detalhe |
|-------|---------|
| **Sem JWT no browser** | Não usar `localStorage`, `sessionStorage` nem cookies legíveis por JS para sessão |
| **Sessão = cookie HttpOnly** | Backend emite cookie; frontend usa `credentials: "include"` em todo `fetch` |
| **Dois cookies independentes** | Admin e customer podem coexistir no mesmo browser sem conflito de nome |
| **Logout admin** | `POST /auth/admin/logout` + CSRF → invalida **apenas** cookie admin; chama `clearCsrfToken()` |
| **Logout customer** | `POST /auth/customer/logout` + CSRF → invalida **apenas** cookie customer; chama `clearCsrfToken()` |
| **Estado React separado** | `AuthContext` guarda `customerUser`; `AdminAuthContext` guarda `admin` — sem campo unificado “user” |
| **CSRF em memória** | `csrfService` cacheia token; compartilhado, mas emitido pelo backend conforme cookie ativo no path `/auth/csrf` |

### Coexistência no mesmo browser

Um operador pode, em teoria, estar logado como admin **e** como customer (contas diferentes). Os guards consultam contextos distintos:

- `/account/*` → só `AuthContext` / cookie customer
- `/admin/*` → só `AdminAuthContext` / cookie admin

Nunca inferir autenticação admin a partir do customer logado (ou o contrário).

---

## 8. Checklist — validação no navegador (DevTools → Network)

Executar com API e frontend rodando (local ou teste/hml). Filtrar por `auth` na aba Network. Cookies: Application → Cookies.

### 8.1 Login admin (`/admin/login`)

- [ ] `POST .../api/auth/admin/login` → **200**, body com `email` e `roles` (ex.: `Owner`)
- [ ] Response header `Set-Cookie` contém cookie admin (`shopflow_admin_dev` ou `__Host-shopflow_admin`)
- [ ] **Não** aparece cookie customer neste login
- [ ] Redirect para `/admin` após sucesso
- [ ] `GET .../api/auth/admin/me` → **200** com dados do admin

### 8.2 Credenciais admin em `/login` (deve falhar)

- [ ] Com credenciais de `SHOPFLOW_ADMIN_EMAIL` / `SHOPFLOW_ADMIN_PASSWORD`
- [ ] `POST .../api/auth/customer/login` → **401**
- [ ] Mensagem genérica (não revelar “conta é admin”)
- [ ] **Sem** novo cookie customer
- [ ] Permanece em `/login` com erro na UI

### 8.3 Login customer (`/login`)

- [ ] Registrar customer em `/register` ou usar conta de teste
- [ ] `POST .../api/auth/customer/login` → **200**, body com `customerId`, `email`, `roles`
- [ ] `Set-Cookie` contém cookie customer (`shopflow_customer_dev` ou `__Host-shopflow_customer`)
- [ ] **Não** aparece cookie admin neste login
- [ ] `GET .../api/auth/customer/me` → **200**

### 8.4 Guards de rota

- [ ] Visitante em `/account` → redirect para `/login?redirect=%2Faccount` (sem chamar admin)
- [ ] Visitante em `/admin` → redirect para `/admin/login`
- [ ] Customer logado em `/account` → página carrega; `GET /auth/customer/me` **200**
- [ ] Customer logado em `/admin` → redirect `/admin/login`; `GET /auth/admin/me` **401**
- [ ] Admin logado em `/admin` → dashboard; `GET /auth/admin/me` **200**
- [ ] Admin logado em `/account` → redirect `/login` (sem sessão customer)

### 8.5 Separação de cookies /me

Com **apenas** cookie admin presente:

- [ ] `GET .../api/auth/admin/me` → **200**
- [ ] `GET .../api/auth/customer/me` → **401**

Com **apenas** cookie customer presente:

- [ ] `GET .../api/auth/customer/me` → **200**
- [ ] `GET .../api/auth/admin/me` → **401**

### 8.6 Logout

Admin logado:

- [ ] `GET .../api/auth/csrf` → token
- [ ] `POST .../api/auth/admin/logout` com header `X-CSRF-TOKEN` → **204**
- [ ] Cookie admin removido; cookie customer (se existia) **permanece**
- [ ] Redirect para `/admin/login`

Customer logado:

- [ ] `POST .../api/auth/customer/logout` com CSRF → **204**
- [ ] Cookie customer removido; cookie admin (se existia) **permanece**

### 8.7 Storage

Após login customer ou admin:

- [ ] `localStorage` **sem** chaves de token/sessão auth
- [ ] `sessionStorage` **sem** chaves de token/sessão auth

(Cypress valida isso em `customer-auth.cy.ts` → `assertNoAuthTokensInStorage`.)

### 8.8 Vitrine pública intacta

- [ ] `/`, `/cart`, `/checkout` acessíveis **sem** login
- [ ] Nenhuma requisição espontânea a `/auth/admin/*` ao navegar na loja como visitante

---

## Resumo da decisão

```
┌─────────────────────────────────────────────────────────────────┐
│                        Frontend Shopflow                         │
├────────────────────────────┬────────────────────────────────────┤
│  Vitrine (customer)        │  Backoffice (admin)                 │
│  /login, /register         │  /admin/login                       │
│  /account/*                │  /admin/*                           │
│  AuthContext               │  AdminAuthContext                   │
│  customerAuthService       │  adminAuthService                   │
│  CustomerRouteGuard        │  AdminRouteGuard                    │
│  cookie customer           │  cookie admin                       │
│  /api/auth/customer/*      │  /api/auth/admin/*                  │
└────────────────────────────┴────────────────────────────────────┘
                              │
                    GET /api/auth/csrf (compartilhado)
```

**Não implementar neste documento** — ele registra a decisão e o comportamento esperado. Novas features (reset-password UI, orders em `/account`, etc.) devem respeitar a separação acima.
