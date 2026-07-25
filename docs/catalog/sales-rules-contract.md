# Sales rules (SKU) — contrato backend

> Fase 1 — 2026-07-19 (terminologia lote §3.1 — 2026-07-22).  
> Design: `docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md`.

## Objetivo

Regras de venda (unidade, mínimo, múltiplos, lote/pacote) no **SKU**, validadas no **CreateCheckoutSession**. Frontend só UX.

## Terminologia: lote vs package

No domínio técnico os nomes `Package*` / `FixedPackage` / `AssortedPackage` permanecem.

Na **exibição** (negócio), a referência do cliente é **lote**:

| Conceito | Significado |
|----------|-------------|
| 1 lote | 1 unidade vendável do SKU pacote/lote |
| `packageSize` | peças **dentro** de 1 lote (só display) |
| `quantity` | quantidade de lotes (ou peças, se Unit) |
| preço do SKU (`regularPrice` / promo) | **preço por lote** |
| valor unitário (display) | `preçoEfetivoDoSku / packageSize` |
| reserva Inventory | `quantity` lotes — **nunca** `quantity * packageSize` |

Exemplo (CORSLET 1146): `packageSize=3`, `quantity=2` → reserva **2**, display **6** peças, preço por lote R$ 241 → unitário ≈ R$ 80,33.

`quantityUnitLabel` / `packageLabel` são livres para linguagem de negócio, por exemplo:

- `lote(s)` (default em modos pacote)
- `pacote(s)`
- `kit(s)`
- `caixa(s)`
- `peça(s)` (modos unitários)

Defaults gerados se omitidos: `quantityUnitLabel = "lote(s)"`, `packageLabel = "Lote com {n} peças"`.

Não assumir que todo lote é sortido:

- `FixedPackage` = lote/pacote fechado com quantidade definida (cliente pode escolher variante do SKU, se `allowCustomerToChooseVariants`).
- `AssortedPackage` = lote/pacote sortido (`allowCustomerToChooseVariants` forçado `false`).

## Semântica de `quantity`

**Sempre unidades do SKU vendido.**

| Modo | quantity=2 | Reserva Inventory | Peças (display) |
|------|------------|-------------------|-----------------|
| Unit / Minimum / Multiple | 2 peças | 2 | 2 |
| FixedPackage / AssortedPackage | 2 lotes | **2** | `2 * packageSize` |

Nunca multiplicar `packageSize` na reserva.

## Sales modes

`Unit` | `MinimumQuantity` | `MultipleQuantity` | `FixedPackage` | `AssortedPackage`  
(`ClosedGrid` = pós-MVP)

## Campos `salesRule`

| Campo | Tipo | Notas |
|-------|------|-------|
| salesMode | string enum | |
| minimumQuantity | int | ≥ 1 |
| quantityStep | int | ≥ 1 |
| packageSize | int? | > 1 se lote/pacote |
| packageLabel | string? | gerado: `Lote com {n} peças` |
| packageDescription | string? | |
| quantityUnitLabel | string? | default `lote(s)` / `peça(s)` na leitura |
| allowCustomerToChooseVariants | bool | AssortedPackage força false |
| showTotalPieces | bool | pacote/lote default true |
| isWholesaleOnly | bool | ignorado na vitrine MVP |

## Admin write

`POST .../variants` (create) — `salesRule` opcional. **Ausente → Unit.**

`PUT .../variants/{skuId}` (update):

| Payload | Comportamento |
|---------|---------------|
| `salesRule` **ausente** / `null` | **Preserva** a regra existente (não reseta para Unit) |
| `salesRule` com `salesMode` válido | Substitui a regra |
| `salesRule` com `salesMode` vazio/inválido | **400** ValidationProblemDetails — não reset silencioso |
| Reset para Unit | Enviar explicitamente `"salesMode": "Unit"` (+ min/step 1) |

