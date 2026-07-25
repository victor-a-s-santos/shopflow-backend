# Admin products listing

> 2026-07-25 — listagem Backoffice paginada, separada da vitrine.

## Por que não usar a listagem pública

| | Público `GET /api/catalog/products` | Admin `GET /api/admin/catalog/products` |
|--|-------------------------------------|----------------------------------------|
| Auth | anônimo | **Backoffice** |
| Escopo | ativos + ≥1 SKU ativo | todos (ativos, inativos, sem SKU, sem imagem) |
| Resposta | `ProductDto` + `salesSummary` | `AdminProductListItemDto` resumido |
| Ordem default | featured → displayOrder → createdAt | `createdAt` DESC |
| Uso | home / “Carregar mais” | tabela de gestão do catálogo |

## Endpoint

`GET /api/admin/catalog/products`

| Param | Default | Notas |
|-------|---------|--------|
| `page` | `1` | ≥ 1 |
| `pageSize` | `20` | 1–100 |
| `q` | — | nome, slug ou código de SKU (case-insensitive) |
| `categorySlug` / `categoryId` | — | match exato; slug inexistente → página vazia |
| `status` | `all` | `all` \| `active` \| `inactive` |
| `featured` | `all` | `all` \| `featured` \| `not_featured` |
| `sort` | `default` | ver abaixo |

### Resposta

```json
{
  "items": [
    {
      "id": "...",
      "name": "Produto",
      "slug": "produto",
      "isActive": true,
      "isFeatured": false,
      "displayOrder": null,
      "createdAt": "2026-07-25T...",
      "category": { "id": "...", "name": "Calças", "slug": "calcas" },
      "primaryImageUrl": "...",
      "skuCount": 3,
      "activeSkuCount": 2,
      "minPrice": 80.33,
      "hasPromotionalPrice": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 120,
  "totalPages": 6,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "total": 120
}
```

### Sorts

| sort | Critério |
|------|----------|
| `default` / `newest` | `createdAt` DESC, `id` ASC |
| `oldest` | `createdAt` ASC, `id` ASC |
| `name_asc` / `name_desc` | nome + `id` |
| `display_order` | displayOrder ASC nulls last → featured → createdAt |
| `featured` | featured → displayOrder → createdAt |
| `price_asc` / `price_desc` | min preço efetivo (SKUs ativos; senão qualquer SKU); sem SKU por último |

`UpdatedAt` **não** é sort — o domínio Product não tem esse campo.

### Indicadores

- `skuCount` / `activeSkuCount` — contagens no banco
- `minPrice` — menor preço efetivo (promo vs regular) entre SKUs ativos; se nenhum ativo, entre todos; `null` sem SKUs
- `hasPromotionalPrice` — algum SKU (preferindo ativos) tem promo

Detalhe completo continua em `GET /api/catalog/products/{id}` (Backoffice).

## Pendência FE

Tela Admin Products deve consumir este endpoint (não a listagem pública com `pageSize=48`).
