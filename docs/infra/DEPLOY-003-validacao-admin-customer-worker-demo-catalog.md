# DEPLOY-003 — Validação pré-deploy: Admin, Customer, Worker e Demo Catalog

**Status:** Revisão pré-deploy (teste + HML)  
**Data:** 2026-07-07  
**Escopo:** Validar readiness antes de enviar artefatos à VPS existente e rodar Docker Compose.

**Não inclui:** produção, Mercado Pago real, alteração de DNS Cloudflare.

---

## 1. Escopo do deploy

Este deploy adiciona/atualiza na VPS existente:

| Feature | Componentes |
|---------|-------------|
| Admin Auth | Identity, cookie HttpOnly, CSRF, policy Backoffice, `/admin/*` |
| Customer Identity (backend) | Register/login/logout/me, forgot/reset/confirm-email, cookie customer |
| Customer Auth (frontend) | `/login`, `/register`, `/forgot-password`, `/account/*` |
| Expiration Worker | `worker-test`, `worker-hml` — expira checkout/order/pix pendentes |
| Demo Clothing Catalog | 10 produtos, 20 imagens, 94 SKUs, estoque demo |
| Frontend Cloudflare Pages | Build com `VITE_API_BASE_URL` por ambiente |

**PaymentsPix:** continua Fake/Pending (sem Mercado Pago).

---

## 2. Arquivos alterados / necessários

### Backend e deploy (alterados nesta revisão)

| Arquivo | Motivo |
|---------|--------|
| `apps/api/Dockerfile` | Porta 8080, `seed-assets`, dirs de upload |
| `deploy/docker-compose.yml` | `worker-test`, `worker-hml`, volumes DataProtection |
| `deploy/.env.test.example` | Vars completas (worker, DataProtection, demo seed) |
| `deploy/.env.hml.example` | Idem para HML |
| `deploy/scripts/deploy-test.sh` | Inclui worker-test |
| `deploy/scripts/deploy-hml.sh` | Inclui worker-hml |
| `apps/web/.gitignore` | Ignora `.env`, `.env.test`, `.env.hml`, `.env.local` |

### Já existentes e validados

- `apps/api/Dockerfile.worker`
- `apps/api/seed-assets/catalog-products/` (20 PNGs)
- `apps/api/.../DemoClothingCatalogSeed.cs` (idempotente)
- `apps/api/.../IdentityAccessDbContextSeed.cs` (admin + reset)
- `apps/api/ApiGateways/.../Program.cs` (CORS, migrations, demo seed)
- `apps/web/.env.test.example`, `.env.hml.example`
- `apps/web/public/_redirects`
- `docs/infra/RUNBOOK-001-vps-setup-deploy.md`
- `docs/infra/RUNBOOK-002-cloudflare-pages-frontend.md`

---

## 3. Checklist pré-deploy (geral)

- [ ] Código mergeado na branch correta (`develop` → teste, `staging` → HML)
- [ ] Nenhum secret real commitado
- [ ] `.env.test` e `.env.hml` na VPS atualizados a partir dos `.example`
- [ ] `DataProtection__KeysPath` configurado e volume persistente
- [ ] `SHOPFLOW_ADMIN_*` definidos (obrigatório em Testing/Staging)
- [ ] `AllowedOrigins__0` aponta para frontend correto
- [ ] `DemoCatalogSeed__Enabled=true` (se quiser catálogo demo)
- [ ] `ExpirationWorker__Enabled=true`
- [ ] Frontend Cloudflare Pages com rebuild após merge
- [ ] Backup do volume Postgres antes do deploy (recomendado)

---

## 4. Checklist docker-compose

- [x] `postgres` com volume `postgres_data` e healthcheck
- [x] `caddy` expõe 80/443, proxy `api-test:8080` e `api-hml:8080`
- [x] `api-test` → `env_file: .env.test` apenas
- [x] `api-hml` → `env_file: .env.hml` apenas
- [x] `worker-test` → `env_file: .env.test` apenas
- [x] `worker-hml` → `env_file: .env.hml` apenas
- [x] Workers sem porta pública
- [x] Postgres sem porta pública
- [x] Volumes: `api_*_uploads`, `dataprotection_*`, `caddy_*`, `postgres_data`
- [ ] Na VPS: `docker compose config` sem erros

