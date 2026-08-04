# Shopflow — Smoke HML: Delivery / Fulfillment / Remessas

> Data/hora: **2026-08-01 12:19 -03**  
> Prompt: `docs/prompts/qa/hml-smoke-delivery-fulfillment-remessas-cursor.md`  
> Escopo: smoke/regressão (sem feature nova). Código e evidência HTTP prevalecem sobre docs stale.

> **Correção de alvo (2026-08-01):** este relatório mirava **HML por engano**. O ambiente correto da rodada de Delivery/Fulfillment/Remessas é **TESTE**. Decisão atualizada: [TESTE-DELIVERY-FULFILLMENT-REMESSAS-SMOKE-REPORT.md](./TESTE-DELIVERY-FULFILLMENT-REMESSAS-SMOKE-REPORT.md). O conteúdo abaixo permanece como evidência histórica do probe HML.

---

## 1. Decisão

### **BLOCKED** *(somente HML — obsoleto como decisão da feature)*

O fluxo operacional completo **não pode ser aprovado em HML** nesta rodada: a API HML pública **não expõe** os endpoints de Delivery Batch, Admin Orders (novo path) nem CEP Shopflow. Smoke manual compra → Paid → remessa → ship/deliver **não foi executável** contra HML.

O código na branch atual (local) + Cypress focado + unit tests mostram implementação avançada; o **gap é de deploy/ambiente HML**, não de ausência de código no repositório.

| Critério de aprovação | Resultado |
|----------------------|-----------|
| Checkout com entrega cria pedido | **NOT RUN** em HML (catálogo vazio + CSRF 500 + endpoints delivery ausentes) |
| Pix/pagamento confirma Paid | **NOT RUN** em HML nesta rodada |
| Admin vê pedido pago / fulfillment | **FAIL** — `GET /api/admin/orders` → **404** em HML |
| Ship/deliver individual | **NOT RUN** (endpoints admin orders 404) |
| Remessa agrupada create/ship/deliver | **FAIL** — `GET /api/admin/delivery-batches` → **404** em HML |
| Cliente/guest sem dados internos | **PASS** por inspeção de código + DTOs (não revalidado E2E HML) |
| CEP via API Shopflow | **FAIL** em HML (`/api/integrations/postal-code/br/{cep}` → **404**); **PASS** em api-teste |
| API/worker sem erro crítico | **PARTIAL** — `/health` OK; CSRF 500; vários `/api/*` novos ausentes |

---

## 2. Ambiente validado

| Papel | URL / alvo | Resultado |
|-------|------------|-----------|
| API HML | `https://api-hml.vipassessoriadigital.com.br` | `/health` → **200** `{"status":"ok","environment":"Staging"}` |
| Web HML | `https://hml.vipassessoriadigital.com.br` | **DNS não resolve** deste host (NOT RUN browser) |
| API Teste (comparativo) | `https://api-teste.vipassessoriadigital.com.br` | Endpoints novos presentes (CEP 200, admin batches **401**) |
| Local stack | `localhost:8080` / `localhost:5127` | Web+API up; usado para Cypress |
| VPS docker/logs/migrations | SSH | **NOT RUN** — sem `Host` VPS em `~/.ssh/config` nesta máquina |

### Branch / commit

| Repo | Branch | Commit | Mensagem |
|------|--------|--------|----------|
| Monorepo (backend/docs) | `develop` | `2d21a03` | `feat(orders): add delivery batch backend` |
| Frontend (`apps/web`) | `develop` | `256ff4e` | `feat(admin): add delivery batch management` |

---

## 3. Checks de ambiente (API HML)

### Health

```text
GET /health → 200 {"status":"ok","environment":"Staging"}
GET /api/catalog/products → 200 {"page":1,"pageSize":20,"total":0,"items":[]}
```

Catálogo HML **vazio** (`total:0`) — compra real na vitrine HML bloqueada mesmo sem o gap de remessas.

### Endpoints críticos (HML vs Teste)

| Endpoint | HML | Teste |
|----------|-----|-------|
| `GET /api/integrations/postal-code/br/01001000` | **404** | **200** (ViaCep, Sé/SP) |
| `GET /api/admin/delivery-batches` | **404** | **401** (existe, exige Backoffice) |
| `GET /api/admin/orders` | **404** | **401** |
| `GET /api/auth/csrf` | **500** | **200** |
| `POST /api/auth/admin/login` (body vazio) | 400 validation | 400 ProblemDetails |
| `POST /api/checkout/sessions` (body vazio) | **500** | **500** |

Interpretação: HML está com build **mais antigo** (ou sem migrate/deploy da branch com DeliveryBatch + Integrations). O 404 em rotas novas não é “ambiente transitório”; é evidência de código/rota ausente no container HML.

### Envs (VPS real)

**NOT RUN** — sem SSH. Referência apenas `deploy/.env.hml.example` (não é o `.env.hml` da VPS):

