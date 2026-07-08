# Shopflow — Contexto base para IA (GPT, Lovable, Cursor)

Use este arquivo como contexto inicial em qualquer prompt. Não assuma funcionalidades além do descrito aqui.

---

## O que é o Shopflow

E-commerce modular em monorepo. Backend .NET monólito modular + frontend React. Foco atual: catálogo, estoque, vitrine, carrinho local e checkout (UI simulada; API de sessão parcialmente pronta no backend).

**Repositório:** `apps/api` (backend), `apps/web` (frontend), `docs/` (documentação).

**Estado detalhado:** `docs/ai-context/shopflow-current-state.md`

---

## Stack

| Camada | Tecnologias |
|--------|-------------|
| Backend | .NET 10, Minimal APIs, MediatR, FluentValidation, EF Core, PostgreSQL |
| Frontend | React, TypeScript, Vite, TanStack Query, shadcn/ui, Tailwind |
| Infra local | Docker Compose: Postgres 16, API :5127, Web :8080 |
| E2E | Cypress 13 (Cursor mantém specs e `data-cy`) |

---

## Arquitetura

- **Monólito modular:** bounded contexts como projetos separados (`Catalog`, `Inventory`, `CartCheckout`, …).
- **Clean Architecture** por módulo: Domain → Application → Infrastructure.
- **HttpApi** (`Vls.Shopflow.HttpApi`) expõe Minimal APIs; handlers MediatR nos módulos.
- **PostgreSQL** com schema por módulo: `catalog`, `inventory`, `cartcheckout`.
- **Integração cross-module** via serviços (ex.: CartCheckout reserva estoque via Inventory; preço via Catalog). Sem FK cross-schema.
- **Frontend:** SPA React; proxy `/api` no Vite dev.

Diagrama e detalhes: `docs/architecture.md`.

---

## Módulos — o que existe hoje

| Módulo | Backend | Frontend |
|--------|---------|----------|
| Catalog | **Pronto** | Admin + vitrine integrados |
| Inventory | **Pronto** | Admin integrado |
| CartCheckout | **Parcial** (sessões + reserva) | **Stub** (checkout simulado na UI) |
| IdentityCustomer | Scaffold | **Visual-only** |
| Orders | Scaffold | Empty states |
| PaymentsPix | Scaffold | “Em breve” na UI |
| Shipping | Scaffold | “a calcular” |

---

## Decisões arquiteturais importantes

1. **Produto sempre com SKU** — não vender sem `skuId`; variantes são SKUs no Catalog.
2. **Estoque sempre por `skuId`** — Inventory não conhece “produto” diretamente na UI; opera em SKU.
3. **Checkout convidado permitido** — login/cadastro opcionais; nunca bloquear compra por falta de conta.
4. **Preço calculado no backend** — frontend exibe, backend recalcula na sessão checkout.
5. **Reserva de estoque no checkout** — feita pelo backend CartCheckout, **não** pelo frontend chamando Inventory diretamente.
6. **Carrinho local** — `localStorage`; não há API de carrinho server-side ainda.
7. **Imagens** — upload local em dev; R2/S3 pendente.

---

## Regras fixas (obrigatórias)

### Para todo agente

- **Não inventar endpoints.** Consultar `docs/ai-context/shopflow-current-state.md` ou `docs/catalog.md`, `docs/inventory.md`, `docs/cart-checkout.md`.
- **Não documentar como pronto** o que é mock, scaffold ou visual-only.
- **Preservar** Admin Catalog, Admin Inventory, vitrine, carrinho e checkout convidado.
- **Separar** `CustomerIdentity` (conta logada futura) de `CheckoutCustomer` (dados do convidado no checkout).

### Lovable

- **Só UX/UI/frontend.** Não criar backend.
- Não chamar APIs inexistentes; usar stubs com TODO explícito.
- Visual clean/premium (shadcn/ui, Tailwind).
- Respeitar checkout convidado.

### Cursor (revisão / integração)

- Rodar build, typecheck, lint (documentar erros pré-existentes).
- Validar rotas, regressão Admin/Vitrine/Cart/Checkout.
- Atualizar Cypress quando fluxo mudar.
- Atualizar docs.

### Cursor (backend)

- Seguir padrões existentes: Clean Architecture, CQRS, MediatR, FluentValidation, migrations EF.
- Não quebrar módulos existentes.
- Testes unitários + integração.
- Analisar código existente antes de implementar.

### Cursor (Cypress)

- Manter `data-cy` estáveis.
- Intercepts para garantir que endpoints inexistentes não são chamados (quando aplicável).
- Spec demo completo deve continuar passando.
- Rodar contra stack local real (Docker).

---

## Fluxo de trabalho recomendado

```
GPT (planeja feature + preenche prompt)
    → Lovable (UI/frontend visual)
        → Cursor (revisa, integra, testa, documenta)
            → Cursor (Cypress + docs/testing.md)
```

Templates: `docs/prompts/01-feature-template.md` … `05-cursor-cypress-template.md`.

---

## Referências rápidas

| Documento | Uso |
|-----------|-----|
| `docs/ai-context/shopflow-current-state.md` | Estado real do projeto |
| `docs/ai-context/next-actions.md` | Próximos passos |
| `docs/ai-context/technical-debt.md` | Dívidas técnicas |
| `docs/testing.md` | Como testar |
| `docs/cart-checkout.md` | Contrato API checkout |

---

## Próxima prioridade do projeto

**Integrar frontend checkout com `POST /api/checkout/sessions`** (CartCheckout backend já implementado).
