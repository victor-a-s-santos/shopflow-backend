# Deploy Shopflow — teste e homologação

Infraestrutura Docker Compose para publicar as APIs de **teste** e **homologação** em uma VPS única, com Caddy (TLS) e PostgreSQL compartilhado.

Documentação da decisão: [docs/infra/ADR-002-deploy-docker-compose-vps.md](../docs/infra/ADR-002-deploy-docker-compose-vps.md)

**Validação pré-deploy:** [docs/infra/DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md](../docs/infra/DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md)

**Deploy automático (CI):** [docs/infra/RUNBOOK-004-github-actions-vps-deploy.md](../docs/infra/RUNBOOK-004-github-actions-vps-deploy.md)

## Estrutura

```
deploy/
├── caddy/Caddyfile          # reverse proxy (api-teste, api-hml)
├── postgres/init-databases.sql
├── scripts/
│   ├── deploy-test.sh       # api-test + worker-test
│   ├── deploy-hml.sh        # api-hml + worker-hml
│   ├── migrate-test.sh
│   └── migrate-hml.sh
├── docker-compose.yml       # postgres, caddy, api-*, worker-*
├── .env.example             # Postgres (interpolação do Compose)
├── .env.test.example        # api-test + worker-test
└── .env.hml.example         # api-hml + worker-hml
```

## Pré-requisitos

- Docker Engine 24+ e Docker Compose v2
- Na VPS: portas **80** e **443** liberadas no firewall
- DNS (quando disponível) apontando para a VPS:
  - `api-teste.vipassessoriadigital.com.br`
  - `api-hml.vipassessoriadigital.com.br`

## Configuração inicial

Três arquivos de ambiente, cada um com responsabilidade distinta:

| Arquivo | Uso | Versionado |
|---------|-----|------------|
| `.env` | Credenciais do Postgres (interpolação no `docker-compose.yml`) | Não |
| `.env.test` | API teste, worker teste, CORS, admin seed, demo seed, DataProtection | Não |
| `.env.hml` | API HML, worker HML, CORS, admin seed, demo seed, DataProtection | Não |

```bash
cd deploy

cp .env.example .env
cp .env.test.example .env.test
cp .env.hml.example .env.hml
```

Edite os três arquivos e substitua os placeholders:

- `.env` → `CHANGE_ME_STRONG_PASSWORD` (Postgres)
- `.env.test` / `.env.hml` → senhas nas connection strings, `SHOPFLOW_ADMIN_PASSWORD` e demais valores sensíveis

A senha em `.env` deve ser a mesma usada nas connection strings de `.env.test` e `.env.hml`.

Nunca commite `.env`, `.env.test` ou `.env.hml`. Os arquivos reais na VPS devem conter **senhas fortes** distintas dos placeholders dos `.example`.

### Admin seed

Em **Testing** e **Staging**, a API exige `SHOPFLOW_ADMIN_EMAIL` e `SHOPFLOW_ADMIN_PASSWORD` no startup. Se ausentes, o container falha com:

```
Admin seed configuration is required in non-development environments.
```

| Ambiente | Arquivo | E-mail (placeholder) | Senha |
|----------|---------|----------------------|-------|
| Teste | `.env.test` | `admin-teste@vipassessoriadigital.com.br` | Forte, definida na VPS |
| Homologação | `.env.hml` | `admin-hml@vipassessoriadigital.com.br` | Forte, definida na VPS |

`SHOPFLOW_ADMIN_NAME` é opcional (default: `Shopflow Admin`).

`SHOPFLOW_ADMIN_RESET_PASSWORD=false` por padrão. Se o admin já existir, o seed não altera a senha. Para rotação, defina temporariamente `true`, redeploy, teste login e volte para `false` (ver RUNBOOK-001).

Em **Development** (docker-compose na raiz), as mesmas variáveis vêm do `.env` do monorepo via `SHOPFLOW_ADMIN_*` — o seed é opcional e só roda se configurado.

### Separação de variáveis

- **`env_file` por serviço** — `api-test` e `worker-test` carregam só `.env.test`; `api-hml` e `worker-hml` carregam só `.env.hml`.
- **`.env` compartilhado** — usado apenas para `${POSTGRES_USER}` e `${POSTGRES_PASSWORD}` no serviço `postgres`.
- **`ASPNETCORE_ENVIRONMENT`** — definido no `docker-compose.yml` (`Testing` / `Staging`).
- **DataProtection** — volume persistente `dataprotection_*` em `/app/dataprotection-keys` (cookies + CSRF sobrevivem a redeploys).
- **Worker** — container separado, sem porta pública; expira checkout/order/pix pendentes.

