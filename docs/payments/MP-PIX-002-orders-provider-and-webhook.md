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
- Frontend (status público via Guest Order Access Token — ver `docs/security/SEC-006-guest-order-access-token.md`)
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
3. Persistir evento em `mercado_pago_webhook_events` (idempotência por `ProviderEventId`).
4. Ignorar `type` ≠ `order` (`ProcessingStatus=Ignored`).
5. Validar formato de `data.id`: deve começar com `ORD` (cobre sandbox `ORDTST…`). IDs genéricos do painel (ex.: `123456`) → `Ignored` / outcome `SimulatorEvent`, **HTTP 200**, sem GET e sem Paid.
6. `GET /v1/orders/{ProviderOrderId}` (id **original** da query, case preservado).
7. Localizar `PixPayment` por `ProviderOrderId` (fallback: transaction id / `external_reference`).
8. Conferir Pix, amount, `external_reference`.
9. Mapear status:

| Order MP | Ação Shopflow |
|----------|---------------|
| `created` / `processing` / `action_required` (+ `waiting_*`) | Mantém Pending |
| `processed` + `accredited` (+ tx se presente) | Confirma reserva → Pix Paid → Order Paid |
| `failed` / `canceled` / `expired` | Atualiza Pix; **não** Paid; **não** confirma estoque |
| `refunded` / `charged_back` | Log + ignore (dívida) |

### Lookup HTTP (após assinatura válida)

| Resposta MP | Evento | HTTP Shopflow | Notas |
|-------------|--------|---------------|--------|
| 400 / 404 | `LookupFailed` | 200 | Não retry útil; não marca Paid |
| 401 / 403 | `Failed` | **503** `MisconfiguredAccessToken` | Revisar `MercadoPago__AccessToken` |
| 5xx / outros | `Failed` + exceção | 500 (retry MP) | Transitório |

Status terminais para o mesmo `ProviderEventId`: `Processed`, `Ignored`, `LookupFailed` (não reinsere / não reprocessa).

### Simulação do painel vs checkout real

- O **teste de webhook no painel** Mercado Pago costuma enviar `data.id` genérico (ex. `123456`). Isso **não** é uma Order da Orders API → Shopflow **não** confirma pagamento.
- Para validar o fluxo end-to-end: criar Pix via checkout Shopflow (`POST /v1/orders` gera `ORD…` / `ORDTST…`), pagar no sandbox (ex. payer `APRO`), e receber webhook com esse `ProviderOrderId`.
- Consulta manual: `GET https://api.mercadopago.com/v1/orders/{ProviderOrderId}` com Bearer Access Token.
- Confirmação de Paid só com order `status=processed` e `status_detail=accredited` (e transaction correspondente, se presente).

Logs mascaram `ProviderOrderId` (prefixo/sufixo); **nunca** logar AccessToken, WebhookSecret ou `x-signature` completa.

## Assinatura (SDK oficial + oráculo manual)

Pacote NuGet: **`mercadopago-sdk` 3.3.0** (`MercadoPago.Webhook.WebhookSignatureValidator`).

| Camada | Comportamento |
|--------|----------------|
| **Primária** | `CompositeMercadoPagoWebhookSignatureValidator` → SDK `Validate(xSignature, xRequestId, queryDataId, secret, tolerance)` |
| **Diagnóstico** | Manual HMAC (docs “without SDK”): data.id **lowercase** no manifest — útil para ver `sdk_valid` vs `manual_valid` |
| **Fallback** | Só se o SDK lançar exceção inesperada (não `InvalidWebhookSignatureException`) |

**Entrada do SDK (nunca body):**

| Campo | Origem |
|-------|--------|
| `data.id` | `Request.Query["data.id"]` **como recebido** (SDK preserva case após fix no SDK; não fazer lowercase antes) |
| `x-request-id` | header |
| `x-signature` | header (`ts` em ms tipicamente; `v1`) |
| `secret` | `MercadoPago__WebhookSecret` com `.Trim()` nas pontas |

Decisão: SDK aceita → webhook aceito (mesmo se manual rejeitar por lowercase). SDK rejeita → **401** (mesmo se manual aceitar). Ambos rejeitam → 401.

Logs: `sdk_signature_valid`, `manual_signature_valid`, `signature_validator_final` (`Sdk` / `ManualFallback` / `Rejected`), `secret_length`, `secret_trimmed_changed`, fingerprint — **nunca** secret/token/x-signature/v1 completos.

**.env:** sem aspas, sem espaço/quebra no fim do secret. Fingerprint local: `printf '%s' "$SECRET" | shasum -a 256 | cut -c1-8`.

Shopflow: assinatura válida **sem** query `data.id` → `200 MissingQueryDataId`. Query ≠ body → `200 DataIdMismatch`.

Diagnóstico de `signature_mismatch`: secret da **mesma** app do AccessToken, evento **Order (Mercado Pago)**, URL com query preservada, HMAC com query `data.id` (não body).

### Validar app / AccessToken / WebhookSecret (mesmo mismatch operacional)

Quando o manifest oficial está correto mas o HMAC ainda diverge, a causa mais comum é **secret de outra aplicação/ambiente**.

No log do webhook (sempre) e no mismatch (Warning):

| Campo log | Comparar com |
|-----------|----------------|
| `body_application_id` | App ID no painel MP onde o **WebhookSecret** foi copiado; e `MercadoPago__ApplicationId` se preenchido |
| `body_user_id` | User/seller da conta; `MercadoPago__UserId` se preenchido |
| `body_live_mode` | `false` → credenciais/URL de teste; `true` → produção |
| `configured_environment` | `MercadoPago__Environment` (Sandbox/Production) |
| `webhook_secret_fingerprint` | primeiros 8 hex de SHA256(secret) — comparar entre containers/envs **sem** ver o secret |
| `application_id_matches_config` / `user_id_matches_config` | `false` ⇒ config desalinhada do body |

Startup (Testing/Staging/Development): loga `NotificationUrl`, `ApplicationId`/`UserId` configurados e `webhook_secret_fingerprint`. Em Production o fingerprint fica oculto no startup (continua no log de mismatch).

Checklist operacional:

1. AccessToken usado em `POST /v1/orders` e WebhookSecret devem ser da **mesma** app MP.
2. URL modo teste vs produção no painel usa secrets distintos — escolher o par certo.
3. Evento: **Order (Mercado Pago)** / tópico `orders`.
4. Preencher `MercadoPago__ApplicationId` e `MercadoPago__UserId` nos `.env` ajuda a ver `*_matches_config=false` imediatamente.
## Migration

`AddPixPaymentOrdersApiFields` — colunas Orders em `pix_payments`; renomeia `mercado_pago_webhook_events.ProviderPaymentId` → `ProviderOrderId`.

## Testes

`Vls.Shopflow.PaymentsPix.UnitTests` — provider Orders, order client (400/404/401/5xx sem throw crítico), assinatura, webhook Paid/Pending/Failed/mismatch/IgnoredType, painel `data.id=123456` → Ignored, ORDTST → GET, LookupFailed.

## Dívida

- Refund/chargeback completo
- Estratégia se reserva já expirou quando MP acredita
- Frontend QR + polling/status Paid