---

## 5. Checklist `.env.test`

Copiar de `.env.test.example` e preencher secrets reais:

- [ ] `POSTGRES_*` em `deploy/.env` (compartilhado)
- [ ] Connection strings → `shopflow_test`
- [ ] `DataProtection__KeysPath=/app/dataprotection-keys`
- [ ] `Uploads__PublicBaseUrl=https://api-teste.vipassessoriadigital.com.br`
- [ ] `AllowedOrigins__0=https://teste.vipassessoriadigital.com.br`
- [ ] `SHOPFLOW_ADMIN_EMAIL=admin-teste@vipassessoriadigital.com.br`
- [ ] `SHOPFLOW_ADMIN_PASSWORD` (senha forte real)
- [ ] `SHOPFLOW_ADMIN_RESET_PASSWORD=false` (ou `true` só durante rotação)
- [ ] `DemoCatalogSeed__Enabled=true`
- [ ] `ExpirationWorker__Enabled=true`

---

## 6. Checklist `.env.hml`

Mesma estrutura, com valores HML:

- [ ] Connection strings → `shopflow_hml`
- [ ] `Uploads__PublicBaseUrl=https://api-hml.vipassessoriadigital.com.br`
- [ ] `AllowedOrigins__0=https://hml.vipassessoriadigital.com.br`
- [ ] `SHOPFLOW_ADMIN_EMAIL=admin-hml@vipassessoriadigital.com.br`
- [ ] `SHOPFLOW_ADMIN_PASSWORD` (senha forte real, distinta de teste)

---

## 7. Checklist Dockerfile API

- [x] Build context: `apps/api`
- [x] Copia `seed-assets/` para `/app/seed-assets`
- [x] `ASPNETCORE_URLS=http://+:8080` (compose também define)
- [x] Cria `/app/wwwroot/uploads/seed-products`
- [x] Volume `wwwroot/uploads` para persistência
- [x] Não depende de arquivos fora do build context
- [x] Compose sobrescreve porta 8080 em runtime

---

## 8. Checklist Dockerfile worker

- [x] Existe em `apps/api/Dockerfile.worker`
- [x] Publica `Workers/Vls.Shopflow.Worker`
- [x] Imagem `runtime:10.0` (sem porta exposta)
- [x] Sem secrets hardcoded
- [x] Connection strings via `env_file` (mesmo padrão da API)
- [x] `ExpirationWorker__*` controla intervalo, batch e TTLs

---

## 9. Checklist seed demo

- [x] `apps/api/seed-assets/catalog-products/` com 20 imagens
- [x] Dockerfile copia `seed-assets`
- [x] Seed copia para `wwwroot/uploads/seed-products/` (idempotente — não recopia se existe)
- [x] Produtos/SKUs: skip se slug já existe
- [x] Inventory: seed separado após catálogo
- [x] URLs: `/uploads/seed-products/<arquivo>.png` via `Uploads__PublicBaseUrl`
- [x] Falha em Testing/Staging se imagens obrigatórias ausentes
- [ ] Após deploy: validar `curl -I .../uploads/seed-products/camiseta-basica-branca.png`

---

## 10. Checklist DataProtection / uploads

- [x] Volume `dataprotection_test` → `/app/dataprotection-keys`
- [x] Volume `dataprotection_hml` → `/app/dataprotection-keys`
- [x] Volume `api_test_uploads` / `api_hml_uploads` → `/app/wwwroot/uploads`
- [ ] **Crítico:** sem volume DataProtection, cookies e CSRF invalidam a cada recreate
- [ ] Após primeiro deploy com volume novo, usuários precisam logar novamente (esperado)

---

## 11. Checklist CORS / CSRF / cookies

### CORS

- [x] Sem wildcard `*`
- [x] `AllowedOrigins__0` = origem exata do frontend
- [x] `AllowCredentials()` habilitado
- [x] `UseCors` antes de auth/endpoints
- [x] OPTIONS ignorado pelo CSRF middleware

### CSRF