Não use `docker compose --env-file .env.test` para subir a stack inteira: isso mistura responsabilidades. O Compose carrega `.env` automaticamente do diretório `deploy/`.

**Por que não `profiles`?** Profiles servem para ativar serviços opcionais. Aqui teste e hml rodam **simultaneamente** na mesma VPS — `env_file` por serviço é a abordagem correta.

## Subir localmente

Útil para validar o Compose antes de enviar à VPS.

```bash
cd deploy

# 1. Configure .env, .env.test e .env.hml (passo acima)

# 2. Suba toda a stack
docker compose up -d --build

# 3. Verifique os containers
docker compose ps
docker compose logs -f api-test
```

### Testar sem DNS público

Como o Caddy usa hostnames reais, para testes locais adicione ao `/etc/hosts`:

```
127.0.0.1 api-teste.vipassessoriadigital.com.br
127.0.0.1 api-hml.vipassessoriadigital.com.br
```

Em ambiente local sem certificado válido, o Caddy pode falhar no HTTPS. Para smoke test rápido da API, acesse diretamente o container:

```bash
docker compose exec api-test wget -qO- http://localhost:8080/api/catalog/products || true
docker compose exec api-hml wget -qO- http://localhost:8080/api/catalog/products || true
```

Confirme que cada API usa o banco correto:

```bash
docker compose exec postgres psql -U shopflow -d shopflow_test -c '\dt'
docker compose exec postgres psql -U shopflow -d shopflow_hml -c '\dt'
```

## Deploy na VPS

### 1. Preparar o servidor

```bash
# Instalar Docker (Ubuntu/Debian — ajuste conforme sua distro)
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
```

Clone o repositório na VPS (ou envie os artefatos via `git pull` após push).

### 2. Configurar variáveis

```bash
cd deploy
cp .env.example .env
cp .env.test.example .env.test
cp .env.hml.example .env.hml
nano .env          # POSTGRES_USER e POSTGRES_PASSWORD
nano .env.test     # connection strings, CORS, SHOPFLOW_ADMIN_*
nano .env.hml      # connection strings, CORS, SHOPFLOW_ADMIN_*
```

### 3. Primeira subida (stack completa)

```bash
docker compose up -d --build
```

Isso sobe: `postgres`, `caddy`, `api-test` e `api-hml`.

### 4. Deploys subsequentes por ambiente

```bash
# Apenas teste
./scripts/deploy-test.sh

# Apenas homologação
./scripts/deploy-hml.sh
```

### 5. Migrations

A API aplica migrations automaticamente no startup. Após atualizar a imagem:

```bash
./scripts/migrate-test.sh
./scripts/migrate-hml.sh
```

## Endpoints esperados

| Ambiente | URL | Banco | `ASPNETCORE_ENVIRONMENT` |
|----------|-----|-------|--------------------------|
| Teste | https://api-teste.vipassessoriadigital.com.br | `shopflow_test` | `Testing` |
| Homologação | https://api-hml.vipassessoriadigital.com.br | `shopflow_hml` | `Staging` |

Rotas da API seguem o prefixo `/api` (ex.: `/api/catalog/products`).

Health check público (sem auth): `GET /health` → `{ "status": "ok", "environment": "Testing" | "Staging" }`.  
Runbook: [docs/infra/RUNBOOK-001-vps-setup-deploy.md](../docs/infra/RUNBOOK-001-vps-setup-deploy.md)

## Frontend (futuro)

Os subdomínios `teste` e `hml` estão reservados para o frontend. Quando houver build de produção do `apps/web`, adicione os blocos correspondentes no `caddy/Caddyfile`.

## Troubleshooting

| Problema | Ação |
|----------|------|
| Certificado TLS não emitido | Confirme DNS apontando para a VPS e portas 80/443 abertas |
| API não conecta ao Postgres | Verifique senha igual em `.env` e nas connection strings |
| Bancos não existem | Volume Postgres criado antes do init? Remova o volume e suba de novo (apenas em ambiente descartável) |
| `POSTGRES_PASSWORD is required` | Crie `.env` a partir de `.env.example` |
| `env file .env.test not found` | Copie `.env.test.example` para `.env.test` |
| API crash no startup (admin seed) | Defina `SHOPFLOW_ADMIN_EMAIL` e `SHOPFLOW_ADMIN_PASSWORD` em `.env.test` ou `.env.hml` |

## O que não está incluído

- Produção
- Cloudflare
- GitHub Actions (preparado para deploy manual)
- Frontend nos subdomínios `teste` / `hml`
