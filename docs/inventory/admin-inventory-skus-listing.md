# Admin Inventory SKUs listing

> 2026-07-25 — listagem Backoffice paginada, SKU-cêntrica, com estoque operacional.

## Por que não usar Catalog Admin / listagem pública

| | Público `GET /api/catalog/products` | Admin Products `GET /api/admin/catalog/products` | **Admin Inventory SKUs** `GET /api/admin/inventory/skus` |
|--|-------------------------------------|--------------------------------------------------|----------------------------------------------------------|
| Auth | anônimo | Backoffice | **Backoffice** |
| Unidade | produto (vitrine) | produto (gestão) | **SKU** (estoque) |
| Escopo | ativos + ≥1 SKU ativo | todos os produtos | todos os SKUs (+ estoque 0 se sem `inventory_items`) |
| Estoque | não | não | físico / reservado / disponível + `stockStatus` |
| Uso | home / cards | tabela de catálogo | tabela Inventory Admin |

Não usar `/api/catalog/products` nem `getProductById` em loop no Inventory Admin.

## Endpoint

`GET /api/admin/inventory/skus`

| Param | Default | Notas |
|-------|---------|--------|
| `page` | `1` | ≥ 1 |
| `pageSize` | `20` | 1–100 |
| `q` | — | `productName`, `productSlug` ou `skuCode` (case-insensitive) |
| `productId` | — | SKUs de um produto |
| `categorySlug` / `categoryId` | — | match exato; slug inexistente → página vazia |
| `status` | `all` | `all` \| `active` \| `inactive` |
| `stockStatus` | `all` | `all` \| `in_stock` \| `low_stock` \| `out_of_stock` \| `reserved` |
| `sort` | `default` | ver abaixo |

### Status (produto + SKU)

- `active`: produto **e** SKU ativos
- `inactive`: SKU inativo **ou** produto inativo

### stockStatus (filtro)

- `in_stock`: `available > 5`
- `low_stock`: `0 < available ≤ 5`
- `out_of_stock`: `available ≤ 0`
- `reserved`: `reservedQuantity > 0` (independente do disponível)

Threshold de low-stock: **5** (default; sem config admin neste MVP).

### stockStatus (campo no item)

Prioridade de exibição:

1. `reserved` — available ≤ 0 e reserved > 0  
2. `out_of_stock` — available ≤ 0  
3. `low_stock` — available ≤ 5  
4. `in_stock` — available > 5  

### Estoque

Fonte: `inventory.inventory_items` (`QuantityOnHand`, `QuantityReserved`).

- `physicalQuantity` = on hand (0 se sem linha)
- `reservedQuantity` = reserved
- `availableQuantity` = `GREATEST(onHand - reserved, 0)`

### Sort

| Valor | Ordem |
|-------|--------|
| `default` / `product_name_asc` | productName ASC, skuCode ASC, skuId |
| `product_name_desc` | productName DESC, skuCode ASC, skuId |
| `sku_code_asc` / `sku_code_desc` | skuCode ±, productName ASC, skuId |
| `stock_asc` / `stock_desc` | physicalQuantity ±, productName, skuId |
| `available_asc` / `available_desc` | availableQuantity ±, productName, skuId |
| `reserved_desc` | reservedQuantity DESC, productName, skuId |
| `price_asc` / `price_desc` | effectivePrice ± NULLS LAST, productName, skuId |

`createdAt` no item = `products.CreatedAt` (SKU não tem timestamp próprio).

### Resposta

Mesmo envelope de paginação do projeto (`items`, `page`, `pageSize`, `totalItems`, `totalPages`, `hasNextPage`, `hasPreviousPage`, `total`).

Campos do item: `skuId`, `productId`, `productName`, `productSlug`, `productIsActive`, `skuCode`, `skuIsActive`, `category`, `primaryImageUrl`, preços, quantidades, `stockStatus`, `salesMode` / package fields, `createdAt`.

## Implementação

`AdminInventorySkuReadModel` — SQL cross-schema (Catalog + Inventory) via `InventoryDbContext`, **sem** HTTP interno e **sem** N+1.

## Segurança

Exige `AuthPolicies.Backoffice`. Anônimo → 401; customer → 403; admin → 200.

## Pendência FE

Tela Inventory Admin deve consumir este endpoint (deixar de indexar via Admin Products + `getProductById`).
