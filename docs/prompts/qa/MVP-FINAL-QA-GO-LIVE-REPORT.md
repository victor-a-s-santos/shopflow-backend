# Shopflow MVP — Relatório Final de QA / Go-Live

> Data da auditoria: **2026-07-17**  
> Escopo: validação (sem feature nova) — docs, envs, rotas, migrations, testes, workers, Mercado Pago, segurança e riscos.  
> Prompt: `docs/qa/mvp-final-qa-go-live-checklist-cursor.md`  
> Código prevalece sobre docs quando houver divergência.

---

## 1. Resumo executivo

O MVP de venda (vitrine → carrinho → checkout → Pix Mercado Pago → Order Paid via webhook **ou** reconciliação Worker → Admin/Customer Orders) está **implementado e compilável**. Auth admin/customer, CSRF, guest order token, migrations de Orders/PaymentsPix e workers de expiração + reconciliação existem no código.

A entrega **HML/teste** é viável **desde que** Mercado Pago Sandbox + reconciliação estejam ligados nas envs reais da VPS (os `.example` ainda vêm com `Fake` / reconciliation `false`). **Produção de venda real** ainda exige template/checklist de prod, credenciais Production, e smoke com valor real — não há `deploy/.env.prod.example` nem job de deploy prod no GitHub Actions.

Webhook MP com falha de assinatura em sandbox permanece risco operacional **mitigado** pela reconciliação `GET /v1/orders` (não bloqueia MVP HML se o worker estiver ativo).

---

## 2. Status geral

**READY WITH RISKS**

| Ambiente | Veredito |
|----------|----------|
| Local / CI unitário | Pronto (build + unit tests verdes nesta auditoria) |
| Test / HML | Pronto **com riscos** — depende de config MP + reconciliation ON + smoke manual |
| Produção (venda real) | **Não pronto** sem checklist prod, secrets Production e compra real controlada |

## 3. Percentual estimado de prontidão

| Fatia | % |
|-------|---|
| Código MVP (fluxo compra + Paid + orders) | ~90% |
| Docs alinhadas ao código | ~70% (feature docs OK; AI-context stale) |
| Envs / deploy HML | ~75% (exemplos incompletos para go-live MP) |
| Envs / deploy produção | ~40% (sem template/job prod) |
| Testes automatizados E2E reais (MP sandbox) | ~50% (Cypress existe; não executado nesta auditoria) |
| **Prontidão HML go-live** | **~78%** |
| **Prontidão produção venda** | **~55%** |

---

## 4. Fluxos validados

Validação = inspeção de código + docs + testes unitários (não smoke E2E real contra MP nesta sessão).

| Fluxo | Status | Evidência |
|-------|--------|-----------|
| Catálogo público + detalhe produto | OK (código + FE) | `CatalogEndpoints`, `App.tsx` `/` + `/product/:slug` |
| Carrinho local | OK | FE `CartContext` |
| CheckoutSession + reserva estoque | OK | `CheckoutEndpoints` + unit CartCheckout |
| Create order from session (+ CustomerUserId se cookie) | OK | `OrdersEndpoints` + `CreateOrderFromCheckoutSessionCommandHandler` + Orders unit |
| Guest status com `X-ORDER-ACCESS-TOKEN` | OK | endpoint + rate limit + unit guest |
| Pix create (Orders API) | OK (código) | `MercadoPagoPixPaymentProvider` / `POST v1/orders` |
| Paid só `processed` + `accredited` | OK | `MercadoPagoOrderStatusRules.IsPaid` |
| Reconciliação Worker fallback | OK (código) | `MercadoPagoPixReconciliationWorker` + processor |
| Expiração checkout/order/pix | OK (código + unit) | `ExpirationProcessor` |
| Admin Orders list/detail | OK | `AdminOrdersEndpoints` Backoffice + DTOs sem QR |
| Customer Orders list/detail | OK | `CustomerOrdersEndpoints` + filtro só `CustomerUserId` |
| Admin / Customer auth cookies | OK | IdentityAccess + guards FE |
| Raw capture Production gate | OK | `MercadoPagoWebhookRawCapture` `IsProduction()` |

## 5. Fluxos não validados

