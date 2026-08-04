# Shopflow — Smoke TESTE: Delivery / Fulfillment / Remessas

> Data/hora: **2026-08-01 19:35 -03**  
> Prompt: `docs/prompts/qa/hml-smoke-delivery-fulfillment-remessas-cursor.md`  
> **Alvo desta rodada: ambiente de TESTE** (não HML).  
> Escopo: smoke/regressão (sem feature nova, sem redeploy). Código e evidência HTTP prevalecem.

> Nota sobre o relatório HML anterior: [HML-DELIVERY-FULFILLMENT-REMESSAS-SMOKE-REPORT.md](./HML-DELIVERY-FULFILLMENT-REMESSAS-SMOKE-REPORT.md) mirava HML por engano. A decisão operacional desta rodada está **neste** documento.

---

## 1. Decisão

### **APPROVED WITH RISKS**

Fluxo operacional completo validado em **TESTE**: checkout com preferência de entrega → Pix Mercado Pago Sandbox → Paid (reconciliação) → admin orders → remessa agrupada ship/deliver → fulfillment individual ship/deliver → guest/public DTO sem vazamento interno → CEP via API Shopflow.

Riscos operacionais de configuração (não de funcionalidade) impedem `APPROVED` limpo: captura raw de webhook ativa e flag de reset de senha admin ligada no container de teste.

| Critério de aprovação | Resultado |
|----------------------|-----------|
| API teste expõe rotas novas | **PASS** (admin orders/batches **401** sem auth; CEP **200**) |
| CSRF teste funciona | **PASS** (`200` + cookie `__Host-shopflow_csrf` + token) |
| Catálogo teste comprável | **PASS** (`totalItems=11`) |
| Checkout cria pedido com entrega | **PASS** (preferência Carrier + data + nota cliente) |
| Pix confirma Paid | **PASS** (~15–25s via reconciliation) |
| Admin vê pedido pago / fulfillment | **PASS** |
| Ship/deliver individual | **PASS** (pedido `#10019`) |
| Remessa create/ship/deliver | **PASS** (Remessa `#30000`, pedidos `#10016` + `#10017`) |
| Cliente/guest sem dados internos | **PASS** |
| CEP via API Shopflow | **PASS** |

---

## 2. Ambiente validado

| Papel | URL / alvo | Resultado |
|-------|------------|-----------|
| API TESTE | `https://api-teste.vipassessoriadigital.com.br` | `/health` → **200** `Testing` |
| Web TESTE | `https://teste.vipassessoriadigital.com.br` | rotas `/`, `/checkout`, `/admin/login`, `/admin/orders`, `/admin/delivery-batches` → **200** |
| VPS containers | `shopflow-api-test`, `shopflow-worker-test`, `shopflow-postgres`, `shopflow-caddy` | Up (api/worker ~8h; postgres/caddy semanas) |
| HML | — | **Fora de escopo** nesta rodada (não redeployado / não revalidado) |

### Branch / commit

| Repo | Branch | Commit | Mensagem |
|------|--------|--------|----------|
| Monorepo (backend/docs) | `develop` | `2d21a03` | `feat(orders): add delivery batch backend` |
| Frontend (`apps/web`) | `develop` | `256ff4e` | `feat(admin): add delivery batch management` |

---

## 3. Containers / runtime TESTE

`docker compose ps` (VPS `/opt/shopflow/app/deploy`):

| Container | Status |
|-----------|--------|
| `shopflow-api-test` | Up |
| `shopflow-worker-test` | Up |
| `shopflow-postgres` | Up (healthy) |
| `shopflow-caddy` | Up |
| `shopflow-api-hml` | Up (ignorado nesta rodada) |

### Envs principais (mascarados)

| Chave | Valor observado |
|-------|-----------------|
| `ASPNETCORE_ENVIRONMENT` | `Testing` |
| `AllowedOrigins__0` | `https://teste.vipassessoriadigital.com.br` |
| `PaymentsPix__Provider` | `MercadoPago` |
| `MercadoPago__Enabled` | `true` |
| `MercadoPago__Environment` | `Sandbox` |
| `MercadoPago__NotificationUrl` | webhook api-teste |
| `MercadoPagoReconciliation__Enabled` | `true` |
| `GuestOrderAccess__Enabled` | `true` |
| `MercadoPago__WebhookRawCaptureEnabled` | **`true`** (risco — ver §21) |
| `SHOPFLOW_ADMIN_RESET_PASSWORD` | **`true`** (risco — ver §21) |
| Secrets (`AccessToken`, `WebhookSecret`, `TokenHashSecret`, senhas) | presentes, **não impressos** |

