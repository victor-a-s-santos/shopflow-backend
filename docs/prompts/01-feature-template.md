# Template — Nova feature Shopflow

Copie este arquivo para `docs/prompts/features/<nome-da-feature>.md` e preencha antes de enviar ao GPT, Lovable ou Cursor.

---

## Contexto

- Estado atual relevante: (linkar `docs/ai-context/shopflow-current-state.md`)
- Módulo(s) afetado(s):
- Decisões de produto aplicáveis:

---

## Objetivo

(1–3 frases: o que deve existir ao final)

---

## Escopo

### Incluído

- [ ] …

### Fora do escopo

- [ ] Backend de módulos não relacionados
- [ ] Endpoints não acordados
- [ ] …

---

## Backend

| Item | Detalhe |
|------|---------|
| Módulo | Catalog / Inventory / CartCheckout / Orders / … |
| Endpoints novos | `METHOD /api/...` |
| Commands/Queries | nomes MediatR |
| Migrations | schema afetado |
| Integrações | ex.: reserva Inventory |
| Validação | FluentValidation rules |

Se **não houver backend nesta feature**, escrever explicitamente: “Sem alteração backend”.

---

## Frontend

| Item | Detalhe |
|------|---------|
| Rotas novas | `/...` |
| Páginas/componentes | … |
| Services | real API vs stub |
| Estado | Context / React Query keys |
| `data-cy` | lista mínima |

---

## Integrações

- APIs consumidas (existentes):
- APIs **proibidas** (inexistentes):
- Contrato request/response: (linkar doc ou JSON exemplo)

---

## Testes

### Backend

- [ ] Unit tests (handlers, domain)
- [ ] Integration tests (Postgres)

### Frontend

- [ ] `npm run build`
- [ ] `npm run typecheck`
- [ ] `npm run lint` (ou documentar pré-existentes)

---

## Cypress

- [ ] Spec nova ou atualização de spec existente
- [ ] `data-cy` adicionados
- [ ] Intercepts para endpoints que **não** devem ser chamados
- [ ] Demo principal (`shopflow-demo.cy.ts`) ainda passa

---

## Documentação

- [ ] `docs/ai-context/shopflow-current-state.md` (se estado mudar)
- [ ] `docs/ai-context/next-actions.md` (se prioridade mudar)
- [ ] Doc de módulo (`docs/catalog.md`, etc.)
- [ ] `docs/testing.md` (se fluxo de teste mudar)

---

## Critérios de aceite

- [ ] …
- [ ] Build/typecheck passam
- [ ] Nenhum endpoint inexistente chamado
- [ ] Checkout convidado preservado (se aplicável)
- [ ] Admin/Vitrine não regressaram

---

## Resultado esperado

Descrever entregáveis concretos: arquivos, endpoints, specs, screenshots/vídeo demo se aplicável.

---

## Divisão sugerida de prompts

| Etapa | Template |
|-------|----------|
| UI Lovable | `02-lovable-template.md` + este escopo frontend |
| Revisão Cursor | `03-cursor-review-template.md` |
| Backend Cursor | `04-cursor-backend-template.md` |
| Cypress Cursor | `05-cursor-cypress-template.md` |
