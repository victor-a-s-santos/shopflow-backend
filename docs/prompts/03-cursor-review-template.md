# Template Cursor — Revisão de entrega Lovable

Você está atuando como engenheiro full stack sênior do Shopflow revisando uma entrega **frontend** do Lovable.

**Leia:** `docs/prompts/00-project-context.md` · `docs/ai-context/shopflow-current-state.md`

---

## Objetivo da revisão

Estabilizar a entrega frontend: build, types, rotas, navegação, Cypress, arquitetura, ausência de chamadas indevidas. **Não implementar backend** salvo se explicitamente pedido no escopo.

---

## Checklist de execução

### 1. Build, types e lint

```bash
cd apps/web && npm run build
cd apps/web && npm run typecheck   # ou npx tsc --noEmit
cd apps/web && npm run lint
```

- Corrigir erros causados pela feature.
- **Não** corrigir lint global pré-existente fora do escopo — documentar quais restam.

### 2. Rotas

Validar registro em `App.tsx` e renderização:

- Rotas novas da feature
- Rotas existentes: `/`, `/product/:slug`, `/cart`, `/checkout`, `/admin/*`

### 3. Serviços e endpoints

Procurar chamadas HTTP para:

```
/api/auth  /api/login  /api/register  /api/customers
/api/account  /api/identity  /api/orders  /api/addresses
/api/profile  /api/checkout  /api/payments  /api/pix  /api/shipping
```

- Se endpoint **não existe** no backend → remover ou stub local explícito.
- Se endpoint **existe** mas feature deveria ser stub → manter stub até integração planejada.

### 4. AuthContext / estado

- Visual-only? Sem persistência de token/senha?
- `CustomerIdentity` separado de `CheckoutCustomer`?
- Login/register não fingem produção?

### 5. Checkout convidado

- `/checkout` funciona sem login?
- `GuestCheckoutNotice` não bloqueia?
- Carrinho preservado ao navegar login → checkout?
- Checkout ainda simulado se backend não integrado?

### 6. Header / navegação

- Link login/conta correto
- Carrinho e badge funcionando
- Admin não afetado

### 7. Cypress

Rodar specs relevantes (Docker se macOS 26):

```bash
docker run --rm \
  -v "$PWD/apps/web:/e2e" -w /e2e \
  --add-host=host.docker.internal:host-gateway \
  -e CYPRESS_BASE_URL=http://host.docker.internal:8080 \
  -e CYPRESS_API_URL=http://host.docker.internal:5127/api \
  cypress/included:13.17.0 \
  --spec "cypress/e2e/<spec>.cy.ts,cypress/e2e/shopflow-demo.cy.ts"
```

- Corrigir seletores / `data-cy`
- Adicionar spec mínima se feature nova
- `setupForbiddenAuthIntercepts` / checkout intercepts quando aplicável
- Demo principal deve passar

### 8. Regressão Admin / Vitrine

- Admin Catalog CRUD
- Admin Inventory
- Vitrine + add to cart

### 9. Documentação

Atualizar se necessário:

- `docs/ai-context/shopflow-current-state.md`
- `docs/testing.md`
- Doc específica da feature

---

## Relatório final (obrigatório)

1. Build / typecheck / lint — passou ou falhou
2. Cypress — specs e resultado
3. Arquivos corrigidos
4. Bugs encontrados
5. Bugs corrigidos
6. Endpoints inexistentes encontrados
7. AuthContext visual-only confirmado?
8. Checkout convidado confirmado?
9. Admin/Vitrine ok?
10. Rotas validadas
11. Dívidas restantes
12. Roteiro manual de teste

---

## Escopo desta revisão

<!-- PREENCHER: colar resumo do que Lovable entregou -->

### Feature

…

### Rotas esperadas

…

### Componentes esperados

…

### Restrições

…

---

## Critérios de aceite

- [ ] Build passa
- [ ] TypeScript passa
- [ ] Nenhum endpoint inexistente chamado
- [ ] Checkout convidado não bloqueado
- [ ] Admin não quebrado
- [ ] Cypress atualizado/passando
- [ ] Docs refletem visual-only vs real
