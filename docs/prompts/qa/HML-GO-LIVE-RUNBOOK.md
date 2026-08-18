# Shopflow — Runbook operacional HML (go-live / smoke)

> Data: **2026-07-17**  
> Objetivo: levar Homologação de *READY WITH RISKS* → **HML READY** com Mercado Pago Sandbox + Worker de reconciliação.  
> Relatório QA: [MVP-FINAL-QA-GO-LIVE-REPORT.md](./MVP-FINAL-QA-GO-LIVE-REPORT.md)  
> Checklist curto: [HML-SMOKE-TEST-CHECKLIST.md](./HML-SMOKE-TEST-CHECKLIST.md)

**Não criar feature neste runbook.** Não alterar `.env.hml` real via git. Não imprimir secrets.

---

## 1. Objetivo

Validar ponta a ponta em HML:

1. Deploy API + Worker + migrations.
2. Mercado Pago **Sandbox** cria Pix (`POST /v1/orders`).
3. Pagamento sandbox → Order/Pix **Paid** (webhook **ou** reconciliação Worker).
4. Admin Orders e Customer Orders operacionais e sem vazamento sensível.
5. Cookies/CORS/HTTPS ok; Cypress crítico executado ou motivo registrado.

URLs de referência (domínio atual do projeto):

| Papel | URL |
|-------|-----|
| Frontend HML | `https://hml.vipassessoriadigital.com.br` |
| API HML | `https://api-hml.vipassessoriadigital.com.br` |
| Webhook MP | `https://api-hml.vipassessoriadigital.com.br/api/payments/pix/webhooks/mercado-pago` |

Placeholders nos curls abaixo:

```bash
export API_HML=https://api-hml.vipassessoriadigital.com.br
export WEB_HML=https://hml.vipassessoriadigital.com.br
```

---

## 2. Pré-requisitos

- Acesso SSH à VPS e permissão `docker` / `docker compose`.
- Repo em `/opt/shopflow/app` (ou path do deploy) com pasta `deploy/`.
- Arquivos **não versionados** na VPS: `deploy/.env`, `deploy/.env.hml` (secrets reais).
- Modelo: `deploy/.env.hml.example` (atualizado para HML go-live — sem secrets).
- Conta Mercado Pago **aplicação de teste** (Sandbox): AccessToken, WebhookSecret, ApplicationId, UserId da **mesma** app.
- Frontend Cloudflare Pages HML com `VITE_API_BASE_URL=https://api-hml.vipassessoriadigital.com.br/api` ([RUNBOOK-002](../infra/RUNBOOK-002-cloudflare-pages-frontend.md)).
- Branch de deploy HML: tipicamente `staging` → workflow `deploy-vps.yml` job hml ([RUNBOOK-004](../infra/RUNBOOK-004-github-actions-vps-deploy.md)).
- Opcional local: Node 20+ para Cypress em `apps/web`.

Serviços Compose HML: `api-hml`, `worker-hml`, `postgres`, `caddy`. Confirme nomes:

```bash
cd deploy && docker compose ps --services
```

---

## 3. Checklist de env HML

Edite **somente** `deploy/.env.hml` na VPS (nunca commit). Compare com `deploy/.env.hml.example`.

### Obrigatório para smoke MP

