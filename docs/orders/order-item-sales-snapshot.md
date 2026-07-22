# Order item — sales rule snapshot

> Fase 4 backend — 2026-07-22. Design: `docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md`.

## Por que existe

O pedido precisa continuar exibindo a regra comercial **do momento da compra**, mesmo se o SKU mudar depois.

Exemplo: pedido de **2 lotes de 3 peças (= 6 peças)**. Se o lojista alterar o SKU para lote de 6, o pedido antigo **não** pode passar a parecer “2 lotes de 6”.

## Onde é capturado

1. `CreateCheckoutSession` — ao criar cada `CheckoutSessionItem`, a partir do `SkuSalesRule` + `quantity` + `unitPrice` efetivo.
2. `CreateOrderFromCheckoutSession` — copia o snapshot item a item para `OrderItem` (**não** relê o catálogo atual).

Sessões antigas (pré-migration) sem `SalesMode`: ao virar Order, fallback `SalesMode = Unit` (sem inventar packageSize do catálogo atual).

## Campos persistidos (nullable em linhas antigas)

Em `cartcheckout.checkout_session_items` e `orders.order_items`:

| Campo | Uso |
|-------|-----|
| `SalesMode` | Modo no checkout |
| `PackageSize` | Peças por unidade vendida (só display) |
| `PackageLabel` / `PackageDescription` | Copy |
| `QuantityUnitLabel` | `lote(s)`, `pacote(s)`, … |
| `ShowTotalPieces` | Flag de UI |
| `TotalPieces` | `quantity * packageSize` (pacote) |
| `EquivalentUnitPrice` | `UnitPrice / packageSize` (2 casas, AwayFromZero) |
| `SalesDisplaySummary` | Ex.: `2 lote(s) = 6 peças` |

## Semântica que **não** muda

- `quantity` = unidades do SKU vendido (lotes, não peças internas).
- Subtotal = `quantity × UnitPrice` (sem × packageSize).
- Inventory reserva `quantity` (sem × packageSize).
- Snapshot **não** entra em cálculo de Pix/pagamento.

## DTO `salesDisplay`

Exposto em detalhes Admin / Customer / Guest (e `OrderItemDto` do create):

```json
{
  "salesMode": "FixedPackage",
  "packageSize": 3,
  "packageLabel": "Lote com 3 peças",
  "packageDescription": null,
  "quantityUnitLabel": "lote(s)",
  "showTotalPieces": true,
  "totalPieces": 6,
  "equivalentUnitPrice": 80.33,
  "summary": "2 lote(s) = 6 peças"
}
```

- **FixedPackage / AssortedPackage** com `packageSize > 1` → preenchido.
- **Unit / Minimum / Multiple** (e pedidos antigos sem snapshot) → `salesDisplay: null`.

## Frontend

Consumido em Admin Order Detail, Customer Order Detail e Guest tracking (`apps/web/docs/order-sales-display.md`).