| Fluxo | Motivo |
|-------|--------|
| Pagamento Pix sandbox → Paid ponta a ponta | Requer VPS/HML + MP + worker; **NOT RUN** |
| Webhook MP com assinatura válida em sandbox | Dívida operacional conhecida; dependente de secret/app |
| Compra real Production | Sem ambiente prod configurado no repo |
| Cypress E2E (admin/customer orders, pix, auth) | Requer stack API + browser; **NOT RUN** nesta sessão |
| Integração tests que precisam Postgres (`SHOPFLOW_TEST_DB`) | Não provisionado aqui |
| Frete / e-mail / cancelamento / reembolso / claim guest | Fora do MVP (não implementados) |
| Dashboard admin métricas reais | Fake hardcoded (dívida conhecida) |

---

## 6. Rotas backend validadas

| Grupo | Rotas | Auth | Notas |
|-------|-------|------|-------|
| Health | `GET /health` | Anônimo | OK |
| Catalog público | products/categories/attributes/by-slug | Anônimo | Mutações Backoffice |
| Inventory storefront | availability | Anônimo (safe) | Reserve/confirm/cancel **só** `/api/admin/inventory/*` Backoffice |
| Checkout | `POST/GET /api/checkout/sessions`, cancel | Anônimo | GET sem PII |
| Orders | `POST /api/orders/from-checkout-session` | Anônimo (+ CustomerCookie opcional) | **RISK:** response com PII completa + guest token one-shot |
| Orders | `GET /api/orders/guest/{orderId}/status` | Anônimo + header token + rate limit | Customer mascarado — OK |
| Orders | `GET /api/orders/{id}` | Backoffice | Full PII |
| Admin Orders | `GET /api/admin/orders`, `/{orderId}` | Backoffice | Sem QR/copy-paste/tokens |
| Customer Orders | `GET /api/customer/orders`, `/{orderId}` | Customer | Ownership; guest não por e-mail; 404 se alheio |
| Payments Pix | `POST /api/payments/pix/orders/{orderId}` | **Anônimo** | **RISK:** QR/copy-paste por `orderId` (IDOR se GUID vazar) |
| Payments Pix | `POST /api/payments/pix/webhooks/mercado-pago` | Anônimo; CSRF bypass | Assinatura + GET order |
| Payments Pix | GETs por paymentId | Backoffice | OK |
| Admin auth | login/logout/me + `GET /api/auth/csrf` | Cookie admin | OK |
| Customer auth | register/login/logout/me/forgot/reset/confirm | Cookie customer | OK |

Lacunas de contrato vs checklist do prompt:

- FE usa `/product/:slug`, não `/products/:slug` (singular).
- Não existe alias `/products` (home é `/`).

---

## 7. Rotas frontend validadas

Fonte: `apps/web/src/App.tsx`.

| Rota | Status | Guard |
|------|--------|-------|
| `/` | EXISTS | — |
| `/products` | **MISSING** (catalog = `/`) | — |
| `/products/:slug` | **MISSING** (usa `/product/:slug`) | — |
| `/cart`, `/checkout`, `/checkout/pix/:orderId` | EXISTS | Pix polling via guest status + token sessionStorage |
| `/admin/login`, `/admin`, products, inventory, orders | EXISTS | `AdminRouteGuard` |
| `/login`, `/register`, `/forgot-password` | EXISTS | — |
| `/account`, profile, addresses, orders | EXISTS | `CustomerRouteGuard` |

Segurança UI (inspeção):

- Admin Orders: sem QR/copy-paste/tokens (alinha docs + DTOs).
- Customer Orders: sem `providerOrderId` / payment/transaction IDs.
- Customer ≠ admin: cookies/schemes separados; guards distintos.

---

## 8. Configs / envs obrigatórias

### Arquivos

| Arquivo | Status |
|---------|--------|
| `deploy/.env.test.example` | Presente |
| `deploy/.env.hml.example` | Presente |
| `deploy/.env.example` | Presente (só Postgres Compose) |
| `.env.example` (raiz) | Presente (local + MP + guest) |
| `deploy/.env.prod.example` | **AUSENTE** |
| Deploy CI | `.github/workflows/deploy-vps.yml` — **test + hml apenas** |