| Chave | Valor HML |
|-------|-----------|
| `PaymentsPix__Provider` | `MercadoPago` |
| `MercadoPago__Enabled` | `true` |
| `MercadoPago__Environment` | `Sandbox` |
| `MercadoPago__BaseUrl` | `https://api.mercadopago.com` |
| `MercadoPago__AccessToken` | secret Sandbox (vazio no example) |
| `MercadoPago__WebhookSecret` | secret Sandbox (vazio no example) |
| `MercadoPago__ApplicationId` | id app teste |
| `MercadoPago__UserId` | user id app teste |
| `MercadoPago__NotificationUrl` | URL webhook API HML (referência; painel MP é a fonte com `SendNotificationUrlInOrderCreate=false`) |
| `MercadoPago__SendNotificationUrlInOrderCreate` | `false` |
| `MercadoPago__WebhookRawCaptureEnabled` | `false` |
| `MercadoPago__SandboxPayerFirstNameOverride` | `APRO` opcional (só HML/teste) |
| `MercadoPagoReconciliation__Enabled` | **`true`** |
| `MercadoPagoReconciliation__IntervalSeconds` | `60` |
| `MercadoPagoReconciliation__BatchSize` | `20` |
| `MercadoPagoReconciliation__MaxAgeMinutes` | `180` |

### Guest / auth / infra

| Chave | Valor HML |
|-------|-----------|
| `GuestOrderAccess__Enabled` | `true` |
| `GuestOrderAccess__TokenHashSecret` | segredo forte (não `CHANGE_ME_*`) |
| `GuestOrderAccess__TokenTtlDays` | `30` |
| `GuestOrderAccess__RateLimitPerMinute` | `30` |
| `AllowedOrigins__0` | `https://hml.vipassessoriadigital.com.br` |
| `DataProtection__KeysPath` | `/app/dataprotection-keys` (volume `dataprotection_hml`) |
| `SHOPFLOW_ADMIN_RESET_PASSWORD` | `false` |
| `DemoCatalogSeed__Enabled` | `true` **ok em HML**; **proibido em produção** |
| `ASPNETCORE_ENVIRONMENT` | `Staging` (também no compose) |
| `ExpirationWorker__Enabled` | `true` |

### Segurança / OpenAPI

- Scalar/OpenAPI só em **Development** (`Program.cs`) — Staging HML **não** deve expor Scalar. OK.
- Nunca logar AccessToken, WebhookSecret, TokenHashSecret, cookies ou connection string.

### Risco histórico (corrigido no example)

Antes, `.env.hml.example` vinha com `Provider=Fake`, `Enabled=false`, `Reconciliation=false`. Isso **não** valida MVP MP. O example foi alinhado ao go-live HML (placeholders vazios para secrets). Se o `.env.hml` **real** na VPS ainda estiver Fake/off, atualize manualmente na VPS antes do smoke.

---

## 4. Checklist Mercado Pago Sandbox

- [ ] App **teste** no painel MP (não Production).
- [ ] AccessToken = **Credenciais de teste**.
- [ ] WebhookSecret da **mesma** aplicação.
- [ ] Evento webhook: **Order (Mercado Pago)**.
- [ ] URL no painel Webhooks = `https://api-hml.../api/payments/pix/webhooks/mercado-pago`.
- [ ] `SendNotificationUrlInOrderCreate=false` (painel tem prioridade quando create não envia URL).
- [ ] `Environment=Sandbox`.
- [ ] Sem credenciais Production em HML.
- [ ] Raw capture **off** no go-live.
- [ ] Reconciliação **on** (fallback se assinatura webhook falhar — dívida conhecida).
- [ ] Opcional: `SandboxPayerFirstNameOverride=APRO` para auto-aprovação de cenários de teste.

Assinatura inválida em sandbox **não** bloqueia HML READY se o Worker marcar Paid com `processed`/`accredited`.

---

## 5. Checklist Docker / containers

Volumes esperados:

- `dataprotection_hml` → cookies/CSRF sobrevivem a redeploy.
- `api_hml_uploads` → imagens/seed.

```bash
cd deploy
docker compose config
docker compose ps
docker compose ps api-hml worker-hml caddy postgres
```

Critérios: todos `Up` / healthy (postgres healthy); worker sem porta pública.

---

## 6. Ordem correta de deploy HML