| Chave | Valor no example |
|-------|------------------|
| `PaymentsPix__Provider` | `MercadoPago` |
| `MercadoPago__Enabled` | `true` |
| `MercadoPago__Environment` | `Sandbox` |
| `MercadoPagoReconciliation__Enabled` | `true` |
| `GuestOrderAccess__Enabled` | `true` |
| `PostalCodeLookup__*` | **ausente** no example (dívida de template) |

Secrets (`AccessToken`, `WebhookSecret`, `TokenHashSecret`) **não** foram lidos/imprimidos.

### Migrations (repo — aplicadas em HML: NOT RUN)

Presentes no código:

- `20260730005106_AddOrderDeliveryFulfillment`
- `20260730005116_AddCheckoutDeliveryPreference`
- `20260801142501_AddDeliveryBatch` (+ sequence `orders.delivery_batches_batch_number_seq` START 30000)

Confirmação SQL em Postgres HML: **NOT RUN** (sem SSH).

### Worker / Caddy / Postgres HML

**NOT RUN** (`docker compose ps` / logs na VPS).

---

## 4. Backend — build e testes

| Comando | Resultado |
|---------|-----------|
| `dotnet build` (apps/api) | **PASS** (0 errors; warnings NU1903/OpenApi pré-existentes) |
| `dotnet test tests/Vls.Shopflow.Orders.UnitTests` | **PASS** 114 |
| `dotnet test tests/Vls.Shopflow.CartCheckout.UnitTests` | **PASS** 36 |
| `dotnet test tests/Vls.Shopflow.Shipping.UnitTests` | **PASS** 12 |
| `dotnet test tests/Vls.Shopflow.PaymentsPix.UnitTests` | **PASS** 88 |

Endpoints mapeados no código atual (`Program.cs`): `MapAdminOrdersEndpoints`, `MapAdminDeliveryBatchesEndpoints`, `MapIntegrationsEndpoints`.

---

## 5. Frontend — typecheck / build

| Comando | Resultado |
|---------|-----------|
| `npm run typecheck` (`apps/web`) | **PASS** |
| `npm run build` | **PASS** |

CEP FE: `apps/web/src/services/cepLookup.ts` → `GET /integrations/postal-code/br/{cep}`; comentário e testes proíbem ViaCEP direto. `rg viacep` em `apps/web/src` só em comentários/testes.

---

## 6. Cypress (local Docker → localhost)

Stack: web `:8080`, API `:5127`, image `cypress/included:13.17.0`, credenciais admin via `CYPRESS_ADMIN_*` (de `.env` local, não impressas).

| Spec | Resultado |
|------|-----------|
| `checkout-delivery-preferences.cy.ts` | **PASS** 1/1 |
| `admin-order-fulfillment.cy.ts` | **PASS** 2/2 |
| `admin-orders-list-polish.cy.ts` | **PASS** 3/3 |
| `admin-delivery-batches-list.cy.ts` | **PASS** 2/2 |
| `admin-delivery-batch-detail.cy.ts` | **PASS** 2/2 |
| `admin-create-delivery-batch-from-order.cy.ts` | **1 PASS / 1 FAIL** — timeout login (`/admin/login` não saiu) |
| `customer-orders.cy.ts` | **1 PASS / 3 pending** (sem customer creds) |
| `order-sales-display.cy.ts` | **3 PASS / 2 FAIL** — `customer-order-item-row` não encontrado |
| `checkout-csrf-pix-flow.cy.ts` | **NOT RUN** nesta rodada |

**Totais:** 21 testes · **15 pass** · **3 fail** · **3 pending**.

Falhas Cypress: flaky/auth/UI customer detail — **não** usadas para aprovar HML; delivery UI specs principais passaram com intercepts.

---

## 7. Smoke manual HML

### Compra individual

| Passo | Status |
|-------|--------|
| Produto ativo na vitrine | **FAIL** — catálogo HML `total:0` |
| Checkout + CEP + preferências entrega | **NOT RUN** |
| Pix → Paid | **NOT RUN** |
| Admin orders / fulfillment | **FAIL** — endpoint 404 |
| Ship / Deliver individual | **NOT RUN** |
| Guest/customer tracking seguro | **NOT RUN** E2E HML |

**Smoke individual: FAIL / NOT RUN**

### Remessa agrupada

| Passo | Status |
|-------|--------|
| Dois pedidos Paid elegíveis | **NOT RUN** |
| Candidates / create batch | **FAIL** — `/api/admin/delivery-batches` 404 |
| Ship / Deliver remessa | **NOT RUN** |

**Smoke remessa: FAIL**

---

## 8. Segurança

| Check | Evidência | Resultado |
|-------|-----------|-----------|
| Customer/guest DTO sem `InternalOrderNote` / `FulfillmentUpdatedByAdminId` / batch interno | `OrderDeliveryInfoDto` só campos públicos; admin detail tem campos internos (`AdminOrderDtos.cs`) | **PASS** (código) |
| Admin delivery-batches exige Backoffice | Código: `RequireAuthorization(AuthPolicies.Backoffice)`; Teste: 401 sem cookie | **PASS** (código + api-teste); HML 404 |
| CSRF em mutations admin | Middleware global; Teste CSRF 200 | **PASS** (código + teste); HML CSRF **500** (blocker operacional) |
| Guest status exige token | Código + rate limit; HML path guest não revalidado E2E | **PASS** (código) / **NOT RUN** HML |
| FE não chama ViaCEP | `cepLookup.ts` + testes | **PASS** |