### Go-live HML (valores esperados nas envs **reais**, não nos examples)

| Chave | Expectativa go-live HML | Nos `.example` |
|-------|-------------------------|----------------|
| `PaymentsPix__Provider` | `MercadoPago` | `Fake` ⚠️ |
| `MercadoPago__Enabled` | `true` | `false` ⚠️ |
| `MercadoPago__Environment` | `Sandbox` | `Sandbox` |
| `MercadoPago__AccessToken` / `WebhookSecret` / `ApplicationId` / `UserId` | Preenchidos (mesma app) | Vazios |
| `MercadoPago__BaseUrl` | `https://api.mercadopago.com` | OK |
| `MercadoPago__NotificationUrl` | URL HML no **painel** MP | HML example vazio; test tem URL |
| `MercadoPago__SendNotificationUrlInOrderCreate` | `false` (decisão atual) | `false` OK |
| `MercadoPago__SandboxPayerFirstNameOverride` | `APRO` opcional só sandbox | Vazio nos deploy examples; `APRO` no root example |
| `MercadoPago__WebhookRawCaptureEnabled` | `false` | `false` OK (+ gate Production no código) |
| `MercadoPagoReconciliation__Enabled` | **`true`** | `false` ⚠️ |
| `MercadoPagoReconciliation__IntervalSeconds/BatchSize/MaxAgeMinutes` | 60 / 20 / 180 típico | Presentes |
| `GuestOrderAccess__Enabled` | `true` | `true` |
| `GuestOrderAccess__TokenHashSecret` | Segredo forte | `CHANGE_ME_*` |
| `AllowedOrigins` | Origem FE do ambiente | Presente test/hml |
| `DataProtection__KeysPath` | Volume persistente | Presente deploy |
| `SHOPFLOW_ADMIN_RESET_PASSWORD` | `false` | `false` |
| `DemoCatalogSeed__Enabled` | OK em HML; **off** em prod | `true` em HML example |

### Perigosos para produção

- `MercadoPago__Environment=Sandbox`
- Qualquer `SandboxPayerFirstNameOverride` (esp. `APRO`)
- `WebhookRawCaptureEnabled=true`
- `PaymentsPix__Provider=Fake`
- `DemoCatalogSeed__Enabled=true`
- `SHOPFLOW_ADMIN_RESET_PASSWORD=true`
- Secrets `CHANGE_ME_*` / `dev-only-*`
- `GuestOrderAccess__TokenHashSecret` fraco

**Não imprimir secrets em logs/docs** — startup loga apenas booleanos de config + fingerprint do webhook secret fora de Production.

---

## 9. Migrations aplicáveis

### Inventário (todas forward-only via API startup `MigrateAsync`)

| Módulo | Migrations |
|--------|------------|
| Catalog | Initial + AddProductImages |
| Inventory | Initial + IntegrityConstraints |
| CartCheckout | Initial |
| Orders | Initial + **AddGuestOrderAccessTokens** + **AddCustomerUserIdToOrders** |
| PaymentsPix | Initial + ProviderStatus/WebhookEvents + **OrdersApiFields** |
| Identity | InitialIdentityAccess |

### Checks CustomerUserId — PASS

- Migration `20260715221854_AddCustomerUserIdToOrders` existe
- `Order.CustomerUserId` é `Guid?`
- Índice `IX_orders_CustomerUserId_CreatedAt` existe

### Aplicação

- **API** aplica migrations no boot (`Program.cs`).
- **Worker não migra** — subir/reiniciar API antes do worker.
- Scripts: `deploy/scripts/migrate-hml.sh` / `migrate-test.sh` → `docker compose restart api-*`.

**Risco se não aplicar `AddCustomerUserIdToOrders`:** Customer Orders quebra (SQL/coluna ausente).

Comando recomendado HML:

```bash
cd deploy && ./scripts/migrate-hml.sh
# ou: docker compose restart api-hml && docker compose logs --tail=80 api-hml
```

---

## 10. Testes executados e resultados

### Executados nesta auditoria