1. Atualizar código na VPS (`git pull` ou Actions rsync).
2. Validar `docker compose config`.
3. Confirmar/ajustar `deploy/.env.hml` (MP + reconciliation).
4. Build `api-hml` + `worker-hml`.
5. **Subir/recriar API primeiro** (migrations no boot).
6. Conferir logs API (migrate + PaymentsPix provider).
7. Subir/recriar Worker.
8. Conferir logs Worker (reconciliation started).
9. Reload Caddy se Caddyfile mudou.
10. `GET /health`.
11. Validar FE Cloudflare → API HML.
12. Cypress crítico.
13. Smoke guest → customer → admin.
14. Registrar resultado (READY / NOT READY).

### Via script existente

```bash
cd deploy
./scripts/deploy-hml.sh
# Migrations (restart API):
./scripts/migrate-hml.sh
# Se worker não recriou após mudança de env, force:
docker compose up -d --force-recreate worker-hml
docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile
docker compose ps
```

### Via Actions

Push na branch `staging` ou `workflow_dispatch` hml (`.github/workflows/deploy-vps.yml`). Envs reais na VPS **não** são sobrescritas pelo rsync.

---

## 7. Ordem correta de migrations

| Fato | Detalhe |
|------|---------|
| Quem migra | **Só a API** no startup (`MigrateAsync` em `Program.cs`) |
| Worker | **Não** aplica migrations |
| Ordem | API up/restart **antes** de confiar no Worker |
| Obrigatória recente | `AddCustomerUserIdToOrders` — sem ela, Customer Orders quebra |
| Outras críticas | GuestOrderAccessTokens; PaymentsPix Orders API fields |

```bash
cd deploy
./scripts/migrate-hml.sh
# equivalente:
docker compose up -d postgres
docker compose restart api-hml
sleep 5
docker compose logs --tail=150 api-hml | grep -iE "migration|migrate|error|exception|CustomerUserId|fail"
```

Sem erros de migrate + API estável → seguir. Worker só depois.

---

## 8. Comandos para subir API e Worker

```bash
cd deploy

docker compose build api-hml worker-hml

# API primeiro (migrations)
docker compose up -d --force-recreate api-hml
docker compose logs --tail=120 api-hml

# Worker depois
docker compose up -d --force-recreate worker-hml
docker compose logs --tail=120 worker-hml

docker compose up -d caddy
docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile

docker compose ps
```

Imagens: `apps/api/Dockerfile` (API), `apps/api/Dockerfile.worker` (Worker).

---

## 9. Comandos para validar logs

```bash
cd deploy

docker compose logs -f api-hml
docker compose logs -f worker-hml
docker compose logs -f caddy
docker compose logs -f postgres

# Foco API
docker compose logs --tail=200 api-hml | grep -iE \
  "PaymentsPix|MercadoPago|Webhook|Reconciliation|DataProtection|CORS|CSRF|error|exception|migrate"

# Foco Worker
docker compose logs --tail=200 worker-hml | grep -iE \
  "reconciliation|MercadoPago|processed|accredited|Paid|Expiration|reservation|error|exception|disabled|started"
```

### Esperado

| Sinal | OK |
|-------|-----|
| API sobe sem exception fatal | Sim |
| Log startup: provider `MercadoPago` | Sim |
| `webhook secret configured: True` (booleano, não o secret) | Sim |
| Worker: `Mercado Pago Pix reconciliation worker started` | Sim |
| Expiration worker ativo | Sim |
| Raw capture ativo | **Não** no go-live |
| AccessToken / WebhookSecret em texto | **Nunca** |

Se aparecer `reconciliation worker is disabled` → `MercadoPagoReconciliation__Enabled` ainda false no `.env.hml` real; corrigir e recreate worker.

---

## 10. Curl — saúde / auth / endpoints críticos

