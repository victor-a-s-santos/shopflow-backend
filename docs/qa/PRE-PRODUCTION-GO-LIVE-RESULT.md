# PRE-PRODUCTION GO-LIVE — Resultado da validação

> Data/hora (reexecução): **2026-08-02 06:46 -03**  
> Checklist oficial: [PRE-PRODUCTION-GO-LIVE-CHECKLIST.md](./PRE-PRODUCTION-GO-LIVE-CHECKLIST.md)  
> Ambiente validado: **TESTE / pré-produção** (`api-teste` + `teste.vipassessoriadigital.com.br`)  
> Produção real: **não existe / não validada nesta rodada**  
> Smoke Delivery/Remessas de referência: [TESTE-DELIVERY-FULFILLMENT-REMESSAS-SMOKE-REPORT.md](./TESTE-DELIVERY-FULFILLMENT-REMESSAS-SMOKE-REPORT.md)

---

## Decisão final

### **BLOCKED** *(para liberar produção)*

TESTE operacional permanece **APPROVED WITH RISKS**: flags perigosas desligadas e senha admin rotacionada; checklist completo; rotas mínimas OK; Cypress parcial (1 falha de login admin em create-batch). **WhatsApp Pages ainda sem número real** (bundle com `phone:void 0`). Produção continua **BLOCKED**.

| Papel | Decisão |
|-------|---------|
| Runtime TESTE (compra → Pix → Orders → Fulfillment → Remessas) | **APPROVED WITH RISKS** |
| Go-live **Produção** | **BLOCKED** |

---

## Reexecução pós-correção de blockers

1. **Data/hora:** 2026-08-02 ~00:18 UTC (VPS) / 06:46 -03 (relatório).
2. **Ambiente:** TESTE (`/opt/shopflow/app/deploy`).
3. **Commits:** backend `develop` @ `2d21a03`; frontend `062b388`.
4. **Flags corrigidas (container api-test):**
   - `MercadoPago__WebhookRawCaptureEnabled=false`
   - `SHOPFLOW_ADMIN_RESET_PASSWORD=false`
   - HML `.env.hml` também forçado a `false` (arquivo; containers HML não recriados).
5. **Senha admin TESTE rotacionada:** **sim** — login 200 após reset e após `RESET=false`; valor só em `/root/.shopflow_admin_teste_password_tmp` (chmod 600) na VPS; **não** em docs/git.
6. **WhatsApp env/Pages validado:** **não** — bundle publicado `index-FeRdHvJ1.js` tem código CTA mas `phone:void 0` / sem `wa.me/<digits>`; falta número real + rebuild Cloudflare Pages. Examples no repo usam placeholder `55DDDNUMERO` / `ENABLED=false`.
7. **Checklist corrigido:** **sim** — `docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md`; stub em `docs/prompts/qa/`.
8. **Cypress (Docker `cypress/included:13.17.0` → local `:8080`/`:5127`):**
   - **PASS:** `whatsapp-contact` (3), `checkout-delivery-preferences` (1), `checkout-csrf-pix-flow` (2), `customer-orders` (1 pass / 3 pending), `admin-orders-list-polish` (3), `admin-order-fulfillment` (2), `admin-delivery-batches-list` (2), `admin-delivery-batch-detail` (2).
   - **FAIL:** `admin-create-delivery-batch-from-order` — timeout em `before each` (ficou em `/admin/login`; credenciais locais de exemplo vs ambiente).
9. **DataProtection/logs:** volume `dataprotection_test` montado; key XML presente; `DataProtection__KeysPath=/app/dataprotection-keys`. API subiu após recreate; log de seed `Admin password reset for admin-teste@...`. Caddy teve 502 pontual no recreate (esperado) e warn ACME em api-hml.
10. **Rotas mínimas TESTE:** health **200**; csrf **200** + `__Host-shopflow_csrf`; admin orders/batches **401**; CEP **200**; catalog **200** (`totalItems=11`).
11. **Bugs remanescentes:** FE-WA-001 (Pages sem telefone); Cypress create-batch login flake/fail local; Production não validada; NU1903 Crypto.Xml.
12. **Riscos remanescentes:** WhatsApp TESTE inútil até rebuild Pages; HML desatualizado; MP Production never exercised; senha nova só na VPS (ops deve recuperar/armazenar no cofre).
13. **Decisão atualizada:** TESTE **APPROVED WITH RISKS**; Produção **BLOCKED**.

