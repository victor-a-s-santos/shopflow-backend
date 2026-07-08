# Demo catalog seed — loja de roupas

Carga inicial idempotente de produtos de moda para **Development**, **Testing** e **Staging**.

## Objetivo

Popular catálogo e estoque com dados realistas para testar vitrine, admin e checkout sem cadastro manual:

| Item | Quantidade |
|------|------------|
| Produtos | 10 |
| Imagens | 20 (2 por produto / cor) |
| SKUs | 94 (Cor + Tamanho) |
| Inventory items | 94 |
| Estoque padrão | 20 un/SKU (1.880 total) |

## Configuração

Seção `DemoCatalogSeed` (appsettings ou env):

| Chave | Default | Descrição |
|-------|---------|-----------|
| `Enabled` | `false` (base), `true` (Development) | Liga/desliga o seed |
| `CopyImages` | `true` | Copia PNGs para `wwwroot/uploads/seed-products/` |
| `CreateInventory` | `true` | Cria `InventoryItem` por SKU demo |
| `DefaultStockQuantity` | `20` | Quantidade inicial por SKU |

Variáveis de ambiente:

```bash
DemoCatalogSeed__Enabled=true
DemoCatalogSeed__CopyImages=true
DemoCatalogSeed__CreateInventory=true
DemoCatalogSeed__DefaultStockQuantity=20
```

**Production:** manter `Enabled=false` salvo decisão explícita.

## Imagens

**Fonte (versionadas no repo):**

```
apps/api/seed-assets/catalog-products/
```

**Destino público (runtime):**

```
wwwroot/uploads/seed-products/<arquivo-normalizado>.png
```

**URL salva no banco:**

```
/uploads/seed-products/<arquivo-normalizado>.png
```

Arquivos com espaço no nome (jaquetas) são normalizados ao copiar:

| Fonte | Destino |
|-------|---------|
| `jaqueta jeans feminina.png` | `jaqueta-jeans-feminina.png` |
| `jaqueta jeans escura feminina.png` | `jaqueta-jeans-escura-feminina.png` |

## Produtos

| Slug | Nome | Categoria | SKUs |
|------|------|-----------|------|
| `camiseta-basica-algodao` | Camiseta Básica Algodão | Camisetas | 10 |
| `camiseta-oversized` | Camiseta Oversized | Camisetas | 8 |
| `camisa-social-manga-longa` | Camisa Social Manga Longa | Camisas | 8 |
| `blusa-moletom-com-capuz` | Blusa de Moletom com Capuz | Moletons | 8 |
| `jaqueta-jeans-feminina` | Jaqueta Jeans Feminina | Jaquetas | 10 |
| `calca-jeans-masculina-reta` | Calça Jeans Masculina Reta | Calças | 10 |
| `calca-social-alfaiataria` | Calça Social Alfaiataria | Calças | 10 |
| `shorts-social-feminino-linho` | Shorts Social Feminino de Linho | Shorts | 10 |
| `vestido-midi-manga-bufante` | Vestido Midi Manga Bufante | Vestidos | 10 |
| `saia-midi-evase` | Saia Midi Evasê | Saias | 10 |

Atributos globais: **Cor** e **Tamanho** (valores adicionados idempotentemente se ausentes).

## Idempotência

- Produto: slug único — se existir, não recria
- SKU: código único por produto — se existir, pula
- Imagem: URL/`StoragePath` — se existir, pula
- Estoque: `InventoryItem` por `SkuId` — se existir, **não soma** novamente
- Categorias/atributos base: `CatalogDbContextSeed` + valores demo adicionados sem duplicar

## Implementação

| Arquivo | Função |
|---------|--------|
| `Catalog.Infrastructure/Seed/DemoCatalogSeedOptions.cs` | Options |
| `Catalog.Infrastructure/Seed/DemoClothingCatalogSeedData.cs` | Definições estáticas |
| `Catalog.Infrastructure/Seed/DemoClothingCatalogSeed.cs` | Produtos, SKUs, imagens |
| `Inventory.Infrastructure/Seed/DemoClothingInventorySeed.cs` | Estoque inicial |
| `HttpApi/Program.cs` | Orquestra após migrations |

Testes: `tests/Vls.Shopflow.Catalog.IntegrationTests/DemoClothingCatalogSeedIntegrationTests.cs`

## Como rodar

### Local (dotnet)

```bash
cd apps/api
dotnet run --project ApiGateways/Vls.Shopflow.HttpApi
```

Com `appsettings.Development.json` (`DemoCatalogSeed:Enabled=true`).

### Docker (monorepo)

```bash
docker compose build api
docker compose up -d api
```

### VPS (deploy/)

Variáveis em `.env.test` / `.env.hml` — ver `deploy/.env.test.example`.

## Validar

```bash
curl http://localhost:5127/api/catalog/products
curl http://localhost:5127/api/catalog/products/by-slug/camiseta-basica-algodao
curl http://localhost:5127/api/catalog/products/by-slug/vestido-midi-manga-bufante
curl -I http://localhost:5127/uploads/seed-products/camiseta-basica-branca.png
```

Inventory (SKU id do produto acima):

```bash
curl http://localhost:5127/api/inventory/skus/{skuId}
```

## Limitações

- **Sem campo descrição** no domínio `Product` — nomes realistas; descrições curtas/longas do prompt não persistidas
- **Categorias planas** — hierarquia Feminino/Masculino/Unissex não existe no schema
- **Imagens por produto**, não por SKU/cor individual no modelo atual
- **Production** desabilitado por padrão

## Próximo passo

Conectar vitrine/admin para exibir catálogo demo após deploy; validar seleção de variantes Cor/Tamanho no frontend.
