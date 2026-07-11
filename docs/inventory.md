# Inventory

Módulo responsável pelo estoque físico, reservas e movimentações. **Fonte da verdade** de disponibilidade — Catalog não persiste `stockQuantity`.

## Escopo

- Inventário por SKU (`quantityOnHand`, `quantityReserved`)
- Disponibilidade: `availableQuantity = quantityOnHand - quantityReserved`
- Movimentações (entrada/saída)
- Reserva / confirm / cancel (uso interno: CartCheckout, Expiration worker; HTTP admin técnico)
- Endpoint batch Backoffice para preview de estoque no Admin Product Edit

## Fronteira Catalog / Inventory

| Responsabilidade | Módulo |
|------------------|--------|
| Produto, SKU, preço, atributos | Catalog |
| Quantidade em estoque / reserva | Inventory |

Editar produto no Admin **não** grava estoque. Ajuste real continua em Admin → Estoque.

## Endpoints

| Método | Path | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/api/inventory/skus/{skuId}` | Público (shape seguro) / Backoffice (completo) | Disponibilidade individual |
| `GET` | `/api/inventory/skus/{skuId}/movements` | Backoffice | Movimentações |
| `POST` | `/api/inventory/skus/{skuId}` | Backoffice + CSRF | Criar inventário |
| `POST` | `/api/inventory/skus/{skuId}/add` | Backoffice + CSRF | Entrada |
| `POST` | `/api/inventory/skus/{skuId}/remove` | Backoffice + CSRF | Saída |
| `POST` | `/api/admin/inventory/skus/availability` | Backoffice + CSRF | **Batch** disponibilidade (somente leitura) |
| `POST` | `/api/admin/inventory/skus/{skuId}/reserve` | Backoffice + CSRF | Reserva (técnico) |
| `POST` | `/api/admin/inventory/reservations/{id}/confirm` | Backoffice + CSRF | Confirmar (técnico) |
| `POST` | `/api/admin/inventory/reservations/{id}/cancel` | Backoffice + CSRF | Cancelar (técnico) |

### Batch — disponibilidade Admin

```
POST /api/admin/inventory/skus/availability
{ "skuIds": ["uuid-1", "uuid-2"] }
```

Regras:

- Somente leitura — não cria inventário, não reserva, não altera estoque
- Máximo **100** `skuIds` por request
- Ordem da resposta = ordem do request (duplicatas preservadas)
- SKU sem inventário → `exists: false` e quantidades `null`
- Não consulta Catalog

Resposta:

```json
{
  "items": [
    {
      "skuId": "uuid-1",
      "availableQuantity": 20,
      "quantityOnHand": 25,
      "reservedQuantity": 5,
      "exists": true
    },
    {
      "skuId": "uuid-2",
      "availableQuantity": null,
      "quantityOnHand": null,
      "reservedQuantity": null,
      "exists": false
    }
  ]
}
```

Uso esperado: Admin Product Edit substitui N× `GET /api/inventory/skus/{skuId}` por uma chamada batch.

## Testes

| Projeto | Cobertura |
|---------|-----------|
| `Vls.Shopflow.Inventory.UnitTests` | Domain + handlers (incl. batch) |
| `Vls.Shopflow.Inventory.IntegrationTests` | Operações atômicas / concorrência |
| `Vls.Shopflow.IdentityAccess.IntegrationTests` | Exposição Backoffice/CSRF do batch |

## Próximos passos

1. Frontend Admin Product Edit consumir o batch
2. (Opcional) batch para listagem Admin Inventory