| Comando | Resultado |
|---------|-----------|
| `dotnet build` (`apps/api`) | **PASS** (0 errors; warnings NU1903 OpenApi + CS8602 teste) |
| `dotnet test` Orders.UnitTests | **PASS** 46 |
| `dotnet test` PaymentsPix.UnitTests | **PASS** 88 |
| `dotnet test` Expiration.UnitTests | **PASS** 7 |
| `dotnet test` Inventory.UnitTests | **PASS** 20 |
| `dotnet test` CartCheckout.UnitTests | **PASS** 10 |
| `dotnet test` Catalog.UnitTests | **PASS** 7 |
| `dotnet test` IdentityAccess.IntegrationTests | **PASS** 50 |
| `npm run typecheck` (`apps/web`) | **PASS** |
| `npm run build` (`apps/web`) | **PASS** (chunk >500kB warning) |

**Total unit/integration executados com sucesso: 228 testes** (soma dos projetos acima).

### Não executados (NOT RUN)

| Item | Motivo |
|------|--------|
| `dotnet test` solution completa / Integration (Inventory, Orders, PaymentsPix, CartCheckout, Catalog, Expiration) | Muitos exigem Postgres / tempo; não provisionado |
| Cypress `admin-orders.cy.ts`, `customer-orders.cy.ts`, pix/auth | Precisa API + browser/Docker; não rodado |
| Smoke manual A–E do prompt | Ambiente HML/MP não exercitado nesta sessão |
| `Vls.Shopflow.HttpApi.UnitTests` | Pasta **vazia** (sem `.csproj`) — placeholder |

Comandos sugeridos no ambiente correto:

```bash
# Backend
cd apps/api && dotnet test

# Frontend
cd apps/web && npm run typecheck && npm run build
npx cypress run --spec cypress/e2e/admin-orders.cy.ts
npx cypress run --spec cypress/e2e/customer-orders.cy.ts
npx cypress run --spec cypress/e2e/checkout-csrf-pix-flow.cy.ts
```

---

## 11. Smoke tests manuais recomendados

### A) Compra guest

1. Abrir loja sem login.
2. Adicionar produto ao carrinho.
3. Checkout convidado.
4. Gerar Pix.
5. Pagar no sandbox (APRO se override configurado).
6. Worker reconcilia → Order `Paid` (ou webhook se assinatura OK).
7. Tela Pix mostra aprovado.
8. Admin vê pedido pago.
9. Pedido **não** aparece em `/account/orders` só por e-mail.

### B) Compra customer logado

1. Login customer.
2. Checkout logado.
3. Gerar Pix → pagar sandbox.
4. Worker/webhook → Paid.
5. Admin vê pedido.
6. Customer vê em `/account/orders` + detalhe.

### C) Admin

1. Login admin → `/admin/orders` → filtrar Paid → detalhe.
2. Conferir cliente, entrega, itens, total, Pix resumo.
3. Confirmar ausência de QR / copia-e-cola / tokens / secrets.

### D) Segurança

1. Sem login → admin orders 401/redirect.
2. Customer não acessa admin orders.
3. Sem login → customer orders 401.
4. Customer A não acessa pedido de B (404).
5. Guest token não autoriza área customer.

### E) Expiração

1. Pix não pago expira.
2. Pedido pendente expira conforme TTL.
3. Reserva cancelada.
4. Pedido pago **não** expira.

---

## 12. Riscos bloqueantes (BLOCKER)

Nenhum **blocker de código** absoluto para HML **se** MP + reconciliation estiverem corretamente configurados e o worker estiver up.

| Item | Severidade real | Nota |
|------|-----------------|------|
| Migrations não aplicadas em HML após deploy de `CustomerUserId` | **BLOCKER operacional** se esquecer restart API | Mitigar com `migrate-hml.sh` + checar logs |
| `PaymentsPix__Provider=Fake` em HML “de verdade” | **BLOCKER operacional** para validar MP | Trocar nos `.env` reais |
| Produção com Sandbox / Fake / seed reset | **BLOCKER** para venda real | Sem `env.prod.example` aumenta chance de erro |

---

## 13. Riscos não bloqueantes

### HIGH

