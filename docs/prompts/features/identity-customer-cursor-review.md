# Feature: IdentityCustomer — Revisão Cursor (pós-Lovable)

> **Status: CONCLUÍDO** (maio/2026). Cypress identity + demo passando.
> Template base: `docs/prompts/03-cursor-review-template.md`

---

## Contexto

Lovable entregou preparação visual IdentityCustomer. Cursor revisou e estabilizou sem implementar backend.

**Decisão de produto:** checkout convidado permitido; login/cadastro opcionais.

---

## O que foi implementado (Lovable)

| Área | Entrega |
|------|---------|
| Rotas | `/login`, `/register`, `/forgot-password`, `/account/*` |
| Componentes | AuthLayout, AccountLayout, AuthRequiredState, GuestCheckoutNotice |
| Contexto | AuthContext visual-only |
| Serviços | authService stub |
| UI | Header login/conta, aviso checkout, dropdown preparado |

---

## Objetivo da revisão

Corrigir build, types, rotas, Cypress, arquitetura frontend, uso indevido de APIs. **Sem backend IdentityCustomer.**

---

## Checklist executado

### Build / types / lint

```bash
cd apps/web && npm run build      # ✓ passou
cd apps/web && npm run typecheck  # ✓ passou
cd apps/web && npm run lint       # ✗ 6 erros pré-existentes (fora escopo)
```

### Rotas validadas (`App.tsx`)

- [x] `/login`, `/register`, `/forgot-password`
- [x] `/account`, `/account/orders`, `/account/orders/:id`, `/account/addresses`, `/account/profile`
- [x] Loja e admin intactos

### AuthContext

- [x] Visual-only, sem persistência
- [x] Sem token fake permanente
- [x] `signInVisualOnly` existe mas Login/Register **não** autenticam de fato
- [x] `isAuthenticated` false em uso normal → conta mostra `AuthRequiredState`

### authService

- [x] Stubs only — zero HTTP
- [x] TODOs explícitos para `/api/identity/*`

### Endpoints — nenhuma chamada real

Proibidos e verificados: `/api/auth`, `/api/identity`, `/api/customers`, `/api/account`, `/api/orders`, `/api/addresses`, `/api/profile`

### Checkout convidado

- [x] Não exige login
- [x] `GuestCheckoutNotice` visível
- [x] “Continuar como convidado” **ativo**
- [x] Checkout permanece simulado (`checkoutService` stub)
- [x] Mensagem pós-compra honesta sobre IdentityCustomer

### Redirect seguro

- [x] `/login?redirect=/checkout` aceito
- [x] URLs externas rejeitadas (`getSafeRedirectPath`)

### Header

- [x] `data-cy="header-account-link"`
- [x] Carrinho intacto

### Cypress

Spec: `cypress/e2e/identity-customer.cy.ts` — **7/7 passando**

Cobertura:

1. Header login
2. `/login` renderiza + links
3. `/register` formulário
4. `/forgot-password`
5. `/account` → AuthRequiredState
6. Checkout guest notice + continuar convidado
7. Redirect seguro

Também revalidado: `shopflow-demo.cy.ts`, `checkout-simulated.cy.ts` (ajuste seletores pós-GuestCheckoutNotice).

### Documentação atualizada

- `docs/architecture.md` — seção IdentityCustomer frontend
- `docs/next-steps.md` — estado visual
- `docs/testing.md` — spec identity + limitações

---

## Bugs corrigidos na revisão

| Bug | Correção |
|-----|----------|
| “Continuar como convidado” disabled | Botão ativo + focus `#fullName` |
| Redirect externo aceito | `safeRedirect.ts` |
| Cypress clicava “Continuar como convidado” em vez de avançar etapa | `data-cy="checkout-contact-continue"` etc. |
| Spec `/account` esperava `<button>Entrar` | Ajustado para `<Link>` |

---

## Dívidas restantes (esperadas)

- IdentityCustomer backend (JWT, sessão, perfil real)
- Login wired a auth real
- `signInVisualOnly` para preview UI logada (opcional)
- Lint global frontend (6 erros pré-existentes)
- Orders para “Meus pedidos” real

---

## Roteiro manual

1. **Login** — `/login` → submit → toast stub; link register/forgot ok
2. **Cadastro** — `/register` → validação senha → toast stub
3. **Forgot** — `/forgot-password` → toast stub
4. **Account** — `/account` sem login → AuthRequiredState
5. **Checkout convidado** — carrinho → checkout → preencher identificação sem login

---

## Critérios de aceite — todos atendidos

- [x] Build passa
- [x] TypeScript passa
- [x] Rotas renderizam
- [x] AuthContext não finge produção
- [x] Nenhum endpoint inexistente chamado
- [x] Checkout convidado não bloqueado
- [x] Admin não quebrado (demo E2E)
- [x] Cypress atualizado e passando
- [x] Docs refletem visual-only

---

## Reutilizar este prompt

Para próxima entrega Lovable de frontend visual:

1. Copiar este arquivo
2. Substituir seção “O que foi implementado”
3. Anexar escopo Lovable
4. Seguir `03-cursor-review-template.md`
