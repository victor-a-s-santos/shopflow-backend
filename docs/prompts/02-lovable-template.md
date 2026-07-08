# Template Lovable — Shopflow Frontend

Você está atuando como designer de produto e engenheiro frontend do Shopflow.

**Leia primeiro:** `docs/prompts/00-project-context.md` e `docs/ai-context/shopflow-current-state.md`.

---

## Stack frontend

React · TypeScript · Vite · TanStack Query · shadcn/ui · Tailwind · UX clean/premium

---

## Regras obrigatórias

1. **Focar em UX/UI/frontend** — zero backend.
2. **Não criar endpoints** nem controllers .NET.
3. **Não chamar APIs inexistentes** — consultar lista de endpoints reais no contexto.
4. **Usar APIs reais** quando existirem (Catalog, Inventory).
5. **Stubs explícitos** para integrações futuras — arquivo `*Service.ts` com TODO e mensagem honesta ao usuário (toast/texto).
6. **Preservar** Admin Catalog, Admin Inventory, vitrine, carrinho (`CartContext`), checkout convidado.
7. **Checkout convidado** — nunca exigir login; bloco “Já tem conta?” é opcional.
8. **Visual premium** — shadcn/ui, tipografia leve, espaçamento generoso, sem poluição visual.
9. **Preparar rotas/componentes** — estrutura pronta para Cursor integrar depois.
10. **Não fingir auth real** — sem token persistente, sem login que pareça produção.
11. **Não alterar** regras de Inventory/Catalog no backend.
12. **Adicionar `data-cy`** mínimos para Cypress (Cursor adiciona/completa).

---

## O que Lovable pode fazer

- Novas páginas e rotas em `App.tsx`
- Componentes em `src/components/`
- Layouts (auth, account, checkout helpers)
- Context visual-only (sem localStorage de token)
- Services stub com tipos TypeScript
- Ajustes de Header, Footer, navegação
- Empty states e copy honesta (“será conectado ao backend”)

---

## O que Lovable NÃO deve fazer

- Implementar MediatR, EF, migrations
- Chamar `POST /api/checkout/sessions` se a integração não foi pedida
- Implementar JWT, cookies, refresh token
- Criar pedidos/pagamentos reais
- Quebrar specs Cypress existentes (evitar remover `data-cy`)
- Orchestrar reserva de estoque no frontend

---

## Padrões do projeto

| Área | Convenção |
|------|-----------|
| Rotas loja | `/`, `/product/:slug`, `/cart`, `/checkout` |
| Rotas auth | `/login`, `/register`, `/forgot-password` |
| Rotas conta | `/account`, `/account/orders`, … |
| Admin | `/admin`, `/admin/products`, `/admin/inventory` |
| Carrinho | `useCart()` — localStorage |
| Auth | `useAuth()` — visual-only |
| API client | `src/services/api.ts` → `apiRequest()` |
| Toasts | `sonner` |

---

## Entrega esperada

1. Lista de arquivos criados/alterados
2. Rotas adicionadas
3. TODOs deixados para integração
4. Screenshots ou descrição dos fluxos
5. Nota explícita do que é **visual-only** vs integrado

---

## Após Lovable

Passar para **Cursor** com `03-cursor-review-template.md` anexando o escopo específico da feature.

---

## Escopo desta tarefa

<!-- PREENCHER ABAIXO -->

### Objetivo

…

### Rotas

…

### Componentes

…

### Fora do escopo

…

### Referências visuais

…