**Segurança (código): PASS** · **Segurança (runtime HML): FAIL/PARTIAL** (CSRF 500 + rotas ausentes)

---

## 9. CEP

| Check | HML | Teste | Local |
|-------|-----|-------|-------|
| Endpoint Shopflow | **404** | **200** fill street/neighborhood/city/UF | **404** (API local desatualizada nesta máquina) |
| CEP inválido | N/A | **400** validation, não bloqueia contrato | N/A |
| Provider ViaCEP só no backend | — | `source:"ViaCep"` na resposta | — |

**CEP HML: FAIL** · **CEP Teste: PASS** · Template `.env.hml.example` ainda sem `PostalCodeLookup__*` (medium doc).

---

## 10. Bugs encontrados

| ID | Severidade | Descrição |
|----|------------|-----------|
| B1 | **blocker** | API HML sem rotas DeliveryBatch / Admin Orders / postal-code (404). Deploy HML atrás do código `develop`. |
| B2 | **blocker** | `GET /api/auth/csrf` → **500** em HML (antiforgery / forwarded headers — já documentado no runbook). Impede mutations cookie. |
| B3 | **high** | Catálogo HML vazio (`total:0`) — impossibilita compra smoke mesmo após redeploy de rotas. |
| B4 | **high** | Web HML DNS não resolve deste executor — smoke browser HML NOT RUN. |
| B5 | **medium** | `deploy/.env.hml.example` sem chaves `PostalCodeLookup__*`. |
| B6 | **medium** | Cypress: `admin-create-delivery-batch-from-order` flake login; `order-sales-display` customer rows. |
| B7 | **low** | Local API nesta máquina também sem CEP (404) — imagem/processo local stale vs branch. |

---

## 11. Riscos conhecidos

- Webhook MP sandbox instável → Paid depende de reconciliation worker (precisa validar env VPS após deploy).
- HML e Teste divergem de versão — Teste já tem CEP/remessas; HML não.
- Sem SSH nesta sessão: migrations/worker logs HML não auditados diretamente.
- CSRF 500 em HML pode persistir pós-redeploy se Caddy `X-Forwarded-Proto` não estiver correto.

---

## 12. Próximos passos recomendados

1. **Redeploy HML** da branch com DeliveryBatch + Integrations + Admin Orders (API + Worker) e rodar `migrate-hml`.
2. Validar na VPS: `docker compose ps`, logs api-hml/worker-hml, migrations `__EFMigrationsHistory`.
3. Corrigir CSRF HML (`ForwardedHeaders` + Caddy) até `GET /api/auth/csrf` → 200.
4. Popular catálogo HML (seed demo ou produto manual) com estoque.
5. Atualizar `deploy/.env.hml.example` (+ `.env.hml` real) com `PostalCodeLookup__*`.
6. Reexecutar este smoke: compra individual → remessa → ship/deliver → guest/customer sem internal note.
7. Re-rodar Cypress focado + `checkout-csrf-pix-flow.cy.ts` após HML estável (ou contra api-teste se política permitir).

---

## 13. Comandos executados (resumo)

```bash
# Health / probes
curl -i https://api-hml.vipassessoriadigital.com.br/health
curl https://api-hml.vipassessoriadigital.com.br/api/catalog/products
curl https://api-hml.vipassessoriadigital.com.br/api/admin/delivery-batches   # 404
curl https://api-hml.vipassessoriadigital.com.br/api/integrations/postal-code/br/01001000  # 404
curl https://api-teste.vipassessoriadigital.com.br/api/integrations/postal-code/br/01001000  # 200

# Backend
cd apps/api && dotnet build
dotnet test tests/Vls.Shopflow.Orders.UnitTests
dotnet test tests/Vls.Shopflow.CartCheckout.UnitTests
dotnet test tests/Vls.Shopflow.Shipping.UnitTests
dotnet test tests/Vls.Shopflow.PaymentsPix.UnitTests

# Frontend
cd apps/web && npm run typecheck && npm run build

# Cypress (Docker → localhost)
docker run --rm -v "$PWD/apps/web:/e2e" -w /e2e \
  --add-host=host.docker.internal:host-gateway \
  -e CYPRESS_BASE_URL=http://host.docker.internal:8080 \
  -e CYPRESS_API_URL=http://host.docker.internal:5127/api \
  -e CYPRESS_ADMIN_EMAIL=... -e CYPRESS_ADMIN_PASSWORD=... \
  cypress/included:13.17.0 \
  --spec "cypress/e2e/checkout-delivery-preferences.cy.ts,..."
```

---

## 14. Classificação final

**BLOCKED**

Não avançar para WhatsApp/chat/frete até redeploy HML + smoke manual compra/remessa verdes.