```bash
export API_HML=https://api-hml.vipassessoriadigital.com.br

# Health
curl -i "$API_HML/health"
# Esperado: 200 + status ok + environment Staging

# CSRF cookie
curl -i -c /tmp/sf-hml.txt "$API_HML/api/auth/csrf"

# Catálogo
curl -i "$API_HML/api/catalog/products"

# Upload seed (exemplo — nome pode variar conforme seed)
curl -i "$API_HML/uploads/seed-products/camiseta-basica-branca.png"

# Admin Orders sem cookie → 401
curl -i "$API_HML/api/admin/orders"

# Customer Orders sem cookie → 401
curl -i "$API_HML/api/customer/orders"

# Guest status sem token → 401 (GuestOrderAccessDenied)
curl -i "$API_HML/api/orders/guest/00000000-0000-0000-0000-000000000001/status"
```

Não incluir passwords/tokens nos exemplos. Login admin/customer: usar browser ou Cypress com env local.

---

## 11. Frontend / Cloudflare Pages

| Check | Esperado |
|-------|----------|
| `VITE_API_BASE_URL` | `https://api-hml.vipassessoriadigital.com.br/api` ([apps/web/.env.hml.example](../../apps/web/.env.hml.example)) |
| `VITE_APP_ENV` | `hml` |
| Credentials | requests com cookies (`credentials: include` no client) |
| CORS API | `AllowedOrigins__0` = origem FE HML |
| Imagens | `/uploads/...` na origem da **API** (`Uploads__PublicBaseUrl`) |
| Rotas | `/`, `/product/:slug` (singular), `/cart`, `/checkout`, `/checkout/pix/:orderId`, `/admin/login`, `/admin/orders`, `/account/orders` |

Build local (opcional):

```bash
cd apps/web
cp .env.hml.example .env.hml   # se for build local apontando HML
npm run typecheck
npm run build
```

Deploy FE: [RUNBOOK-002](../infra/RUNBOOK-002-cloudflare-pages-frontend.md) — projeto Pages branch `staging`.

---

## 12. Cypress a executar

Specs críticos (UI com intercepts; auth real quando env setada):

```bash
cd apps/web

# Apontar para FE HML (ou local com proxy)
export CYPRESS_BASE_URL=https://hml.vipassessoriadigital.com.br
export CYPRESS_API_URL=https://api-hml.vipassessoriadigital.com.br/api

# Credenciais — NÃO commit; use env shell ou cypress.env.json local (gitignored se criado)
export CYPRESS_ADMIN_EMAIL='...'
export CYPRESS_ADMIN_PASSWORD='...'
export CYPRESS_CUSTOMER_EMAIL='...'
export CYPRESS_CUSTOMER_PASSWORD='...'

npx cypress run --spec cypress/e2e/admin-orders.cy.ts
npx cypress run --spec cypress/e2e/customer-orders.cy.ts
npx cypress run --spec cypress/e2e/checkout-csrf-pix-flow.cy.ts
```

Config: `apps/web/cypress.config.cjs` (`CYPRESS_*` → `env`).

Se Cypress **não** rodar: registrar motivo no resultado do go-live (máquina, rede, credenciais). Smoke manual guest/customer/admin continua obrigatório para HML READY.

---

## 13. Smoke test guest (passo a passo)

1. Abrir `$WEB_HML` **sem** login.
2. Escolher produto demo; conferir imagem.
3. Adicionar SKU ao carrinho → `/cart` → `/checkout`.
4. Preencher dados convidado; finalizar.
5. Criar pedido + gerar Pix Sandbox.
6. Tela `/checkout/pix/:orderId`: QR e/ou copia-e-cola.
7. Pagar no sandbox (APRO se override ativo).
8. Aguardar Worker (até ~`IntervalSeconds`, tipicamente 60s+).
9. Logs worker: candidatos, `GET /v1/orders`, `processed`/`accredited`, Paid.
10. UI Pix → pagamento aprovado.
11. Login admin → `/admin/orders` → pedido **Paid**.
12. Detalhe: cliente, entrega, itens, total, payment Paid.
13. Login customer com **mesmo e-mail** do guest → `/account/orders` **não** lista o pedido guest.

