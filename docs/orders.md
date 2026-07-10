# Orders

Módulo responsável por criar e consultar pedidos a partir de sessões de checkout existentes.

## Escopo MVP (backend)

- Criar pedido a partir de `CheckoutSession` com status `Pending`
- Pedido nasce com status **`PendingPayment`**
- Snapshot imutável de cliente, endereço, itens e totais da sessão
- Uma `CheckoutSession` gera **no máximo um** `Order`
- Checkout convidado permitido (sem `CustomerId`)
- **Sem** pagamento real, Pix, cobrança ou confirmação de estoque vendido
- Expiração automática via worker quando pagamento não ocorre (ver `docs/expiration-worker.md`)

## Fora do escopo nesta etapa

- PaymentsPix / QR Code / webhooks (ver `docs/payments-pix.md` — MVP backend fake existe)
- Marcar pedido como pago
- Confirmar ou cancelar reservas de estoque
- IdentityCustomer / JWT
- Listagem pública por e-mail
- Painel admin de pedidos
- Integração frontend (próxima etapa opcional)

## Fluxo — criar pedido

```
POST /api/orders/from-checkout-session
{ "checkoutSessionId": "guid" }
        │
        ▼
FluentValidation (checkoutSessionId obrigatório)
        │
        ▼
Verificar se já existe Order para checkoutSessionId → 409
        │
        ▼
ICheckoutSessionReader → buscar sessão no schema cartcheckout
        │
        ├─ não encontrada → 404
        ├─ status ≠ Pending → 409
        │
        ▼
Copiar snapshot (customer, address, items, subtotal, total)
        │
        ▼
Persistir Order (status PendingPayment) + order_items
        │
        ▼
Retornar 201 + resumo do pedido
```

## Fluxo — consultar pedido

```
GET /api/orders/{orderId}
GET /api/orders/by-checkout-session/{checkoutSessionId}  (opcional)
```

## Reserva de estoque — decisão adotada

A reserva de estoque ocorre **apenas** na criação da `CheckoutSession` (módulo CartCheckout).

Ao criar um `Order` nesta etapa:

| Ação | Comportamento |
|------|----------------|
| Reservar estoque novamente | **Não** — evita duplicidade |
| Confirmar reserva (venda) | **Sim** — via webhook Mercado Pago `approved` |
| Cancelar reserva | **Não** no Orders — feito por CartCheckout (cancel) ou worker de expiração |
| Alterar status da CheckoutSession | **Não** — sem semântica artificial |

A confirmação definitiva da reserva (venda) ocorre quando o **webhook Pix** marca o pedido como `Paid` (`docs/payments/MP-PIX-002-webhook-confirmation.md`).

Pedidos `PendingPayment` com sessão/Pix vencidos são marcados como **`Expired`** pelo worker (`docs/expiration-worker.md`), com liberação de reserva no Inventory.

## Endpoints

| Método | Path | Status | Descrição |
|--------|------|--------|-----------|
| `POST` | `/api/orders/from-checkout-session` | 201 | Cria pedido `PendingPayment` |
| `GET` | `/api/orders/{orderId}` | 200 | Consulta pedido por ID |
| `GET` | `/api/orders/by-checkout-session/{checkoutSessionId}` | 200 | Consulta pedido por sessão |

### Request — criar pedido

```json
{
  "checkoutSessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### Response — pedido criado (201)

```json
{
  "orderId": "guid",
  "orderNumber": "guid-como-string",
  "checkoutSessionId": "guid",
  "status": "PendingPayment",
  "customer": {
    "fullName": "João Silva",
    "email": "joao@email.com",
    "phone": "11999999999"
  },
  "address": {
    "zipCode": "01001000",
    "street": "Rua Exemplo",
    "number": "123",
    "complement": "Apto 10",
    "neighborhood": "Centro",
    "city": "São Paulo",
    "state": "SP"
  },
  "items": [
    {
      "skuId": "guid",
      "productName": "Produto",
      "skuCode": "SKU-001",
      "quantity": 2,
      "unitPrice": 100,
      "subtotal": 200
    }
  ],
  "subtotal": 200,
  "shipping": null,
  "total": 200,
  "createdAt": "2026-05-23T12:00:00Z"
}
```

## Erros HTTP

| Status | Situação |
|--------|----------|
| 400 | Request inválido (`checkoutSessionId` ausente) |
| 404 | Checkout session não encontrada; pedido não encontrado |
| 409 | Sessão em status inválido; pedido já existe para a sessão |

## Banco de dados

- Schema: `orders`
- Tabelas: `orders.orders`, `orders.order_items`
- History table: `orders.__EFMigrationsHistory`
- Constraint: `checkout_session_id` **unique**
- Índices: `customer_email`, `created_at`

## Integração cross-module

| Porta (Orders.Application) | Implementação (Orders.Infrastructure) |
|----------------------------|----------------------------------------|
| `ICheckoutSessionReader` | `CheckoutSessionReader` — lê `CartCheckoutDbContext` (read-only) |

Orders **não** referencia Inventory diretamente.

## Testes

| Projeto | Cobertura |
|---------|-----------|
| `Vls.Shopflow.Orders.UnitTests` | Domain + Application handlers |
| `Vls.Shopflow.Orders.IntegrationTests` | Persistência real com PostgreSQL (`SHOPFLOW_TEST_DB`) |

## Próximos passos

1. Frontend: exibir QR/status Paid e Account/Meus pedidos
2. Guest Order Access Token
3. Notificação por e-mail ao confirmar pagamento