---

## 1. Código, branches e versionamento

| Item | Resultado | Evidência |
|------|-----------|-----------|
| Branch/commit backend | **PASS** | `develop` @ `2d21a03` — `feat(orders): add delivery batch backend` |
| Branch/commit frontend | **PASS** | `develop` @ `062b388` — `feat(storefront): add whatsapp seller contact` |
| `dotnet build` HttpApi | **PASS** | build local OK nesta rodada |
| Unit tests Orders | **PASS** | 114/114 |
| Unit tests CartCheckout | **PASS** | 36/36 |
| Unit tests Shipping | **PASS** | 12/12 |
| Unit tests PaymentsPix | **PASS** | 88/88 |
| Unit tests Inventory | **PASS** | 43/43 |
| Unit tests Catalog | **PASS** | 137/137 |
| Unit tests IdentityAccess | **NOT RUN** | projeto `*.UnitTests` inexistente; há só `IdentityAccess.IntegrationTests` |
| Falhas sandbox/MSBuild | **PASS** (documentado) | jobs anteriores falharam com `SocketException` named pipes; reexecução com permissões adequadas OK |
| `npm run typecheck` | **PASS** | |
| `npm run build` | **PASS** | vite build ~2.4s |
| Cypress focado | **PARTIAL** | Docker local 2026-08-02: maioria PASS; 1 FAIL `admin-create-delivery-batch-from-order` (login) — ver §6 / reexecução |
| Mock permanente / endpoint hardcoded de teste no FE prod | **NOT RUN** auditoria completa | build local OK; front TESTE publicado não re-auditado linha a linha |

Warnings: vários `NU1903` em `System.Security.Cryptography.Xml` 10.0.8 (risco de dependência — ver § Bugs/Riscos).

---

## 2. Variáveis de ambiente

### Backend API TESTE (observado)

| Item | Resultado |
|------|-----------|
| `ASPNETCORE_ENVIRONMENT=Testing` | **PASS** para TESTE / **FAIL** como proxy de Production |
| Connection strings Postgres | **PASS** (presentes, mascaradas) |
| CORS `AllowedOrigins__0=https://teste.vipassessoriadigital.com.br` | **PASS** |
| CSRF | **PASS** (`GET /api/auth/csrf` 200 + cookie `__Host-shopflow_csrf`) |
| Guest access enabled + TTL + rate limit | **PASS** |
| `GuestOrderAccess__TokenHashSecret` | **PASS** (SET) |
| DataProtection volume/persistência | **PASS** — volume `dataprotection_test` → `/app/dataprotection-keys`; key XML presente |
| Cookies `Secure=true` / rate limits genéricos | **PARTIAL** — CSRF cookie `Secure`; rate limits guest/CEP evidenciados por políticas; auditoria Cookie__ completa **NOT RUN** |
| Logs sem secrets | **PASS** (parcial) — seed reset log sem senha; raw capture off; sem dump de AccessToken/WebhookSecret nos trechos inspecionados |

### Mercado Pago / Pix (TESTE)

| Item | Resultado |
|------|-----------|
| `PaymentsPix__Provider=MercadoPago` | **PASS** |
| `MercadoPago__Enabled=true` | **PASS** |
| `MercadoPago__Environment=Sandbox` | **PASS** (correto para TESTE; Production MP **NOT RUN**) |
| AccessToken / WebhookSecret | **PASS** (SET) |
| NotificationUrl → api-teste webhook | **PASS** |
| Reconciliation enabled + worker up | **PASS** |
| `MercadoPago__WebhookRawCaptureEnabled=false` | **PASS** (reexecução 2026-08-02 — container `false`) |
| `SHOPFLOW_ADMIN_RESET_PASSWORD=false` | **PASS** (reexecução 2026-08-02 — container `false`) |

### Produção (API/MP/CORS/DataProtection)

| Item | Resultado |
|------|-----------|
| Todos os itens “validar em produção” do checklist | **NOT RUN** — sem ambiente Production |

### CEP