| Risco | Impacto | Sugestão |
|-------|---------|----------|
| Dependência do Worker para Paid se webhook sandbox falha assinatura | Atraso Paid (até IntervalSeconds); se worker cair, pedidos ficam Pending | Manter `MercadoPagoReconciliation__Enabled=true`; alertar se worker down; abrir suporte MP p/ assinatura |
| Worker cair / container parado | Pagamentos acreditados no MP sem Order Paid | Health/ps + restart; reconciliação manual controlada |
| TTL checkout/reserva (~15m) vs Pix MP (~30m) | Cliente paga após expire local → Order não vira Paid automaticamente | Alinhar TTLs; dívida “race webhook vs worker” |
| Pix create anônimo por `orderId` | IDOR de QR se GUID vazar | Pós-MVP: exigir guest token / ownership |
| Create-order público retorna PII completa | Quem tem session/order id vê e-mail/endereço | Aceitável só se GUIDs forem secretos; endurecer pós-MVP |

### MEDIUM

| Risco | Impacto | Sugestão |
|-------|---------|----------|
| Guest sem token perde status (sem e-mail) | Suporte manual | Documentar; claim pós-MVP |
| Sem e-mails transacionais | Cliente não recebe confirmação | Operação manual / pós-MVP |
| Sem frete real | Shipping null / “a calcular” | Processo manual de entrega |
| Sem cancelamento/reembolso no sistema | Operação só no painel MP + DB | Runbook manual |
| Docs AI-context stale (`shopflow-current-state`, FE context) | Decisões erradas em prompts futuros | Atualizar docs |
| `Microsoft.OpenApi` NU1903 | Advisory high no pacote OpenAPI | Atualizar pacote quando possível |
| Admin dashboard fake | Métricas enganosas | Não usar para operação financeira |

### LOW

| Risco | Nota |
|-------|------|
| Raw capture residual | Remover após diagnóstico (`MP-PIX-003`) |
| Chunk JS > 500kB | Performance |
| Pasta HttpApi.UnitTests vazia | Placeholder |
| Rotas `/products` vs `/product` | Ajustar docs/QA scripts |

---

## 14. Dívidas técnicas pós-MVP

- Webhook MP assinatura confiável em todos os ambientes (hoje: reconciliação é fallback MVP).
- Remover raw capture temporária.
- Gate guest-token / ownership em create Pix.
- Reduzir PII no create-order response público.
- E-mails (pedido/Pix/Paid).
- Claim de pedido guest.
- Frete real / fulfillment / tracking.
- Cancelamento / reembolso / chargeback.
- Dashboard admin real.
- `deploy/.env.prod.example` + pipeline prod.
- Atualizar `docs/ai-context/shopflow-current-state.md` e `apps/web/docs/ai-context/shopflow-frontend-context.md` (stale).
- technical-debt.md ainda lista “Sem CI/CD” e “Admin sem guard” — **desatualizado** (há GHA deploy + `AdminRouteGuard`).
- Alinhar TTL reserva vs Pix expiration.

---

## 15. Checklist go-live test / HML

- [ ] `.env.test` / `.env.hml` preenchidos (não usar placeholders)
- [ ] `PaymentsPix__Provider=MercadoPago` e `MercadoPago__Enabled=true`
- [ ] Credenciais Sandbox da **mesma** app (AccessToken + WebhookSecret + ApplicationId + UserId)
- [ ] `MercadoPago__Environment=Sandbox`
- [ ] `SendNotificationUrlInOrderCreate=false`; URL no **painel** Webhooks = API pública
- [ ] `MercadoPagoReconciliation__Enabled=true` (+ interval/batch/maxAge)
- [ ] `WebhookRawCaptureEnabled=false`
- [ ] `GuestOrderAccess__Enabled=true` + `TokenHashSecret` forte
- [ ] `AllowedOrigins` = FE Cloudflare do ambiente
- [ ] DataProtection volume montado
- [ ] Migrations: restart `api-test` / `api-hml`; logs sem erro de migrate
- [ ] `api-*` e `worker-*` up (`docker compose ps`)
- [ ] `GET /health` OK
- [ ] Admin login OK
- [ ] Customer register/login OK
- [ ] Produto demo visível
- [ ] Smoke A (guest) OK
- [ ] Smoke B (customer) OK
- [ ] Admin Orders OK (sem QR/secrets)
- [ ] Customer Orders OK (guest não por e-mail)
- [ ] Logs sem AccessToken / WebhookSecret / x-signature completa
- [ ] Cookies Secure/SameSite OK via HTTPS (Caddy + forwarded headers)

