# PaymentsPix

Módulo responsável por registrar intenções/cobranças Pix associadas a pedidos `PendingPayment`, com provider abstrato e implementação fake/dev.

## Escopo MVP (backend)

- Criar `PixPayment` para `Order` com status `PendingPayment`
- Provider abstrato (`IPixPaymentProvider`) com implementação `FakePixPaymentProvider`
- `PixPayment` nasce com status **`Pending`**
- Amount copiado do `Total` do pedido (sem recalcular)
- Endpoint idempotente: se já existir pagamento `Pending` para o pedido, retorna o existente (200)
- **Sem** gateway real, QR Code real, copia-e-cola real ou webhook
- **Sem** marcar `Order` como `Paid`
- **Sem** confirmar reserva de estoque no Inventory
- Expiração automática de `PixPayment` `Pending` via worker (ver `docs/expiration-worker.md`)
- **Sem** integração frontend nesta etapa

## Fora do escopo nesta etapa

- Mercado Pago, Pagar.me, Asaas, Efí ou banco real
- Webhook de confirmação de pagamento
- Endpoint `simulate-paid` (próxima etapa dev/sandbox)
- E-mail/WhatsApp
- Admin de pagamentos
- Conciliação financeira

## Fluxo — criar pagamento Pix

```
POST /api/payments/pix/orders/{orderId}
        │
        ▼
FluentValidation (orderId obrigatório)
        │
        ▼
Já existe PixPayment Pending para orderId? → 200 + pagamento existente
        │
        ▼
IOrderPaymentReader → buscar Order no schema orders (read-only)
        │
        ├─ não encontrada → 404
        ├─ status ≠ PendingPayment → 409
        ├─ total ≤ 0 → 400
        │
        ▼
IPixPaymentProvider.CreatePixChargeAsync (Fake — sem API externa)
        │
        ▼
Persistir PixPayment (status Pending) em payments_pix.pix_payments
        │
        ▼
Retornar 201 + resumo do pagamento
```

## Fluxo — consultar pagamento

```
GET /api/payments/pix/{paymentId}
GET /api/payments/pix/by-order/{orderId}
```

## Integração com Orders

| Porta (PaymentsPix.Application) | Implementação (PaymentsPix.Infrastructure) |
|--------------------------------|---------------------------------------------|
| `IOrderPaymentReader` | `OrderPaymentReader` — lê `OrdersDbContext` (read-only) |

PaymentsPix **consulta** Order para validar existência, status e total. **Não altera** o status da Order nesta etapa (exceto indiretamente via worker de expiração quando o Pix vence).

## Provider abstraction

```csharp
IPixPaymentProvider.CreatePixChargeAsync(PixChargeRequest)
```

Implementação atual: `FakePixPaymentProvider`

| Campo | Comportamento fake |
|-------|-------------------|
| `provider` | `Fake` |
| `providerPaymentId` | `fake-dev-{orderId}` |
| `qrCode` | `null` |
| `qrCodeImageUrl` | `null` |
| `copyPasteCode` | `null` |
| `status` | `Pending` |

## Estratégia idempotente (pagamento duplicado)

Se já existir `PixPayment` com status `Pending` para o mesmo `orderId`, o endpoint `POST` retorna **200 OK** com o pagamento existente — não cria duplicata.

Índice único parcial no banco: `order_id` unique onde `status = 'Pending'`.

## Endpoints

| Método | Path | Status | Descrição |
|--------|------|--------|-----------|
| `POST` | `/api/payments/pix/orders/{orderId}` | 201 / 200 | Cria ou retorna PixPayment Pending |
| `GET` | `/api/payments/pix/{paymentId}` | 200 | Consulta por ID |
| `GET` | `/api/payments/pix/by-order/{orderId}` | 200 | Consulta mais recente do pedido |

### Response — pagamento criado (201 ou 200 idempotente)

```json
{
  "paymentId": "guid",
  "orderId": "guid",
  "status": "Pending",
  "provider": "Fake",
  "amount": 100.00,
  "qrCode": null,
  "qrCodeImageUrl": null,
  "copyPasteCode": null,
  "expiresAt": "2026-05-23T13:00:00Z",
  "createdAt": "2026-05-23T12:30:00Z",
  "message": "Pagamento Pix criado em modo preparação. Gateway real ainda não integrado."
}
```

## Erros HTTP

| Status | Situação |
|--------|----------|
| 400 | `orderId` inválido; total do pedido ≤ 0 |
| 404 | Pedido não encontrado; pagamento não encontrado |
| 409 | Pedido não está `PendingPayment` |

## Banco de dados

- Schema: `payments_pix`
- Tabela: `payments_pix.pix_payments`
- History table: `payments_pix.__EFMigrationsHistory`
- Constraint: `"Amount" > 0`
- Índice único parcial: um `Pending` por `order_id`

## Testes

| Projeto | Cobertura |
|---------|-----------|
| `Vls.Shopflow.PaymentsPix.UnitTests` | Domain + Application handlers |
| `Vls.Shopflow.PaymentsPix.IntegrationTests` | Persistência real com PostgreSQL (`SHOPFLOW_TEST_DB`) |

## Próximos passos

1. Plug provider real (Mercado Pago, etc.) implementando `IPixPaymentProvider`
2. Webhook de confirmação → marcar `PixPayment` Paid + `Order` Paid + confirmar reserva Inventory
3. Endpoint dev `simulate-paid` (sandbox) com documentação explícita
