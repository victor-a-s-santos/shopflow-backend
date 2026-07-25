# Product list — pagination & storefront ordering

> 2026-07-25 — home “Carregar mais” + ordem comercial (não usar `UpdatedAt`).

## Endpoint

`GET /api/catalog/products?page=1&pageSize=16&sort=default&categorySlug=calcas&q=jeans`

| Param | Default | Limites |
|-------|---------|---------|
| `page` | `1` | ≥ 1 |
| `pageSize` | `16` | 1–48 |
| `sort` | `default` | ver abaixo |
| `categorySlug` | — | slug da categoria (exata); server-side **antes** do count/paginação |
| `categoryId` | — | GUID opcional; AND com `categorySlug` se ambos forem enviados |
| `q` | — | busca básica no **nome** do produto (case-insensitive); AND com categoria |

### Filtro por categoria

- Match **exato** em `Category.Slug` (sem árvore de subcategorias — pendência).
- `categorySlug` inexistente → lista vazia (`totalItems=0`, `hasNextPage=false`), **não** 404.
- `GET /api/catalog/categories` agora retorna `slug` por categoria para o FE montar o filtro.
- Frontend **não** deve filtrar categoria client-side após “Carregar mais”.

### Resposta

```json
{
  "items": [ /* ProductDto + salesSummary */ ],
  "page": 1,
  "pageSize": 16,
  "totalItems": 52,
  "totalPages": 4,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "total": 52
}
```

`total` é alias de `totalItems` (compat). Frontend público usa `hasNextPage` para **Carregar mais**.

## Elegibilidade (listagem pública)

- produto `IsActive`
- pelo menos 1 SKU ativo

Inativos / sem SKU ativo não entram no `totalItems`. Estoque não filtra a vitrine neste momento.

Ordem da query: elegibilidade → filtros (`categorySlug` / `categoryId` / `q`) → `Count` → sort → `Skip`/`Take` → `salesSummary` só da página.

## Ordenação

**Não usar `UpdatedAt`.** Edição de texto/imagem/preço não reordena a vitrine.

### `sort=default` (vitrine)

1. `isFeatured` DESC  
2. `displayOrder` ASC (**nulls last**)  
3. `createdAt` DESC  
4. `id` ASC  

### Outros sorts

| sort | Critério |
|------|----------|
| `newest` | `createdAt` DESC, `id` ASC |
| `name_asc` | `name` ASC, `id` ASC |
| `price_asc` / `price_desc` | menor preço unitário comparável (≈ `salesSummary.fromPrice`: pacote = preço/lote ÷ `packageSize`), depois `id` |

Nota: sort por preço usa divisão SQL; arredondamento pode diferir levemente do AwayFromZero do `fromPrice` no card.

## Campos admin (Product)

| Campo | Tipo | Default |
|-------|------|---------|
| `isFeatured` | bool | `false` |
| `displayOrder` | int? | `null` |
| `createdAt` | DateTimeOffset | set no create |

- Create: `POST /api/catalog/products/variant` aceita `isFeatured` / `displayOrder` (flat, opcional)
- Update: `PUT /api/catalog/products/{id}` — objeto opcional `display`:

```json
{
  "name": "...",
  "slug": "...",
  "categoryId": "...",
  "isActive": true,
  "display": { "isFeatured": true, "displayOrder": 10 }
}
```

  Sem `display` → preserva destaque/ordem (compat com admin antigo).
- Detail: `GET /api/catalog/products/{id}` e by-slug retornam `isFeatured`, `displayOrder`, `createdAt`
- Listagem pública **não** expõe esses campos; só aplica a ordem

`displayOrder` negativo → validação 400.

## Relação com `salesSummary`

`salesSummary` continua calculado **só para os itens da página** (após filtro/sort/Skip/Take). Ver `docs/catalog/product-list-sales-summary.md`.

## Pendências FE / admin UI

1. Home: “Carregar mais” com `page` / `hasNextPage` (default `pageSize=16`).
2. Admin form: inputs para destaque e ordem de exibição.