- [x] Mutations admin exigem `X-CSRF-TOKEN`
- [x] `GET /api/auth/csrf` disponível
- [x] Frontend: `credentials: "include"` + CSRF em mutations

### Cookies (Testing/Staging)

- [x] HttpOnly
- [x] Secure (Always fora de Development)
- [x] SameSite=Lax
- [x] Cookies `__Host-*` sem Domain
- [x] Admin e customer em schemes/cookies separados

---

## 12. Checklist frontend

- [x] `VITE_API_BASE_URL` por ambiente (`.env.test.example` / `.env.hml.example`)
- [x] Build produção falha sem `VITE_API_BASE_URL` (`env.ts`)
- [x] `_redirects` para SPA
- [x] `/admin/login`, `/login`, `/register`, `/forgot-password`
- [x] `/account/*` protegido (`CustomerRouteGuard`)
- [x] `/admin/*` protegido (`AdminRouteGuard`)
- [x] Imagens resolvidas via `resolveCatalogImageUrl` → origem da API
- [x] Sem JWT/localStorage para auth
- [ ] Cloudflare Pages: rebuild com env correta após merge

---

## 13. Comandos — build local

```bash
# API
cd apps/api
dotnet build ApiGateways/Vls.Shopflow.HttpApi/Vls.Shopflow.HttpApi.csproj

# Worker
dotnet build Workers/Vls.Shopflow.Worker/Vls.Shopflow.Worker.csproj

# Frontend (teste)
cd apps/web
cp .env.test.example .env.test   # local only, não commitar
npm ci && npm run build

# Compose local (deploy/)
cd deploy
cp .env.example .env
cp .env.test.example .env.test
cp .env.hml.example .env.hml
docker compose config
docker compose build api-test worker-test
```

---

## 14. Comandos — deploy teste (VPS)

```bash
cd /opt/shopflow/app/deploy

docker compose config
docker compose config --services

docker compose build --no-cache api-test worker-test
docker compose up -d --force-recreate api-test worker-test

docker compose logs --tail=150 api-test
docker compose logs --tail=150 worker-test
```

Ou via script: `./scripts/deploy-test.sh`

---

## 15. Comandos — deploy HML (VPS)

```bash
cd /opt/shopflow/app/deploy

docker compose build --no-cache api-hml worker-hml
docker compose up -d --force-recreate api-hml worker-hml

docker compose logs --tail=150 api-hml
docker compose logs --tail=150 worker-hml
```

Ou via script: `./scripts/deploy-hml.sh`

---

## 16. Comandos — validação curl

### Health

```bash
curl -i https://api-teste.vipassessoriadigital.com.br/health
curl -i https://api-hml.vipassessoriadigital.com.br/health
```

### Catálogo

```bash
curl -i 'https://api-teste.vipassessoriadigital.com.br/api/catalog/products?page=1&pageSize=50'
curl -i 'https://api-hml.vipassessoriadigital.com.br/api/catalog/products?page=1&pageSize=50'
```

### Imagens seed

```bash
curl -I https://api-teste.vipassessoriadigital.com.br/uploads/seed-products/camiseta-basica-branca.png
curl -I https://api-hml.vipassessoriadigital.com.br/uploads/seed-products/camiseta-basica-branca.png
```

### CORS preflight — teste

```bash
curl -sS -D - -o /dev/null -X OPTIONS \
  'https://api-teste.vipassessoriadigital.com.br/api/catalog/products?page=1&pageSize=50' \
  -H 'Origin: https://teste.vipassessoriadigital.com.br' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Access-Control-Request-Headers: content-type'
```

### CORS preflight — HML

```bash
curl -sS -D - -o /dev/null -X OPTIONS \
  'https://api-hml.vipassessoriadigital.com.br/api/catalog/products?page=1&pageSize=50' \
  -H 'Origin: https://hml.vipassessoriadigital.com.br' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Access-Control-Request-Headers: content-type'
```

### Admin sem cookie (esperado 401)

```bash
curl -i -X POST 'https://api-teste.vipassessoriadigital.com.br/api/catalog/products/variant' \
  -H 'Content-Type: application/json' \
  -d '{}'
```

### Admin login