---

## 16. Checklist go-live produção

- [ ] Criar `deploy/.env.prod` (hoje **não há** `.example` — copiar hml e endurecer)
- [ ] `MercadoPago__Environment=Production`
- [ ] AccessToken / WebhookSecret / ApplicationId / UserId **produção**
- [ ] Remover `SandboxPayerFirstNameOverride` (vazio / ausente)
- [ ] `WebhookRawCaptureEnabled=false` (código também bloqueia Production)
- [ ] Notification URL produção no painel MP
- [ ] Domínio FE/API produção + HTTPS
- [ ] `AllowedOrigins` produção
- [ ] Cookie `__Host-` / Secure confirmado
- [ ] `DemoCatalogSeed__Enabled=false`
- [ ] `SHOPFLOW_ADMIN_RESET_PASSWORD=false`
- [ ] Secrets fortes (DB, admin, guest hash)
- [ ] Backup Postgres + volumes Docker (uploads + dataprotection)
- [ ] Worker ativo + reconciliation ON
- [ ] Compra real de **baixo valor**
- [ ] Conferir Order Paid + Admin Orders + Customer Orders + estoque
- [ ] Definir processo manual de entrega/frete
- [ ] Plano de suporte se Paid atrasar (worker / MP painel)

**Produção ainda NÃO READY** até os itens acima + smoke real.

---

## 17. Plano de rollback

1. **Frontend:** republicar versão anterior no Cloudflare Pages.
2. **API/Worker:** redeploy imagem/commit anterior via workflow ou `deploy-*.sh`; `docker compose up -d --force-recreate api-* worker-*`.
3. **Banco:** migrations **forward-only** — não rodar rollback destrutivo automático. Se coluna nova já aplicada, manter compatível com código antigo só se o código antigo tolera a coluna (geralmente sim para add nullable).
4. **Reconciliação problemática:** `MercadoPagoReconciliation__Enabled=false` + restart worker (Paid só via webhook — pior se assinatura falhar).
5. **Provider:** voltar `Fake` **somente** em test/HML — **nunca** em produção de venda.
6. **Pausar vendas:** manutenção no FE (remover CTA checkout) ou tirar origem do ar.
7. **Identificar pagos no MP:** painel Mercado Pago / `GET /v1/orders/{id}` com AccessToken.
8. **Reconciliação manual:** procedimento controlado (comparar `payments_pix.pix_payments` Pending com MP; **não** marcar Paid no DB sem evidência `processed`/`accredited` + confirmar reserva). Preferir reativar worker.

### Queries úteis (não executadas nesta auditoria)

```sql
-- Pix pending MP
SELECT "Id", "OrderId", "Status", "Provider", "ProviderOrderId",
       "ProviderStatus", "ProviderStatusDetail", "PaidAt", "ExpiresAt", "CreatedAt"
FROM payments_pix.pix_payments
WHERE "Provider" = 'MercadoPago'
ORDER BY "CreatedAt" DESC
LIMIT 50;

-- Order + customer binding
SELECT "Id", "Status", "Total", "CustomerUserId", "PaidAt", "CreatedAt"
FROM orders.orders
ORDER BY "CreatedAt" DESC
LIMIT 50;

-- Índice CustomerUserId
SELECT indexname FROM pg_indexes
WHERE schemaname = 'orders' AND indexname = 'IX_orders_CustomerUserId_CreatedAt';

-- Reservas
SELECT "Id", "SkuId", "Status", "Quantity", "ExpiresAt"
FROM inventory.stock_reservations
WHERE "Status" = 'Pending'
LIMIT 50;
```

---

## 18. Próximos passos recomendados

