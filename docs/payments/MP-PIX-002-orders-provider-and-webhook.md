# MP-PIX-002 — Mercado Pago Pix via Orders API (provider + webhook)

Checkout Transparente Pix no Shopflow usa **somente** a Orders API.

| Operação | Endpoint MP |
|----------|-------------|
| Criar Pix | `POST /v1/orders` |
| Confirmar no webhook | `GET /v1/orders/{id}` |

Evento no painel Mercado Pago: **Order** (não `payment`).

## Escopo

- Criar cobrança Pix (`action_required` / `waiting_transfer`)
- Persistir `ProviderOrderId` (ORD…) e `ProviderTransactionId` (PAY…)
- Retornar copia-e-cola + QR base64 (quando houver) + `ticketUrl` opcional
- Webhook público com `x-signature` + consulta confirmatória
- Marcar Paid **somente** com order `processed` + `accredited`
- Confirmar reserva Inventory de forma idempotente

## Fora de escopo

- `/v1/payments` como fluxo principal
- Frontend
- `simulate-paid`
- Cartão, boleto, Checkout Pro, refund/chargeback completo

## Configuração

```bash
PaymentsPix__Provider=MercadoPago
MercadoPago__Enabled=true
MercadoPago__Environment=Sandbox
MercadoPago__BaseUrl=https://api.mercadopago.com
MercadoPago__AccessToken=APP_USR-...
MercadoPago__PublicKey=
MercadoPago__WebhookSecret=
MercadoPago__NotificationUrl=https://api-hml.seudominio.com.br/api/payments/pix/webhooks/mercado-pago
MercadoPago__PixExpirationMinutes=30
MercadoPago__WebhookSignatureToleranceMinutes=10
# Sandbox only — order Pix de teste com auto-aprovação
MercadoPago__SandboxPayerFirstNameOverride=APRO
```

Alias legado: `MercadoPago__TestPayerFirstName` → `SandboxPayerFirstNameOverride`.

`notification_url` **não** é enviado no payload de `POST /v1/orders` nesta etapa; configure o webhook no painel MP apontando para o endpoint Shopflow.

## Criação (`POST /api/payments/pix/orders/{orderId}`)

1. Idempotência: se já existe `PixPayment` Pending, retorna o existente.
2. `MercadoPagoPixPaymentProvider` → `POST /v1/orders` com `X-Idempotency-Key = orderId`.
3. Persistência:

| MP | Shopflow |
|----|----------|
| order `id` (ORD…) | `ProviderOrderId` |
| payment `id` (PAY…) | `ProviderTransactionId` (+ `ProviderPaymentId` alinhado) |
| order `status` / `status_detail` | `ProviderStatus` / `ProviderStatusDetail` |
| payment `status` / `status_detail` | `ProviderTransactionStatus*` |
| `qr_code` | `CopyPasteCode` |
| `qr_code_base64` | `QrCode` (data URI) |
| `ticket_url` | `TicketUrl` (**não** `QrCodeImageUrl`) |

## Webhook (`POST /api/payments/pix/webhooks/mercado-pago`)

Público, sem cookie/CSRF. Segurança = assinatura + GET order.

1. Validar `x-signature` / `x-request-id` / `data.id` (query).
2. Manifesto: `id:<data.id>;request-id:<x-request-id>;ts:<ts>;` — `data.id` alfanumérico em **lowercase**.
3. Ignorar `type` ≠ `order`.
4. `GET /v1/orders/{ProviderOrderId}`.
5. Localizar `PixPayment` por `ProviderOrderId` (fallback: transaction id / `external_reference`).
6. Conferir Pix, amount, `external_reference`.
7. Mapear status:

| Order MP | Ação Shopflow |
|----------|---------------|
| `created` / `processing` / `action_required` (+ `waiting_*`) | Mantém Pending |
| `processed` + `accredited` (+ tx se presente) | Confirma reserva → Pix Paid → Order Paid |
| `failed` / `canceled` / `expired` | Atualiza Pix; **não** Paid; **não** confirma estoque |
| `refunded` / `charged_back` | Log + ignore (dívida) |

## Assinatura

`MercadoPagoWebhookSignatureValidator` — HMAC-SHA256 hex lowercase, tolerância `WebhookSignatureToleranceMinutes`.

## Migration

`AddPixPaymentOrdersApiFields` — colunas Orders em `pix_payments`; renomeia `mercado_pago_webhook_events.ProviderPaymentId` → `ProviderOrderId`.

## Testes

`Vls.Shopflow.PaymentsPix.UnitTests` — provider Orders, order client, assinatura (lowercase ORD), webhook Paid/Pending/Failed/mismatch/IgnoredType.

## Dívida

- Refund/chargeback completo
- Estratégia se reserva já expirou quando MP acredita
- Frontend QR + polling/status Paid
