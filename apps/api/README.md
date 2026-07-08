# Shopflow API (`apps/api`)

Gateway HTTP: `ApiGateways/Vls.Shopflow.HttpApi` (ASP.NET Core / .NET 10).

## Requisitos

- .NET SDK **10.0**
- Postgres 16 (local ou Docker)

## Configuração

`ApiGateways/Vls.Shopflow.HttpApi/appsettings.Development.json`:

- `ConnectionStrings:Catalog` — Npgsql
- `Uploads:RootPath` — vazio = `wwwroot/uploads` relativo ao content root
- `Uploads:PublicBaseUrl` — base para URLs absolutas de imagem (ex.: `http://localhost:5127`)

## Rodar localmente

```bash
cd apps/api
dotnet restore
dotnet ef database update \
  -p src/Modules/Catalog/Vls.Shopflow.Catalog.Infrastructure/Vls.Shopflow.Catalog.Infrastructure.csproj \
  -s ApiGateways/Vls.Shopflow.HttpApi/Vls.Shopflow.HttpApi.csproj
dotnet run --project ApiGateways/Vls.Shopflow.HttpApi
```

API: http://localhost:5127 (conforme `launchSettings.json`).

## Migrations (referência)

```bash
dotnet ef migrations add <Nome> \
  -p src/Modules/Catalog/Vls.Shopflow.Catalog.Infrastructure/Vls.Shopflow.Catalog.Infrastructure.csproj \
  -s ApiGateways/Vls.Shopflow.HttpApi/Vls.Shopflow.HttpApi.csproj \
  --output-dir Migrations
```

Ver também [README_DEV.md](README_DEV.md).

## Docker

Na raiz do repositório backend: `docker-compose.yml` — serviços `db`, `api` e `worker` (build context `./apps/api`). Imagens multi-stage: [Dockerfile](Dockerfile) e [Dockerfile.worker](Dockerfile.worker).

Deploy teste/HML: pasta `deploy/` na raiz (ver [deploy/README.md](../../deploy/README.md)).