| Item | Resultado |
|------|-----------|
| Endpoint `/api/integrations/postal-code/br/{cep}` | **PASS** (200 found / 400 inválido) |
| Frontend não chama ViaCEP direto | **PASS** (código `cepLookup.ts` → API Shopflow) |
| `PostalCodeLookup__*` no container | **NOT RUN** dump vazio; comportamento runtime OK |

### WhatsApp

| Item | Resultado |
|------|-----------|
| Código FE (`WhatsAppContactButton`, `support.ts`, env `VITE_SUPPORT_WHATSAPP_*`) | **PASS** no repo @ `062b388` |
| Spec Cypress `whatsapp-contact.cy.ts` | **PASS** (local Docker, 3/3) — não valida Pages publicado |
| Bundle JS publicado TESTE contém WhatsApp (`wa.me`, “falar com vendedor”) | **PARTIAL** — componente presente; `phone:void 0` (CTA efetivo ausente) |
| Placeholder `5511999999999` removido | **PASS** nos examples (agora `55DDDNUMERO` + `ENABLED=false`) |
| Número real (não placeholder) no deploy TESTE/PROD | **FAIL** — Pages TESTE sem `VITE_SUPPORT_WHATSAPP_PHONE` no build |

---

## 3. Containers / migrations / estoque / catálogo (TESTE)

| Item | Resultado |
|------|-----------|
| `shopflow-api-test` / `worker-test` / `postgres` / `caddy` Up | **PASS** |
| Migrations DeliveryBatch + OrderDeliveryFulfillment + CheckoutDeliveryPreference | **PASS** |
| Catálogo comprável | **PASS** (`totalItems=11`, 11 ativos na página) |
| Estoque | **PASS** — 102 itens com `QuantityOnHand>0`; SKU smoke Camiseta `17` on hand / `0` reserved |
| Admin inventory unauth | **PASS** segurança → **401** |

---

## 4. Smoke funcional (TESTE) — herdado + reconfirmado

| Fluxo | Resultado |
|-------|-----------|
| Health / CSRF / rotas admin novas | **PASS** (reprobe 2026-08-02 08:22 — health/csrf 200) |
| Checkout + entrega + Pix → Paid | **PASS** (smoke Delivery) |
| Admin orders + fulfillment individual | **PASS** (`#10019`) |
| Remessa create/ship/deliver | **PASS** (`#30000` com `#10016`+`#10017`) |
| Guest/public DTO sem dados internos | **PASS** |
| CEP | **PASS** |
| Novo smoke compra nesta rodada go-live | **NOT RUN** (não repetido; evidência do smoke ~1h antes reutilizada) |

---

## 5. Segurança

| Item | Resultado |
|------|-----------|
| Admin orders/batches/inventory → 401 sem cookie | **PASS** |
| Guest status sem token → 401 | **PASS** |
| Guest não vaza internal/batch | **PASS** |
| Mutation admin sem CSRF rejeitada | **PASS** (smoke anterior) |
| Rotação senha admin TESTE após vazamento em dump QA | **PASS** (reexecução 2026-08-02; login 200; reset flag off) |

---

## 6. Cypress

| Item | Resultado |
|------|-----------|
| Specs delivery/fulfillment/remessa/checkout/whatsapp presentes | **PASS** (existência) |
| Execução contra TESTE ou local nesta rodada | **PARTIAL** — Docker local: maioria PASS; 1 FAIL create-batch (login) |

---

## 7. Rollback

| Item | Resultado |
|------|-----------|
| Plano documentado | **PASS** — `docs/infra/RUNBOOK-004-github-actions-vps-deploy.md` §11; `DEPLOY-003` §18 (API/worker recreate; FE Cloudflare Pages; sem `compose down`/wipe volumes; DB forward-only) |
| Drill de rollback executado | **NOT RUN** |

---

## 8. Itens PASS (resumo)

1. Backend/frontend commits anotados; builds locais OK.  
2. Unit tests Orders, CartCheckout, Shipping, PaymentsPix, Inventory, Catalog.  
3. FE typecheck + build.  
4. API/worker/postgres/caddy TESTE up.  
5. Migrations de delivery/checkout aplicadas.  
6. Catálogo + estoque suficientes.  
7. CSRF, CEP, guest token, admin 401.  
8. Pix Sandbox + reconciliation (evidência Paid).  
9. Orders admin + fulfillment individual + remessas.  
10. DTO seguro guest/public.  
11. Código WhatsApp presente no FE.  
12. Plano de rollback documentado.
13. Flags perigosas `false` no api-test; senha admin TESTE rotacionada.
14. Checklist oficial completo em `docs/qa/`.
15. DataProtection volume + keys confirmados.

