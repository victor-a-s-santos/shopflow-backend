# Shopflow — Documentação técnica

Shopflow é um e-commerce modular em monorepo. Nesta fase, **dois módulos estão implementados de ponta a ponta**: Catalog (produtos/SKUs) e Inventory (estoque por SKU). O restante da solution (.NET) existe como scaffold, mas ainda não está exposto na API.

## Stack

| Camada | Tecnologias |
|--------|-------------|
| Backend | .NET 10, Clean Architecture, DDD, CQRS, MediatR, FluentValidation, EF Core, PostgreSQL, Minimal APIs |
| Frontend | React, TypeScript, Vite, React Query, shadcn/ui, Tailwind |
| Infra local | Docker Compose (Postgres 16), API `:5127`, Web `:8080` |

## Módulos implementados

| Módulo | Backend | Admin frontend | Loja (vitrine) |
|--------|---------|----------------|----------------|
| **Catalog** | Sim | Sim (`/admin/products`) | Parcial (`/`, `/product/:slug`) |
| **Inventory** | Sim | Sim (`/admin/inventory`) | Não |

## Documentos

| Arquivo | Conteúdo |
|---------|----------|
| [architecture.md](./architecture.md) | Arquitetura, bounded contexts, banco, padrões |
| [architecture/WHOLESALE-SALES-RULES-DESIGN.md](./architecture/WHOLESALE-SALES-RULES-DESIGN.md) | Design regras de venda atacado/pacotes/múltiplos |
| [catalog.md](./catalog.md) | Produtos, SKUs, atributos, endpoints, admin |
| [inventory.md](./inventory.md) | Estoque, reservas, concorrência, admin |
| [testing.md](./testing.md) | Como rodar, testar e validar manualmente |
| [next-steps.md](./next-steps.md) | Próximos módulos e dívidas técnicas |
| [infra/REPO-001-backend-github-setup.md](./infra/REPO-001-backend-github-setup.md) | Preparo do repo GitHub do backend |
| [infra/RUNBOOK-001-vps-setup-deploy.md](./infra/RUNBOOK-001-vps-setup-deploy.md) | Deploy VPS teste/HML |
| [infra/RUNBOOK-004-github-actions-vps-deploy.md](./infra/RUNBOOK-004-github-actions-vps-deploy.md) | Deploy automático via GitHub Actions |

> O frontend vive em outro repositório. Este docs/ acompanha o **backend**.

## Como rodar (resumo)

Na raiz do repositório backend:

```bash
docker compose up --build
```

Ou, sem Docker:

```bash
cd apps/api
dotnet run --project ApiGateways/Vls.Shopflow.HttpApi
```

Detalhes, testes e URLs: [testing.md](./testing.md).

## URLs locais

| Recurso | URL |
|---------|-----|
| Frontend | http://localhost:8080 |
| Admin | http://localhost:8080/admin |
| Estoque (admin) | http://localhost:8080/admin/inventory |
| API | http://localhost:8080/api → via `VITE_API_BASE_URL` → http://localhost:5127/api |
| Scalar (dev) | http://localhost:5127/scalar/v1 |
| Postgres | `localhost:5432` — db `shopflow`, user/pass `postgres` |

## Collections Postman

Na raiz do repositório:

- `Shopflow_Catalog.postman_collection.json`
- `Shopflow_Inventory.postman_collection.json`

## Estrutura do repositório

```
shopflow/
  docker-compose.yml
  docs/                    ← documentação central (este diretório)
  apps/
    api/                   ← backend .NET (Vls.Shopflow.sln)
      ApiGateways/Vls.Shopflow.HttpApi/
      src/Modules/Catalog/
      src/Modules/Inventory/
      tests/
    web/                   ← frontend React
```

## Próximo passo recomendado

**Cart / Checkout** — consumir Inventory (`reserve` → `confirm` / `cancel`). Ver [next-steps.md](./next-steps.md).