**Sucesso:** Order Paid + Pix Paid + reserva confirmada + Admin vê + Customer não lista por e-mail.

---

## 14. Smoke test customer logado

1. Registrar ou logar customer (`/register` / `/login`).
2. Confirmar sessão (`/account` ou `GET /api/auth/customer/me` autenticado).
3. Produto → carrinho → checkout **com cookie customer**.
4. Criar pedido + Pix + pagar sandbox.
5. Worker reconcilia → Paid.
6. Admin vê pedido Paid.
7. `/account/orders` lista o pedido; `/account/orders/:id` abre detalhe.
8. UI **não** mostra: `providerOrderId`, `providerPaymentId`, QR, copia-e-cola, ticketUrl, tokens, secrets.

**Sucesso:** `CustomerUserId` preenchido; customer vê só os seus.

---

## 15. Smoke test admin orders

1. `/admin/login` → cookie admin.
2. `/admin/orders` → filtrar Paid; buscar e-mail/nome/id.
3. Detalhe: cliente, entrega, itens, subtotal, frete (pode ser null), total, payment provider/status.
4. Ausente: QR, copia-e-cola, ticketUrl, AccessToken, WebhookSecret, GuestAccessToken, x-signature.

**Sucesso:** lojista opera pedido pago sem dados sensíveis de pagamento.

---

## 16. Validação de banco (queries)

Na VPS (ajuste user/db):

```bash
docker compose exec -e PGPASSWORD postgres \
  psql -U shopflow -d shopflow_hml
```

```sql
-- Pix por ProviderOrderId (substitua ORDTST...)
SELECT "Id", "OrderId", "Status", "Provider", "ProviderOrderId",
       "ProviderStatus", "ProviderStatusDetail",
       "ProviderTransactionStatus", "ProviderTransactionStatusDetail",
       "PaidAt", "ExpiresAt", "CreatedAt"
FROM payments_pix.pix_payments
WHERE "ProviderOrderId" = 'ORDTST...';

-- Order
SELECT "Id", "Status", "Total", "CustomerUserId", "PaidAt", "CreatedAt"
FROM orders.orders
WHERE "Id" = '...';

-- Índice CustomerUserId
SELECT indexname
FROM pg_indexes
WHERE schemaname = 'orders'
  AND indexname = 'IX_orders_CustomerUserId_CreatedAt';

-- Reservas recentes
SELECT "Id", "SkuId", "Status", "Quantity", "ExpiresAt"
FROM inventory.stock_reservations
ORDER BY "ExpiresAt" DESC
LIMIT 50;
```

**Não** marcar Paid manualmente sem evidência MP `processed`/`accredited`. Preferir reativar Worker.

---

## 17. Troubleshooting

### A) API não sobe

- `docker compose logs api-hml`
- Connection strings / Postgres healthy
- Admin seed obrigatório em Staging (`SHOPFLOW_ADMIN_EMAIL` / `PASSWORD`)
- Erro de migrate

### B) Worker sem candidatos

- Pix `Status=Pending`, `Provider=MercadoPago`, `ProviderOrderId` preenchido
- `MaxAgeMinutes` / CreatedAt
- Mesmo banco `shopflow_hml` no worker `.env.hml`
- Reconciliation Enabled

### C) Candidato existe mas não Paid

- `GET /v1/orders` 401 → AccessToken
- Status ≠ `processed` + `accredited`
- Order já `Expired` (race TTL) — dívida conhecida
- Exception nos logs do processor

### D) Tela Pix continua Pending

- Guest token em sessionStorage / header `X-ORDER-ACCESS-TOKEN`
- Polling FE; guest status API
- Status real no banco vs UI
- Worker logs

### E) Customer Orders vazio após compra logada

- Migration `CustomerUserId` aplicada?
- Checkout com credentials / cookie customer
- `Order.CustomerUserId` null → foi tratado como guest

### F) Admin Orders não abre