1. **HML:** setar envs reais `MercadoPago` + `Reconciliation__Enabled=true`; restart api+worker; smoke A/B/C.
2. Rodar Cypress críticos (`admin-orders`, `customer-orders`, pix/csrf) contra HML/API local.
3. Validar/abrir chamado MP sobre assinatura webhook sandbox; manter reconciliação ON.
4. Criar `deploy/.env.prod.example` + endurecer checklist produção.
5. Atualizar docs stale (`shopflow-current-state`, FE context, `technical-debt`) e alinhar TTLs reserva/Pix.

---

## Apêndice A — Mercado Pago (validação)

| Check | Resultado |
|-------|-----------|
| Provider MercadoPago | Implementado (config) |
| Orders API (não Payments legada) | `POST/GET /v1/orders` |
| Paid só processed/accredited | `MercadoPagoOrderStatusRules.IsPaid` |
| Reconciliação fallback MVP | Documentada + código; **default OFF nos examples** |
| Webhook assinatura | SDK + oráculo; falhas sandbox = dívida operacional |
| Bloqueia MVP? | **Não**, se reconciliação ativa |
| Bloqueia produção? | Avaliar: ideal webhook OK; reconciliação obrigatória como rede de segurança |
| Raw capture | Temp; OFF; hard-gate Production |
| `SendNotificationUrlInOrderCreate` | `false` documentado/decidido |

---

## Apêndice B — Segurança (checklist)

| Item | Status |
|------|--------|
| Admin/Customer HttpOnly cookies separados | OK |
| CSRF mutações autenticadas | OK |
| Webhook CSRF bypass path | OK (`/api/payments/pix/webhooks/`) |
| CORS allowlist | OK (por env) |
| DataProtection volume | OK (compose deploy) |
| Secrets não logados (token/secret) | OK (booleans/fingerprint) |
| Guest token raw one-shot na create | OK (hash armazenado; reutilizável no polling) |
| Admin Orders sem QR/tokens | OK |
| Customer Orders sem IDs MP | OK |
| Stock mutate não público | OK |
| Guest status sem PII completa | OK |
| Create-order / Pix create públicos | RISK (ver §13) |
| Raw capture Production | Bloqueado no código |

---

## Apêndice C — Workers

| Worker | Config | Comportamento |
|--------|--------|---------------|
| `MercadoPagoPixReconciliationWorker` | `MercadoPagoReconciliation__*` | Pending+MP+ProviderOrderId → GET order → Paid se accredited; idempotente; falha item não para batch |
| `PendingCheckoutExpirationWorker` | `ExpirationWorker__*` | Expira sessions/orders/pix; cancela reservas; não expira Paid |

Logs:

```bash
cd deploy
docker compose logs -f worker-hml
docker compose logs -f api-hml | grep -E 'PaymentsPix|MercadoPago|Reconciliation'
docker compose ps
```

Se worker cair: Paid atrasa/para (webhook pode compensar se assinatura OK).

---

## Apêndice D — Documentação (inconsistências)

| Doc | Avaliação |
|-----|-----------|
| `docs/orders/admin-orders.md` / `customer-orders.md` | Coerentes; “pós-MVP frontend” parcialmente stale |
| `docs/payments/MP-PIX-002` / `MP-PIX-003` | Bons; reconciliation + raw capture claros |
| `docs/payments-pix.md` | OK; trechos Fake “gateway não integrado” desatualizados |
| `docs/ai-context/shopflow-current-state.md` | **Stale** (omite admin/customer orders FE; “sem CI/CD”; gateway pendente) |
| `docs/ai-context/backend-next-actions.md` | **Ausente** — usar `next-actions.md` |
| `docs/ai-context/technical-debt.md` | Parcialmente stale (CI/CD, admin guard) |
| `apps/web/docs/admin-orders.md` / `customer-orders.md` / `api-contracts.md` | Bons |
| `apps/web/docs/ai-context/shopflow-frontend-context.md` | **Stale** vs App.tsx real |

---

## Critério de aceite deste QA

- [x] Nenhuma feature nova criada
- [x] Relatório em `docs/qa/MVP-FINAL-QA-GO-LIVE-REPORT.md`
- [x] Rotas / envs / migrations / docs / testes / riscos validados
- [x] Checklists HML e produção
- [x] Riscos classificados
- [x] Smoke tests documentados
- [x] Plano de rollback documentado
