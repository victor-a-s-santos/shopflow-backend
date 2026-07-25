# Product list — `salesSummary`

> 2026-07-25 — evita N+1 `by-slug` no ProductCard.

## Por quê

A sales rule vive no **SKU**. A PDP (`by-slug`) já retorna `salesRule` + `salesRuleDisplay` por SKU.

A listagem (`GET /api/catalog/products`) precisa de um **resumo compacto** por produto para o card exibir badge, preço por lote e valor unitário **sem** hidratar cada item via `by-slug`.

## O que é / o que não é

| | `salesSummary` (listagem) | `salesRule` / `salesRuleDisplay` (by-slug) |
|--|---------------------------|--------------------------------------------|
| Escopo | 1 produto (agregado) | 1 SKU |
| Uso | ProductCard / home | PDP, carrinho, seleção de variante |
| Completo? | Não — só flags + preço principal | Sim |

`salesSummary` é **null** quando o produto não tem SKU ativo (ou não tem SKUs).

Calculado apenas para os produtos da **página** após filtro/sort/paginação (`docs/catalog/product-list-pagination-and-ordering.md`).

Não altera checkout, inventory, orders nem admin.

## Agregação

Considera apenas SKUs **ativos** da página (projection na mesma query da listagem).

### `primarySalesMode` / `primaryBadge`

| Situação | Mode | Badge |
|----------|------|-------|
| Só Unit | `Unit` | `null` |
| Só MinimumQuantity | `MinimumQuantity` | `Mín. {n} peças` |
| Só MultipleQuantity | `MultipleQuantity` | `Múltiplos de {step}` |
| Só FixedPackage | `FixedPackage` | `packageLabel` ou `Lote com {n} peças` |
| Só AssortedPackage | `AssortedPackage` | `packageLabel` ou `Lote sortido com {n} peças` |
| Unit + pacote | `Mixed` | `Opções por unidade e lote` |
| Unit + Min/Multiple | `Mixed` | `Opções de compra flexível` |
| Outras misturas | `Mixed` | `Opções de compra` |

### SKU pacote principal

Entre SKUs `FixedPackage` / `AssortedPackage` ativos, escolhe o de **menor** `effectivePrice / packageSize` (AwayFromZero, 2 casas).

### Preços (só display)

- `packagePrice` = preço efetivo do SKU pacote principal (**preço por lote**).
- `equivalentUnitPrice` = `Round(packagePrice / packageSize, 2, AwayFromZero)`.
- `fromPrice` = menor preço unitário comparável entre SKUs ativos:
  - Unit / Min / Multiple → `effectivePrice`
  - Package → `effectivePrice / packageSize`
- `fromPriceLabel` = `"A partir de"`

Exemplo: lote R$ 241 / 3 → `equivalentUnitPrice` = `fromPrice` = **80.33**.

## Contrato (campos)

Ver `ProductSalesSummaryDto` e exemplo em `docs/catalog/sales-rules-contract.md`.

## Frontend

ProductCard consome `item.salesSummary` — sem hidratação `by-slug` na listagem (`apps/web/docs/product-card.md`).