CEP: endpoint público respondeu ViaCEP com preenchimento (provider ativo em runtime mesmo sem dump completo de `PostalCodeLookup__*` nesta rodada).

---

## 4. Rotas novas / probes

| Endpoint | Resultado |
|----------|-----------|
| `GET /health` | **200** |
| `GET /api/auth/csrf` | **200** + `Set-Cookie: __Host-shopflow_csrf` |
| `GET /api/admin/orders` (sem auth) | **401** |
| `GET /api/admin/delivery-batches` (sem auth) | **401** |
| `GET /api/admin/orders/{id}/delivery-batch-candidates` (sem auth) | **401** |
| `GET /api/integrations/postal-code/br/01001000` | **200** `found=true` (Sé/SP) |
| `GET /api/integrations/postal-code/br/000` | **400** validation |
| `GET /api/orders/guest/{id}/status` sem token | **401** `INVALID_GUEST_ORDER_TOKEN` |
| `GET /api/catalog/products?page=1&pageSize=16` | **200** `totalItems=11` |

---

## 5. Migrations (banco `shopflow_test`)

Confirmadas em schema `orders` / `cartcheckout`:

| Migration | Presente |
|-----------|----------|
| `AddDeliveryBatch` (`20260801142501`) | **SIM** |
| `AddOrderDeliveryFulfillment` (`20260730005106`) | **SIM** |
| `AddCheckoutDeliveryPreference` (`20260730005116`) | **SIM** |
| `AddGuestOrderAccessTokens` / `AddCustomerUserIdToOrders` / `AddOrderNumberToOrders` | **SIM** |

Tabelas: `orders.delivery_batches`, `orders.delivery_batch_orders`.

Colunas pedido: `PreferredDeliveryMethod/Date`, `CustomerOrderNote`, `InternalOrderNote`, `FulfillmentStatus`, `FinalDeliveryMethod`, `TrackingCode`, `ShippedAt`, `DeliveredAt`, `FulfillmentUpdatedAt`, `FulfillmentUpdatedByAdminId`.

Colunas checkout session: `PreferredDeliveryMethod/Date`, `CustomerOrderNote`.

Históricos EF em schemas `catalog` / `paymentspix` / etc. usam naming distinto nesta VPS (query por schema literal falhou); não bloqueou o smoke — catálogo/Pix operacionais.

---

## 6. Build / testes locais

| Check | Resultado |
|-------|-----------|
| Frontend `npm run typecheck` | **PASS** |
| Frontend `npm run build` | **PASS** (rodadas anteriores nesta sessão; revalidação longa em background não foi pré-requisito do smoke remoto) |
| Backend `dotnet build` HttpApi | **PASS** (sessão anterior) |
| `Vls.Shopflow.Orders.UnitTests` | **PASS** (sessão anterior; 114 testes) |
| Cypress focado delivery/remessa | Specs presentes (`admin-order-fulfillment.cy.ts`, `admin-create-delivery-batch-from-order.cy.ts`, `admin-delivery-batches-list.cy.ts`, `admin-delivery-batch-detail.cy.ts`, `checkout-delivery-preferences.cy.ts`, `checkout-csrf-pix-flow.cy.ts`); **não reexecutados contra TESTE nesta rodada** (smoke manual API cobriu o caminho crítico) |

---

## 7. Catálogo TESTE

- `totalItems=11`
- SKU smoke: `08d9cec6-d74a-4aad-ad37-7928b8b63f81` (Camiseta Básica Algodão)
- Preço / estoque suficientes para checkout + Pix

---

## 8. Worker / Pix TESTE

- Worker `shopflow-worker-test` Up
- `MercadoPagoReconciliation__Enabled=true`
- Pedidos smoke foram a **Paid** com Pix `Pending` → `Paid` em ~15–25s (webhook e/ou reconciliation; comportamento compatível com Sandbox + worker)

Pedidos Paid desta rodada:

| Pedido | Uso |
|--------|-----|
| `#10016` | Remessa (cliente A) |
| `#10017` | Remessa (mesmo cliente A) |
| `#10019` | Fulfillment individual |

---

## 9. Smoke individual (fulfillment)

Pedido `#10019`:

1. Checkout session + `POST /api/orders/from-checkout-session` com Carrier + data + nota  
2. `POST /api/payments/pix/orders/{id}` → Pending → Paid  
3. Admin: `PUT .../internal-note` → `POST .../fulfillment/ship` → `POST .../fulfillment/deliver`  
4. Guest status: `fulfillmentStatus=Delivered`, `trackingCode=INDIV-TRACK-1`, **sem** campos internos/batch  

