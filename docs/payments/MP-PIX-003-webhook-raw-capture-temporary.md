# MP-PIX-003 — Captura temporária de webhook bruto (Testing/HML)

> **TEMPORARY DIAGNOSTIC ONLY.** Remover após diagnosticar o mismatch do SDK.  
> **Nunca habilitar em Production.**

## Por que existe

O Mercado Pago **não envia** `WebhookSecret` no webhook. Ele envia `x-signature` (HMAC).  
A secret fica no painel / `MercadoPago__WebhookSecret` e o backend usa para validar.

Quando o SDK (`mercadopago-sdk` `WebhookSignatureValidator`) rejeita com app/env corretos, esta captura grava os campos exatos necessários para reproduzir `Validate` localmente.

## Como habilitar (Testing / Staging / HML)

```env
MercadoPago__WebhookRawCaptureEnabled=true
# Opcional: só uma order
MercadoPago__WebhookRawCaptureOrderId=ORDTST01KXHE3K4RBCANN4BPFDFHJV85
# Se OrderId vazio: máx. N eventos por processo (default 5)
MercadoPago__WebhookRawCaptureMaxEvents=5
```

Guards:

1. `ASPNETCORE_ENVIRONMENT != Production` (hardcoded)
2. `WebhookRawCaptureEnabled=true`

Em Production, mesmo com (1)=true no env, **nada é capturado**.

## O que aparece no log

Procure:

```text
MP_WEBHOOK_RAW_CAPTURE
```

Campos úteis: `raw_query_string`, `query_data_id_exact`, `header_x_request_id_exact`, `header_x_signature_exact`, `parsed_ts`/`parsed_v1`, `body_raw_json` (máx. 8 KB), `sdk_signature_valid`, `manual_signature_valid`, `webhook_secret_fingerprint`, `webhook_secret_length`.

**Não** contém: AccessToken, WebhookSecret, cookies, Authorization.

## Riscos

- `x-signature` completo em log (não é a secret, mas não compartilhar publicamente).
- Body JSON truncado — ainda assim só em Testing/HML.

## Relação com `notification_url`

Antes de capturar raw em loops longos, teste o canal do painel:

```env
MercadoPago__SendNotificationUrlInOrderCreate=false
```

Documentação MP: URL enviada na criação da order tem prioridade sobre a URL do painel Webhooks. Ver `MP-PIX-002`.

## Como desabilitar / remover

1. `MercadoPago__WebhookRawCaptureEnabled=false` (ou remover a var) e recriar o container.
2. Após o diagnóstico, remover código: `MercadoPagoWebhookRawCapture*`, opções `WebhookRawCapture*`, campos opcionais no command de diagnóstico, este doc.

## Probe local (sem endpoint)

`IMercadoPagoWebhookSignatureProbe.Probe(xSignature, xRequestId, dataId, secret)` — uso interno/testes apenas.
