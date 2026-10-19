# Relatórios admin — demanda por produto e pedidos aguardando pagamento

Consultas somente leitura para o backoffice. Não alteram pedido, pagamento, estoque ou checkout.

Autenticação: cookie admin + policy `Backoffice` (role Owner e `is_staff`). GET não exige CSRF. Cliente autenticado não acessa.

## Demanda confirmada

Entra somente pedido com `Order.Status = Paid`. Esse é o estado em que o webhook/reconciliação já chamou `Order.MarkAsPaid`. Não há status de reembolso ou devolução no domínio; pedido pago permanece na demanda.

Ficam de fora: `PendingPayment`, `Canceled`, `Expired`. Pagamento Pix `Paid` com pedido ainda `PendingPayment` (atraso de reconciliação) **não** entra na demanda.

A quantidade é `OrderItem.Quantity` (unidades comerciais do SKU). `packageSize` / `totalPieces` do snapshot são só exibição e não multiplicam o total.

O agrupamento é por `skuId` do item do pedido. Nome, código e `salesDisplay` vêm do snapshot da linha paga mais recente no filtro (`PaidAt`, senão `CreatedAt`, depois `Order.Id` e `OrderItem.Id`). O pedido não guarda `productId` nem atributos de variante (cor/tamanho); esses dados não são reconstruídos pelo catálogo atual.

Pedidos de convidado (`CustomerUserId` nulo) entram quando estão pagos. Contato é o snapshot do pedido (nome, e-mail, telefone). Não há fallback para o cadastro atual do cliente.

### `GET /api/admin/reports/product-demand`

| Param | Default | Notas |
|-------|---------|--------|
| `page` | 1 | ≥ 1 |
| `pageSize` | 20 | 1–100 |
| `from` / `to` | — | `DateTimeOffset`; filtram a confirmação `PaidAt`, com fallback `CreatedAt` se `PaidAt` for nulo. `from ≤ to` |
| `q` | — | nome do produto ou código do SKU (contains) ou `skuId` (Guid). Máx. 200 |
| `sort` | `quantity_desc` | `quantity_asc`, `orders_desc`, `orders_asc`, `subtotal_desc`, `subtotal_asc`. Desempate: `skuId` |

```json
{
  "items": [
    {
      "skuId": "guid",
      "productName": "Camisa",
      "skuCode": "SKU-1",
      "totalQuantity": 4,
      "orderCount": 2,
      "subtotalTotal": 199.80,
      "salesDisplay": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

`subtotalTotal` é a soma dos subtotais históricos das linhas (`quantity × unitPrice` no momento da compra). `salesDisplay` segue a mesma regra dos pedidos (`FixedPackage` / `AssortedPackage` com `packageSize > 1`; caso contrário `null`).

### `GET /api/admin/reports/product-demand/{skuId}/orders`

Mesmos `page`, `pageSize`, `from`, `to`. `skuId` vazio é 400. SKU sem vendas pagas no período devolve página vazia (200).

| `sort` | Default `paidAt_desc` | também `paidAt_asc`, `createdAt_desc`, `createdAt_asc`. Desempate: `orderId` |

Cada item é um pedido pago que contém o SKU. Várias linhas do mesmo SKU no mesmo pedido somam `quantity` e `subtotal`. `unitPrice` vem preenchido quando todas as linhas têm o mesmo preço. `salesDisplay` só quando há uma única linha.

`phoneDigits` são os dígitos do telefone do snapshot (10–15). Não adiciona DDI. Telefone vazio, curto ou longo demais devolve `phoneDigits: null` sem erro. `payment` reutiliza o resumo Pix já usado em Admin Orders (último pagamento; sem QR, copia-e-cola ou segredo) e pode ser `null`.

`isGuest` é `true` quando `CustomerUserId` é nulo.

## Pedidos aguardando pagamento

### `GET /api/admin/reports/unpaid-orders`

Entra somente `Order.Status = PendingPayment`.

Ficam de fora:

- `Paid`, `Canceled`, `Expired`
- pedido cujo **último** Pix (`CreatedAt` desc) já está `Paid`, mesmo que o pedido ainda não tenha sido marcado como pago

Entram pedidos sem Pix, ou com último Pix `Pending`, `Failed`, `Canceled` ou `Expired`, desde que o pedido continue `PendingPayment`. O relatório não consulta o provedor e não cria cobrança.

| Param | Default | Notas |
|-------|---------|--------|
| `page` / `pageSize` | 1 / 20 | 1–100 |
| `from` / `to` | — | filtram `CreatedAt`. `from ≤ to` |
| `q` | — | igual à listagem admin: Guid = id do pedido **ou** `skuId`; `#10582` ou número com até 9 dígitos = `orderNumber`; telefone com 10–11 dígitos não vira número de pedido. Texto: nome, e-mail, telefone, número, nome do produto ou código do SKU |
| `sort` | `createdAt_desc` | `createdAt_asc`. Desempate: `orderId` |

A resposta traz contato (`phone` do snapshot e `phoneDigits`), totais do pedido, `payment` (mesmo resumo seguro) e **todos** os itens do pedido, não só a linha que casou com a busca. Os itens saem ordenados por `OrderItem.Id` (ordem estável; o pedido não tem índice de linha).

## Índices

Não houve migration. A consulta reusa `IX_order_items_SkuId` e `IX_orders_CreatedAt`. Não há índice em `orders.Status` / `PaidAt`; em volume alto, um índice `(Status, PaidAt)` pode ser avaliado depois.

## Testes

- `Vls.Shopflow.Orders.UnitTests`: telefone, validação, busca, exclusão de Pix `Paid` e mapeamento de contato/itens.
- `Vls.Shopflow.Orders.IntegrationTests.AdminDemandReportIntegrationTests`: agregação real no PostgreSQL (pago, pendente, cancelado, expirado, SKUs separados, pacote, convidado, período, paginação, pedidos sem pagamento).
- `EndpointExposureIntegrationTests`: 401 sem login, 403/401 como cliente, 200 como admin.