```json
{
  "code": "CORSLET-1146-LOTE",
  "regularPrice": 241.00,
  "attributes": [],
  "active": true,
  "salesRule": {
    "salesMode": "FixedPackage",
    "minimumQuantity": 1,
    "quantityStep": 1,
    "packageSize": 3,
    "packageLabel": "Lote com 3 peças",
    "packageDescription": null,
    "quantityUnitLabel": "lote(s)",
    "allowCustomerToChooseVariants": true,
    "showTotalPieces": true,
    "isWholesaleOnly": false
  }
}
```

Sortido (quando aplicável):

```json
{
  "salesMode": "AssortedPackage",
  "minimumQuantity": 1,
  "quantityStep": 1,
  "packageSize": 6,
  "packageLabel": "Lote sortido com 6 peças",
  "packageDescription": "Cores sortidas conforme disponibilidade.",
  "quantityUnitLabel": "lote(s)",
  "allowCustomerToChooseVariants": false,
  "showTotalPieces": true,
  "isWholesaleOnly": false
}
```

## Storefront / admin read

### Listagem pública — `salesSummary`

`GET /api/catalog/products` (e qualquer listagem que use `ProductDto`) inclui `salesSummary` compacto por produto (agregado das SKUs **ativas**).

Detalhe: `docs/catalog/product-list-sales-summary.md`.

Objetivo: ProductCard sem N+1 `by-slug`. Não substitui `salesRule` da PDP.

### Detalhe — `salesRule` / `salesRuleDisplay`

`GET /api/catalog/products/{id}` e `by-slug` — cada SKU inclui:

- `salesRule` — regra normalizada (`packageSize`, `quantityUnitLabel`, `packageLabel`, `showTotalPieces`, …)
- `salesRuleDisplay` — **somente** em `FixedPackage` / `AssortedPackage` (null em Unit/Min/Multiple)

Pedidos (Admin/Customer/Guest detail): ver `docs/orders/order-item-sales-snapshot.md` — `salesDisplay` é snapshot histórico do checkout, independente do SKU atual.

### `salesRuleDisplay` (evita divergência de arredondamento no FE)

```json
{
  "sellingUnitLabel": "lote(s)",
  "packageSize": 3,
  "packageSizeLabel": "Unidades no lote",
  "packagePriceLabel": "Preço por lote",
  "equivalentUnitPriceLabel": "Valor unitário",
  "showEquivalentUnitPrice": true,
  "equivalentRegularUnitPrice": 80.33,
  "equivalentPromotionalUnitPrice": null
}
```

Regras:

- Preço por lote na UI = `regularPrice` / `promotionalPrice` / `effectivePrice` do SKU (já é a unidade vendável).
- `equivalent*UnitPrice` = `Math.Round(skuPrice / packageSize, 2, AwayFromZero)` no backend.
- Labels de tamanho/preço derivam de `quantityUnitLabel` (`lote(s)` → “Unidades no lote” / “Preço por lote”).
- Total de peças na UI: `quantity * packageSize` quando `showTotalPieces` (e/ou `salesRule.packageSize`).

Aceite: SKU `FixedPackage` com `regularPrice=241.00` e `packageSize=3` → `equivalentRegularUnitPrice=80.33`.

Display sugerido:

- "Unidades no lote: 3"
- "Preço por lote: R$ 241,00"
- "Valor unitário: R$ 80,33"
- "2 lotes = 6 peças"

## Checkout

`POST /api/checkout/sessions` valida por item consolidado:

```
quantity >= minimumQuantity
AND (quantity - minimumQuantity) % quantityStep == 0
```

Error codes (400 ProblemDetails `code`):

| Code | Uso |
|------|-----|
| `SALES_MIN_QUANTITY` | abaixo do mínimo |
| `SALES_QUANTITY_STEP` | fora do step |
| `SALES_RULE_INVALID_CONFIGURATION` | lote/regra quebrada |
| `SALES_PACKAGE_INVALID` | reservado |

409 permanece para estoque insuficiente.

## Fora do MVP

ClosedGrid, pacote composto multi-SKU, tier pricing, mínimo global, B2B.
