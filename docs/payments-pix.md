# PaymentsPix

Módulo responsável por registrar intenções/cobranças Pix associadas a pedidos `PendingPayment`, com provider abstrato e implementação fake/dev.

## Escopo MVP (backend)

- Criar `PixPayment` para `Order` com status `PendingPayment`
- Provider abstrato (`IPixPaymentProvider`) com implementações:
  - **`FakePixPaymentProvider`** — dev sem API externa
  - **`MercadoPagoPixPaymentProvider`** — Checkout API Orders (`POST /v1/orders`)
- Webhook Mercado Pago (`POST /api/payments/pix/webhooks/mercado-pago`) com **`mercadopago-sdk` WebhookSignatureValidator** (query `data.id` as-is) + oráculo manual + diagnóstico `application_id`/`user_id`/fingerprint; `GET /v1/orders/{id}`; simulação → `SimulatorEvent` 200
- Em `processed`/`accredited`: PixPayment Paid + Order Paid + confirmação de reserva Inventory
- `PixPayment` nasce com status **`Pending`**
- Amount copiado do `Total` do pedido (sem recalcular)
- Endpoint idempotente: se já existir pagamento `Pending` para o pedido, retorna o existente (200)
- Com Mercado Pago: retorna **QR/copia-e-cola reais** (`qr_code`, `qr_code_base64`; `ticket_url` em campo separado)
- Expiração automática de `PixPayment` `Pending` via worker (ver `docs/expiration-worker.md`)

## Fora do escopo nesta etapa

- Endpoint `simulate-paid`
- Integração frontend (Public Key / QR UI)
- Reembolso, chargeback, cartão, boleto
- Pagar.me, Asaas, Efí ou banco real

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

Seleção via `PaymentsPix:Provider` (`Fake` | `MercadoPago`).

### FakePixPaymentProvider

| Campo | Comportamento fake |
|-------|-------------------|
| `provider` | `Fake` |
| `providerOrderId` | `fake-ord-{orderId}` |
| `providerTransactionId` | `fake-pay-{orderId}` |
| `qrCode` | `null` |
| `qrCodeImageUrl` | `null` |
| `copyPasteCode` | `null` |
| `status` | `Pending` |

### MercadoPagoPixPaymentProvider

| Campo | Comportamento |
|-------|---------------|
| `provider` | `MercadoPago` |
| `providerOrderId` | ORD… (`POST /v1/orders`) |
| `providerTransactionId` | PAY… |
| `copyPasteCode` | `qr_code` (EMV copia e cola) |
| `qrCode` | `qr_code_base64` (data URI), se houver |
| `qrCodeImageUrl` | `null` (`ticket_url` não é imagem) |
| `ticketUrl` | `ticket_url` |
| `status` | `Pending` até webhook Orders |

Ver `docs/payments/MP-PIX-002-orders-provider-and-webhook.md`.

## Estratégia idempotente (pagamento duplicado)

Se já existir `PixPayment` com status `Pending` para o mesmo `orderId`, o endpoint `POST` retorna **200 OK** com o pagamento existente — não cria duplicata.

Índice único parcial no banco: `order_id` unique onde `status = 'Pending'`.

## Endpoints

| Método | Path | Status | Descrição |
|--------|------|--------|-----------|
| `POST` | `/api/payments/pix/orders/{orderId}` | 201 / 200 | Cria ou retorna PixPayment Pending |
| `POST` | `/api/payments/pix/webhooks/mercado-pago` | 200 / 401 / 503 | Webhook Orders (assinatura + GET order; simulação painéis = 200 Ignored/LookupFailed) |
| `GET` | `/api/payments/pix/{paymentId}` | 200 | Consulta por ID (admin) |
| `GET` | `/api/payments/pix/by-order/{orderId}` | 200 | Consulta mais recente do pedido (admin) |

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

1. Integração frontend (QR/copia-e-cola + polling de status via Guest Order Access Token)
2. Notificação por e-mail ao confirmar pagamento
3. Refund/chargeback

Guest status: `docs/security/SEC-006-guest-order-access-token.md`  
Webhook + provider Orders: `docs/payments/MP-PIX-002-orders-provider-and-webhook.md`  
Captura temporária webhook bruto (Testing/HML): `docs/payments/MP-PIX-003-webhook-raw-capture-temporary.md`  
Notas históricas do provider: `docs/payments-mercado-pago-pix.md`
