# Feature: IdentityCustomer — Frontend visual (Lovable)

> **Status: CONCLUÍDO** (maio/2026). Este prompt permanece como referência para features similares ou ajustes visuais futuros.
> Implementação revisada pelo Cursor — ver `identity-customer-cursor-review.md`.

Use com: `docs/prompts/02-lovable-template.md` + `docs/prompts/00-project-context.md`

---

## Contexto

Shopflow permite **checkout como convidado**. Login/cadastro são opcionais. Backend IdentityCustomer **não existe** — este prompt preparou apenas UI/rotas.

**Já implementado pelo Lovable e revisado:**

- Rotas auth/conta
- `AuthLayout`, `AccountLayout`, `AuthRequiredState`, `GuestCheckoutNotice`
- `AuthContext` visual-only
- `authService` stub
- Header com login/conta
- Bloco “Já tem uma conta?” no checkout
- Cypress `identity-customer.cy.ts`

---

## Objetivo (original)

Preparar frontend para autenticação futura sem backend real: rotas, páginas, componentes visuais, stubs honestos.

---

## Regras obrigatórias

- **Não criar backend** nem chamar endpoints inexistentes
- **Não bloquear checkout convidado**
- **Não fingir auth real** — sem token persistente, sem login que redireciona como produção
- **Preservar** Admin Catalog, Admin Inventory, vitrine, carrinho, checkout
- **Mensagens honestas** — “ainda não conectado ao backend”
- **`data-cy` mínimos** para Cypress

---

## Rotas entregues

| Rota | Comportamento |
|------|---------------|
| `/login` | Form e-mail/senha → stub → toast |
| `/register` | Form completo → stub → toast |
| `/forgot-password` | E-mail → stub → toast |
| `/account` | `AuthRequiredState` se não autenticado |
| `/account/orders` | Empty state |
| `/account/orders/:id` | “Pedido não disponível” |
| `/account/addresses` | CRUD em memória (sessão) |
| `/account/profile` | Campos disabled + toast editar |

---

## Componentes entregues

| Componente | Função |
|------------|--------|
| `AuthLayout` | Layout auth pages + prop `pageCy` |
| `AccountLayout` | Sidebar conta + gate auth |
| `AuthRequiredState` | CTAs Entrar / Criar conta / Continuar comprando |
| `GuestCheckoutNotice` | Checkout — login opcional, convidado default |

---

## Header

- Ícone/link **Entrar** → `/login` (`data-cy="header-account-link"`)
- Dropdown logado preparado (Meus pedidos, Endereços, Perfil, Sair) — só visível se `isAuthenticated` (hoje sempre false em uso normal)

---

## Checkout convidado

- `GuestCheckoutNotice` no passo Identificação
- Botão **Entrar** → `/login?redirect=/checkout`
- Botão **Continuar como convidado** — ativo, foca `#fullName`
- Fluxo checkout **não exige** login

---

## Serviços (stub)

`authService.ts`:

- `login`, `register`, `requestPasswordReset` → `{ ok: false, message }`
- `simulateLoginForUiOnly` — helper visual, **não wired** nas páginas
- TODOs: `POST /api/identity/login`, `/register`, `/password/forgot`

**Proibido:** fetch para `/api/auth`, `/api/customers`, etc.

---

## AuthContext

- Estado em memória React only
- `signInVisualOnly` / `signOut` — sem localStorage
- Separar tipos `CustomerIdentity` vs `CheckoutCustomer`

---

## Fora do escopo (mantido)

- JWT, cookies, refresh token
- Orders API
- Backend IdentityCustomer
- Persistência de endereços/perfil

---

## `data-cy` entregues

```
login-page, register-page, forgot-password-page
account-page, account-auth-required
header-account-link
checkout-guest-notice, checkout-continue-as-guest
checkout-contact-continue, checkout-address-continue, checkout-payment-continue
```

---

## Referência estendida

Especificação detalhada original: `apps/web/docs/prompts/lovable-identity-customer-frontend.md`

**Sugestão:** consolidar no futuro em um único arquivo em `docs/prompts/features/` para evitar duplicata.

---

## Próximo passo (não Lovable)

Identity backend + Orders — usar `04-cursor-backend-template.md` quando priorizado (após CartCheckout frontend integrado e Orders).
