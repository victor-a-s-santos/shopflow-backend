# CartCheckout

Módulo responsável por criar e consultar sessões de checkout, reservar estoque e cancelar sessões pendentes.

## Escopo atual

- `POST /api/checkout/sessions` — cria `CheckoutSession` `Pending` com reserva de estoque por item; valida `salesRule` do SKU (`quantity` = unidades do SKU; pacote não multiplica `packageSize` na reserva — ver `docs/catalog/sales-rules-contract.md`)
- `GET /api/checkout/sessions/{id}` — consulta sessão
- `POST /api/checkout/sessions/{id}/cancel` — cancela sessão `Pending` e libera reservas
- TTL de reserva: **15 minutos** (`ReservationExpiresAt` na criação)
- Compensação: falha parcial na reserva cancela reservas já criadas no mesmo request
- **Worker de expiração** — ver `docs/expiration-worker.md` (expira sessões vencidas automaticamente)

## Fora do escopo nesta etapa

- Confirmação de sessão / venda (aguarda pagamento real)
- Shipping / frete
- IdentityCustomer (backend)
- Pagamento integrado neste módulo (Orders + PaymentsPix)

**Checkout convidado:** controlado por `Checkout:AllowGuest` / `AllowGuestCheckout`. Cliente atual = desligado. Ver `docs/checkout/checkout-session.md` e `docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md`. Tracking legado de pedidos guest permanece (`docs/orders/guest-order-access.md`).

## Fluxo — criar sessão

```
POST /api/checkout/sessions
        │
        ▼
Consolidar itens por skuId
        │
        ▼
Para cada item: preço via Catalog + ReserveAsync no Inventory
        │
        ▼
Persistir CheckoutSession (Pending) + checkout_session_items
  (cada item guarda InventoryReservationId)
        │
        ▼
Retornar 201 + sessão (ReservationExpiresAt = now + 15min)
```

## Fluxo — cancelamento manual

```
POST /api/checkout/sessions/{id}/cancel
        │
        ▼
Sessão deve estar Pending
        │
        ▼
CancelReservationAsync para cada item
        │
        ▼
CheckoutSession → Canceled
```

## Fluxo — expiração automática (worker)

Quando `ReservationExpiresAt` vence e a sessão permanece `Pending`:

```
PendingCheckoutExpirationWorker (a cada N segundos)
        │
        ▼
ExpirationProcessor → sessão Expired
        │
        ├─ cancela reservas (Inventory)
        ├─ Order PendingPayment vinculada → Expired
        └─ PixPayment Pending vinculado → Expired
```

Detalhes: `docs/expiration-worker.md`.

## Status da sessão

| Status | Significado |
|--------|-------------|
| `Pending` | Aguardando pagamento; estoque reservado |
| `Canceled` | Cancelamento manual pelo cliente/API |
| `Expired` | TTL vencido (worker) |
| `CompletedSimulated` | Reservado para simulação futura; não usado |

## Endpoints

| Método | Path | Descrição |
|--------|------|-----------|
| `POST` | `/api/checkout/sessions` | Cria sessão + reserva |
| `GET` | `/api/checkout/sessions/{id}` | Consulta |
| `POST` | `/api/checkout/sessions/{id}/cancel` | Cancela + libera estoque |

## Banco de dados

- Schema: `cartcheckout`
- Tabelas: `checkout_sessions`, `checkout_session_items`
- Campo `reservation_expires_at` define elegibilidade para o worker

## Integração cross-module

| Dependência | Uso |
|-------------|-----|
| Catalog | Preço e metadados do SKU |
| Inventory | `ReserveAsync` / `CancelReservationAsync` por `InventoryReservationId` |
| Orders | Lê sessão via `ICheckoutSessionReader` (read-only) |
| Expiration | Expira sessões vencidas e libera reservas |

## Testes

| Projeto | Cobertura |
|---------|-----------|
| `Vls.Shopflow.CartCheckout.UnitTests` | Handlers, domain |
| `Vls.Shopflow.CartCheckout.IntegrationTests` | Compensação de reserva parcial |

## Próximos passos

1. Frontend: status Paid via Guest Order Access Token (`SEC-006`)
2. Shipping no snapshot da sessão