---

## 9. Itens FAIL

1. **WhatsApp Pages TESTE:** build sem telefone (`phone:void 0`); CTA real não smokeado no front publicado.
2. **Ambiente Production** não configurado/validado.
3. **Cypress** `admin-create-delivery-batch-from-order`: falha de login admin no ambiente local desta rodada.

## 10. Itens NOT RUN

1. Checklist completo de produção (smoke prod, MP live).
2. Mercado Pago **Production** (token/live).
3. IdentityAccess unit tests (projeto ausente).
4. Drill de rollback.
5. Smoke compra repetido nesta hora (reuso do smoke Delivery).
6. HML recreate após flags (só arquivo `.env.hml` corrigido).
7. UI WhatsApp no front **publicado** TESTE com número real.

## 11. Bugs encontrados

| ID | Severidade | Descrição |
|----|------------|-----------|
| CFG-001 | **resolved** (TESTE) | Webhook raw capture → `false` no api-test |
| CFG-002 | **resolved** (TESTE) | `SHOPFLOW_ADMIN_RESET_PASSWORD` → `false` no api-test |
| FE-WA-001 | **high** (go-live UX) | Pages TESTE sem `VITE_SUPPORT_WHATSAPP_*` reais; CTA inútil |
| DOC-001 | **resolved** | Checklist oficial em `docs/qa/` completo |
| DEP-001 | **medium** | Warnings `NU1903` System.Security.Cryptography.Xml 10.0.8 |
| SEC-001 | **resolved** (TESTE) | Senha admin TESTE rotacionada; reset desligado |
| CY-001 | **low** | Cypress create-batch: login admin timeout (local/example creds) |

---

## 12. Riscos

1. Produção ainda sem env/MP live/DataProtection/CORS/WhatsApp validados — go-live **BLOCKED**.  
2. Front TESTE publicado sem `VITE_SUPPORT_WHATSAPP_PHONE` — CTA WhatsApp inútil até rebuild Pages com número real.  
3. Senha admin nova só em arquivo root na VPS até copiar para cofre e apagar.  
4. Dependência criptográfica com advisory alto (`NU1903`).  
5. 1 spec Cypress create-batch falhou por login local (não bloqueia smoke TESTE já evidenciado).  
6. Pasta única VPS compartilha código entre test/hml (limitação conhecida do runbook).

---

## 13. Próximos passos

1. **Cloudflare Pages TESTE:** setar `VITE_SUPPORT_WHATSAPP_ENABLED=true` + telefone real; rebuild; smoke CTA em PDP/checkout/pedido.  
2. Guardar senha admin nova do cofre VPS (`/root/.shopflow_admin_teste_password_tmp`) e apagar o arquivo após copiar.  
3. Re-rodar Cypress `admin-create-delivery-batch-from-order` com credenciais locais corretas.  
4. Atualizar `System.Security.Cryptography.Xml` / revisar NU1903.  
5. Preparar **Production**: env Production, DataProtection, CORS real, MP live, NotificationUrl prod, WhatsApp final, smoke controlado, drill rollback.  
6. Recreate HML se for usar o ambiente (flags já `false` no arquivo).

---

## 14. Resposta executiva (pedido)

1. **PASS:** flags perigosas off no TESTE; senha admin rotacionada; checklist completo; DataProtection volume; rotas mínimas; typecheck/build FE; Cypress guest + maioria admin.  
2. **FAIL:** WhatsApp Pages sem número; Production não validada; 1 spec Cypress create-batch.  
3. **NOT RUN:** MP Production, drill rollback, smoke compra novo, UI WhatsApp publicada com número real.  
4. **Bugs:** FE-WA-001 aberto; CFG/SEC/DOC resolvidos nesta reexecução.  
5. **Decisão:** **BLOCKED** para produção; TESTE **APPROVED WITH RISKS**.  
6. **Próximos passos:** ver §13.