- Cookie admin + login
- CORS origem FE
- `AdminRouteGuard` + 401 API

### G) Cookie não persiste

- HTTPS, Secure, SameSite, domínio
- DataProtection volume persistente
- `UseForwardedHeaders` + Caddy `X-Forwarded-*`
- CORS `AllowCredentials`

### H) Imagem demo 404

- Demo seed + copy images
- Volume uploads
- `Uploads__PublicBaseUrl`
- Caddy proxy `/uploads`

### I) Webhook assinatura falha

- Não bloqueia HML se Worker reconcilia
- Manter raw capture off
- Validar secret/app/user/URL painel
- Abrir suporte MP se necessário

---

## 18. Critérios — HML READY

Declarar **HML READY** somente se **todos**:

- [ ] API HML sobe; Worker HML sobe
- [ ] `/health` OK (Staging)
- [ ] Migrations sem erro (`CustomerUserId` ok)
- [ ] Provider MercadoPago; Reconciliation **true**; RawCapture **false**
- [ ] Admin login OK; Customer register/login OK
- [ ] Produto demo + imagem visíveis
- [ ] Checkout guest → Pix sandbox → Paid (Worker e/ou webhook)
- [ ] Admin vê pedido guest Paid
- [ ] Guest **não** aparece em Meus pedidos só por e-mail
- [ ] Checkout customer → Pix → Paid → aparece em `/account/orders`
- [ ] Admin/Customer UI sem campos sensíveis proibidos
- [ ] Cypress admin-orders + customer-orders PASS **ou** NOT RUN com motivo
- [ ] Logs sem secrets

---

## 19. Critérios — HML NOT READY

Qualquer um:

- API ou Worker não sobe / migrate falha
- Provider Fake ou Reconciliation off no smoke real
- Pix não cria / Worker não marca Paid com MP accredited
- Admin não vê Paid / Customer logado não vê o próprio
- Cookies/CORS impedem login
- Logs com secrets / Raw capture ligada sem necessidade
- DataProtection sem volume e cookies quebram a cada deploy

---

## 20. O que NÃO fazer em HML

- Usar credenciais **Production** do Mercado Pago
- Commitar `deploy/.env.hml` ou secrets
- Ativar raw capture no go-live “por padrão”
- Validar MVP com `Provider=Fake`
- Marcar Paid no SQL sem evidência MP
- Desligar Worker durante smoke de pagamento
- Mudar código de negócio no meio do smoke
- Misturar teste de venda real Production com HML

---

## 21. Próximos passos após HML READY

1. Registrar evidências (order ids, timestamps, screenshots) sem secrets.
2. Abrir/acompanhar suporte MP sobre assinatura webhook (manter reconciliation ON).
3. Alinhar TTL reserva checkout vs `PixExpirationMinutes` (dívida race).
4. Preparar `deploy/.env.prod.example` + checklist produção (fora deste runbook).
5. Atualizar docs stale (`shopflow-current-state`, RUNBOOK-004 ainda menciona Fake em trechos).
6. Só então planejar go-live produção com compra real de baixo valor.

---

## Referências rápidas

| Doc | Uso |
|-----|-----|
| [MVP-FINAL-QA-GO-LIVE-REPORT.md](./MVP-FINAL-QA-GO-LIVE-REPORT.md) | Riscos e status QA |
| [HML-SMOKE-TEST-CHECKLIST.md](./HML-SMOKE-TEST-CHECKLIST.md) | Checklist marcável |
| [deploy/README.md](../../deploy/README.md) | Compose / env split |
| [MP-PIX-002](../payments/MP-PIX-002-orders-provider-and-webhook.md) | Provider + webhook + reconciliation |
| [MP-PIX-003](../payments/MP-PIX-003-webhook-raw-capture-temporary.md) | Raw capture temp |
| [admin-orders / customer-orders](../orders/admin-orders.md) | Contratos backend |
