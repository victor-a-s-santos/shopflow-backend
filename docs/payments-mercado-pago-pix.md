# Mercado Pago Pix — provider backend

Implementação do `MercadoPagoPixPaymentProvider` via Checkout Transparente **Orders API** (`POST /v1/orders`).

> Fluxo completo (criação + webhook): [`docs/payments/MP-PIX-002-orders-provider-and-webhook.md`](./payments/MP-PIX-002-orders-provider-and-webhook.md)

## Escopo

- Criar cobrança Pix real no Mercado Pago (`POST /v1/orders`)
- Retornar **copia e cola** (`qr_code`), **QR base64** e `ticketUrl` opcional
- Persistir `ProviderOrderId` (ORD…) e `ProviderTransactionId` (PAY…)
- `PixPayment` / `Order` permanecem Pending até webhook Orders

## Fora do escopo neste doc

- Frontend
- Cartão / boleto / Checkout Pro
- `/v1/payments` como fluxo principal

## Configuração

Seção `PaymentsPix`:

| Chave | Valores | Default |
|-------|---------|---------|
| `Provider` | `Fake`, `MercadoPago` | `Fake` |

Seção `MercadoPago`:

| Chave | Descrição |
|-------|-----------|
| `AccessToken` | Bearer token (obrigatório para MercadoPago) |
| `PublicKey` | Reservado para etapa frontend |
| `BaseUrl` | Default `https://api.mercadopago.com` |
| `WebhookSecret` | Secret do webhook (painel MP) |
| `NotificationUrl` | URL do webhook Shopflow (configurar no painel; não enviada no POST order) |
| `PixExpirationMinutes` | Default 30 |
| `SandboxPayerFirstNameOverride` | Ex.: `APRO` (somente Sandbox) |

```bash
PaymentsPix__Provider=MercadoPago
MercadoPago__AccessToken=APP_USR-...
MercadoPago__PublicKey=
MercadoPago__WebhookSecret=
MercadoPago__SandboxPayerFirstNameOverride=APRO
```

**Nunca commitar tokens reais.**

Access Token de **Credenciais de teste** também usa prefixo `APP_USR-`.

## Fluxo de criação

```
POST /api/payments/pix/orders/{orderId}
        │
        ▼
MercadoPagoPixPaymentProvider
        │
        ▼
POST https://api.mercadopago.com/v1/orders
  type: online
  payment_method: pix / bank_transfer
  X-Idempotency-Key: {orderId}
        │
        ▼
Persiste PixPayment (Pending):
  ProviderOrderId, ProviderTransactionId,
  CopyPasteCode, QrCode (base64), TicketUrl
```

## Mapeamento

| Mercado Pago | Shopflow |
|--------------|----------|
| order `id` | `ProviderOrderId` |
| `transactions.payments[0].id` | `ProviderTransactionId` |
| `qr_code` | `CopyPasteCode` |
| `qr_code_base64` | `QrCode` (data URI) |
| `ticket_url` | `TicketUrl` (não usar como `<img src>`) |

## Erros

Falha na API Mercado Pago → `502 Bad Gateway` (`MercadoPagoPixChargeFailedException`).