```bash
curl -i -c admin-test.cookies -X POST 'https://api-teste.vipassessoriadigital.com.br/api/auth/admin/login' \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin-teste@vipassessoriadigital.com.br","password":"<SENHA_ADMIN_TESTE>"}'
```

### Admin me

```bash
curl -i -b admin-test.cookies 'https://api-teste.vipassessoriadigital.com.br/api/auth/admin/me'
```

### Customer me sem cookie (esperado 401)

```bash
curl -i 'https://api-teste.vipassessoriadigital.com.br/api/auth/customer/me'
```

### Worker logs

```bash
docker compose logs --tail=50 worker-test | rg -i 'Expiration worker'
docker compose logs --tail=50 worker-hml | rg -i 'Expiration worker'
```

---

## 17. Troubleshooting

| Sintoma | Causa provável | Ação |
|---------|----------------|------|
| `/uploads/seed-products/*.png` 404 | Seed não rodou ou imagens não copiadas | Ver logs `Demo clothing catalog seed`; confirmar `seed-assets` na imagem; restart api |
| Cookie/CSRF falha após deploy | DataProtection sem volume persistente | Confirmar volume `dataprotection_*` e `DataProtection__KeysPath`; usuários relogam |
| Seed não roda | `DemoCatalogSeed__Enabled=false` | Ativar no `.env.*`; rebuild api |
| Seed falha no startup | Imagens ausentes na imagem | Rebuild API com `seed-assets`; verificar logs |
| Worker não sobe | Connection string ausente | Verificar `.env.*`; `docker compose logs worker-test` |
| Worker parado | `ExpirationWorker__Enabled=false` | Ativar no `.env.*`; restart worker |
| CORS bloqueado | `AllowedOrigins__0` errado | Corrigir `.env.*`; restart api |
| Admin login falha | Senha desatualizada | Usar `SHOPFLOW_ADMIN_RESET_PASSWORD=true` temporariamente (RUNBOOK-001) |
| Frontend chama localhost | Build sem `VITE_API_BASE_URL` | Rebuild Cloudflare Pages com env correta |

---

## 18. Rollback simples

1. **Imagem anterior:** se tag/commit anterior disponível, checkout e rebuild:
   ```bash
   git checkout <commit-anterior>
   docker compose build api-test worker-test
   docker compose up -d --force-recreate api-test worker-test
   ```
2. **Somente restart:** se deploy falhou no startup, corrigir `.env` e restart (dados Postgres preservados).
3. **Desabilitar demo seed:** `DemoCatalogSeed__Enabled=false` + restart api (não remove dados existentes).
4. **Desabilitar worker:** `ExpirationWorker__Enabled=false` + restart worker.
5. **Frontend:** rollback no Cloudflare Pages para deployment anterior.

---

## 19. Secrets — pontos de atenção

**Nunca commitar:**

| Arquivo | Local |
|---------|-------|
| `deploy/.env` | Postgres |
| `deploy/.env.test` | API + worker teste |
| `deploy/.env.hml` | API + worker HML |
| `apps/web/.env`, `.env.local`, `.env.test`, `.env.hml` | Frontend build |
| Cookies, tokens, senhas reais | Qualquer lugar |

**Gitignore cobre:**

- `deploy/.gitignore` → `.env`, `.env.test`, `.env.hml`
- `apps/web/.gitignore` → `.env`, `.env.test`, `.env.hml`, `.env.local`

**Na VPS:** editar `.env.*` apenas via SSH; não colar secrets em chat/logs.

---

## 20. Veredito

| Item | Status |
|------|--------|
| Código backend (auth, seed, worker, CORS) | ✅ Pronto |
| Dockerfile API + worker | ✅ Pronto (após correções desta revisão) |
| docker-compose (workers + DataProtection) | ✅ Pronto (após correções) |
| `.env.*.example` | ✅ Completo |
| Frontend | ✅ Pronto (depende rebuild Cloudflare Pages) |
| VPS `.env` reais | ⚠️ Operador deve atualizar manualmente |
| Deploy | ⏸️ Aguardando execução manual |

**Pronto para deploy após:** atualizar `.env.test` e `.env.hml` na VPS com as novas variáveis (DataProtection, worker) e executar os comandos da seção 14/15.
