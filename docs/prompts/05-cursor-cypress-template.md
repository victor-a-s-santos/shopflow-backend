# Template Cursor — Cypress E2E

Você está implementando ou atualizando testes E2E Cypress no Shopflow.

**Leia:** `docs/testing.md` · `docs/ai-context/shopflow-current-state.md`

---

## Contexto Cypress no Shopflow

- **Localização:** `apps/web/cypress/`
- **Config:** `cypress.config.cjs`
- **Support:** `cypress/support/commands.ts`, `e2e.ts`
- **Fixtures:** `cypress/fixtures/demo-product.json`
- **Execução macOS 26:** Docker `cypress/included:13.17.0` + `host.docker.internal:8080`

---

## Regras

1. **`data-cy` estáveis** — preferir sobre classes CSS ou texto frágil.
2. **Specs por área** — admin, vitrine, checkout, identity, demo.
3. **Spec demo completo** — `shopflow-demo.cy.ts` deve continuar passando após mudanças.
4. **Intercepts** — bloquear endpoints que **não devem** ser chamados no modo atual:
   - Checkout simulado: `POST **/api/checkout/**`, `/api/orders/**`, `/api/payments/**`
   - Auth visual: `POST **/api/auth/**`, `/api/identity/**`, `/api/customers/**`, etc.
   - Quando integrar checkout real: **remover** bloqueio de `/api/checkout/sessions` e ajustar asserts.
5. **Evitar flaky tests** — sem `cy.wait(ms)` fixo; usar `.should()`; evitar `.within()` longo com re-renders React (preferir seletores scoped `[data-cy="step"] #field`).
6. **Botões ambíguos** — checkout tem “Continuar” e “Continuar como convidado”; usar `data-cy="checkout-contact-continue"` etc.
7. **Sistema real local** — seed via API (`cy.ensureDemoProduct()`), não mock de Catalog/Inventory.
8. **Checkout honesto** — enquanto pagamento/pedido não existirem, validar mensagem de simulação ou sessão parcial conforme estado do projeto.
9. **Atualizar `docs/testing.md`** — tabela de specs, limitações, comandos Docker.

---

## Comandos customizados existentes

| Comando | Uso |
|---------|-----|
| `cy.ensureDemoProduct()` | Seed idempotente produto + estoque |
| `cy.resetAppState()` | Limpa localStorage/cookies |
| `cy.continueAsGuestAtCheckout()` | Clica convidado se notice visível |
| `cy.setupForbiddenAuthIntercepts()` | Rastreia calls auth proibidas |
| `cy.assertNoForbiddenAuthCalls()` | Assert zero calls |
| `cy.setupForbiddenCheckoutIntercepts()` | Rastreia checkout/orders/payments |
| `cy.assertNoForbiddenCheckoutCalls()` | Assert zero calls |
| `cy.demoPause()` / `cy.demoSectionPause()` | Demo lento cliente |

---

## Estrutura de specs

| Spec | Responsabilidade |
|------|------------------|
| `shopflow-demo.cy.ts` | Demo completo — **não quebrar** |
| `shopflow-demo-client.cy.ts` | Demo lento + vídeo |
| `catalog-admin.cy.ts` | Admin produtos |
| `inventory-admin.cy.ts` | Admin estoque |
| `storefront-cart.cy.ts` | Carrinho localStorage |
| `checkout-simulated.cy.ts` | Checkout (simulado ou integrado) |
| `identity-customer.cy.ts` | Auth visual + guest checkout |

---

## Rodar testes

```bash
# Stack
docker compose up -d

# Specs (Docker)
docker run --rm \
  -v "$PWD/apps/web:/e2e" -w /e2e \
  --add-host=host.docker.internal:host-gateway \
  -e CYPRESS_BASE_URL=http://host.docker.internal:8080 \
  -e CYPRESS_API_URL=http://host.docker.internal:5127/api \
  cypress/included:13.17.0 \
  --spec "cypress/e2e/shopflow-demo.cy.ts"

# Demo vídeo cliente
cd apps/web && npm run e2e:demo:client
```

---

## Checklist de entrega

- [ ] `data-cy` adicionados nos componentes tocados
- [ ] Spec(s) nova(s) ou atualizada(s)
- [ ] Demo principal passa
- [ ] Intercepts coerentes com estado real (simulado vs integrado)
- [ ] `docs/testing.md` atualizado
- [ ] Vídeo gerado (se pedido demo cliente)

---

## Escopo desta tarefa

<!-- PREENCHER -->

### Objetivo

…

### Specs afetadas

…

### Endpoints permitidos / proibidos

…

### Novos `data-cy`

…

---

## Critérios de aceite

- [ ] Cypress verde no Docker
- [ ] Sem waits frágeis desnecessários
- [ ] Documentação de testes atualizada
