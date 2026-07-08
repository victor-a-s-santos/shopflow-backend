# Shopflow Backend

Backend do e-commerce modular Shopflow (API HTTP, worker de expiração, deploy teste/HML e documentação de infraestrutura).

O frontend (`apps/web`) vive em **outro repositório** e não faz parte deste projeto.

## Stack

- **.NET 10**
- **ASP.NET Core Minimal APIs**
- **EF Core** + **PostgreSQL 16**
- **Docker Compose**
- **Caddy** (TLS / reverse proxy nos ambientes teste e HML)
- **Worker de expiração** (checkout / pedidos / pagamentos)

## Estrutura

```
.
├── apps/
│   └── api/
│       ├── Dockerfile
│       ├── Dockerfile.worker
│       ├── ApiGateways/
│       ├── Workers/
│       ├── src/
│       ├── tests/
│       └── seed-assets/catalog-products/
├── deploy/
│   ├── docker-compose.yml
│   ├── .env.test.example
│   ├── .env.hml.example
│   ├── caddy/
│   ├── postgres/
│   └── scripts/
├── docs/
│   └── infra/
├── docker-compose.yml          # desenvolvimento local (db + api + worker)
├── .gitignore
├── .dockerignore
└── README.md
```

Arquivos de build .NET ficam em `apps/api/` (`Vls.Shopflow.sln`, `Directory.*.props`, `global.json`).

## Como rodar localmente

### Opção A — Docker Compose (raiz)

```bash
cp .env.example .env   # opcional: seed do admin
docker compose up --build
```

| URL | Serviço |
|-----|---------|
| http://localhost:5127 | API |
| http://localhost:5127/scalar/v1 | API docs (dev) |
| localhost:5432 | PostgreSQL |

### Opção B — SDK .NET

```bash
# Postgres local (ou `docker compose up db -d`)
cd apps/api
dotnet restore
dotnet run --project ApiGateways/Vls.Shopflow.HttpApi
```

Detalhes: [apps/api/README.md](apps/api/README.md).

## Como rodar testes

```bash
cd apps/api
dotnet restore
dotnet test
```

## Validar Docker Compose (deploy teste/HML)

Não executa containers — só valida a configuração:

```bash
cd deploy
cp .env.example .env                 # se ainda não existir
cp .env.test.example .env.test       # se ainda não existir
cp .env.hml.example .env.hml         # se ainda não existir
docker compose config
docker compose config --services
```

## Deploy teste / HML

Arquitetura e scripts em [deploy/README.md](deploy/README.md). Resumo:

1. Configurar DNS e VPS conforme o runbook.
2. Copiar os `.example` para `.env`, `.env.test` e `.env.hml` **na VPS** (nunca no Git).
3. Build/up via scripts:
   - `deploy/scripts/deploy-test.sh`
   - `deploy/scripts/deploy-hml.sh`

Documentação:

- [docs/infra/RUNBOOK-001-vps-setup-deploy.md](docs/infra/RUNBOOK-001-vps-setup-deploy.md)
- [docs/infra/DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md](docs/infra/DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md)
- Preparo deste repositório: [docs/infra/REPO-001-backend-github-setup.md](docs/infra/REPO-001-backend-github-setup.md)

## Secrets

- **Nunca** commitar `deploy/.env`, `deploy/.env.test`, `deploy/.env.hml` ou qualquer `.env` real.
- Use apenas os arquivos `*.example` versionados.
- Não versionar cookies, tokens, chaves DataProtection, dumps ou uploads de runtime.

## Observações

- Frontend/web está em outro repositório.
- Integração **Mercado Pago** ainda pausada.
- **PaymentsPix** continua Fake / Pending (sem provider real).

## Collections Postman

Na raiz: `Shopflow_Catalog.postman_collection.json`, `Shopflow_Inventory.postman_collection.json`.