**PASS**

---

## 10. Smoke remessa

Mesmo cliente (email/telefone) pedidos `#10016` + `#10017`:

1. `GET .../delivery-batch-candidates` → 2 pedidos; `oid2` presente; flag de endereços diferentes observada  
2. `POST /api/admin/delivery-batches` → **201** Remessa `#30000` (`AwaitingShipment`)  
3. `POST .../ship` → `Shipped`  
4. `POST .../deliver` → `Delivered`  
5. Admin detalhe ambos: `fulfillmentStatus=Delivered`, `deliveryBatchNumber=30000`, tracking `TESTE-SMOKE-TRACK-001`  
6. Guest/public: `Delivered` + tracking; keys públicas apenas; **leak=false**  

**PASS**

---

## 11. Segurança

| Check | Resultado |
|-------|-----------|
| Admin endpoints exigem Backoffice | **PASS** (401) |
| Guest status exige token | **PASS** (401) |
| Guest/public DTO sem `internalOrderNote` / batch ids / `fulfillmentUpdatedByAdminId` | **PASS** |
| Admin DTO contém campos operacionais (`internalOrderNote`, `fulfillmentUpdatedByAdminId`, `deliveryBatchNumber`) | **PASS** (esperado) |
| Mutation admin sem CSRF | rejeitada (`400` antiforgery nesta rota; com CSRF → `200`) |
| FE CEP não chama ViaCEP direto | **PASS** por código (`apps/web/src/services/cepLookup.ts` → `/integrations/postal-code/br/{cep}`) |

---

## 12. CEP

| Caso | Resultado |
|------|-----------|
| CEP válido `01001000` | **200** fill (rua/bairro/cidade/UF) |
| CEP inválido curto | **400** validation (não 404) |
| Frontend usa API Shopflow | **PASS** (código) |

---

## 13. Bugs encontrados

| ID | Severidade | Descrição |
|----|------------|-----------|
| CFG-001 | **high** (ops) | `MercadoPago__WebhookRawCaptureEnabled=true` no api-test — captura temporária não deveria ficar ligada |
| CFG-002 | **medium** (ops) | `SHOPFLOW_ADMIN_RESET_PASSWORD=true` no api-test — deixa reset de senha no seed a cada restart |
| OPS-001 | **medium** (ops) | `deploy/.env.test` local tem placeholder `CHANGE_ME`; senha real só na VPS — dificulta smoke sem SSH |
| SEC-001 | **high** (ops) | Durante QA, dump de env com `sed` case-sensitive falhou em mascarar `SHOPFLOW_ADMIN_PASSWORD` — **rotar senha admin de TESTE** |

Nenhum blocker funcional de Delivery/Fulfillment/Remessas no runtime TESTE.

---

## 14. Riscos conhecidos

1. Webhook raw capture ainda ativo em TESTE.  
2. Reset password admin ligado.  
3. Cypress delivery/remessa não reexecutado contra o front TESTE nesta rodada.  
4. HML permanece desatualizado (fora de escopo aqui).  
5. Credencial admin de TESTE deve ser rotacionada após exposição acidental no output de diagnóstico.

---

## 15. Próximos passos recomendados

1. Na VPS `.env.test`: `MercadoPago__WebhookRawCaptureEnabled=false`, `SHOPFLOW_ADMIN_RESET_PASSWORD=false`; **rotar** `SHOPFLOW_ADMIN_PASSWORD`; recreate `api-test`/`worker-test` (sem tocar HML).  
2. Alinhar `deploy/.env.test` local (sem commit de secret) ou documentar onde obter credencial de smoke.  
3. Rodar Cypress focado (`admin-order-fulfillment`, `admin-*-delivery-batch*`, `checkout-delivery-preferences`) apontando API/front TESTE.  
4. Só depois promover o mesmo build a HML (prompt separado).

---

## 16. Evidências resumidas

```text
GET /health → 200 Testing
GET /api/auth/csrf → 200
GET /api/admin/orders → 401
GET /api/admin/delivery-batches → 401
GET /api/integrations/postal-code/br/01001000 → 200 found
Orders Paid: #10016, #10017, #10019
Remessa #30000: create → ship → deliver (2 pedidos Delivered)
Individual #10019: ship → deliver
Guest DTO: Delivered + tracking; leak=false
```
