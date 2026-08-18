# Inventory — movimentações e baixa de estoque

## Conceitos

| Campo | Significado |
|-------|-------------|
| `QuantityOnHand` | Físico |
| `QuantityReserved` | Reservado (checkout) |
| **Available** | `OnHand - Reserved` |

## Baixa operacional

`POST /api/inventory/skus/{skuId}/remove`  
Body:

```json
{ "quantity": 2, "reason": "Avaria" }
```

Regras:

- `quantity` > 0 (obrigatório).
- `reason` obrigatório (trim, máx. 500).
- Baixa **não pode** consumir reservado: a condição SQL exige `(OnHand - Reserved) >= quantity`.
- Resposta **200** com saldo resultante:

```json
{
  "skuId": "...",
  "quantityOnHand": 8,
  "quantityReserved": 2,
  "availableQuantity": 6
}
```

Erros:

- 400 validação (`quantity`, `reason`, `skuId`) em ProblemDetails.
- 409 `INSUFFICIENT_AVAILABLE_STOCK` com `requested` / `available` se exceder disponível.
- 404 se SKU não existe no catálogo.

## Outras movimentações

- `POST .../add` — entrada (reason opcional hoje).
- Reserva/confirm/cancel — uso interno (CartCheckout / worker); superfície admin técnica em `/api/admin/inventory/...`.

Não há idempotency-key na baixa neste MVP; o frontend deve desabilitar double-submit.
