Você está atuando como engenheiro backend sênior do projeto Shopflow.

Problema:
O webhook real Mercado Pago Orders API agora chega com:
- x-signature presente
- x-request-id presente
- query data.id presente
- body data.id presente
- data.id lowercased no manifest
- ts/v1 presentes
- timestamp dentro da tolerância
- WebhookSecret configurado
- manifest oficial id/request-id/ts

Mesmo assim, o backend calcula HMAC diferente do v1 recebido:
failure_reason=signature_mismatch.

Isso indica forte possibilidade de WebhookSecret de aplicação/ambiente diferente do AccessToken usado para criar a order.

Objetivo:
Adicionar diagnóstico seguro para confirmar app/ambiente/secret mismatch sem expor tokens/secrets.

Não mexer no frontend.
Não logar AccessToken, WebhookSecret, x-signature completa, v1 completo ou payload completo.

Implementar:

1. No recebimento do webhook, extrair do body:
   - application_id
   - user_id
   - live_mode
   - type
   - action
   - data.id
   - data.status
   - data.status_detail

2. Logar de forma segura:
   - body_application_id
   - body_user_id
   - body_live_mode
   - type
   - action
   - query_data_id_masked
   - body_data_id_masked
   - signature_valid true/false
   - failure_reason, se houver

3. No startup, além dos logs atuais, adicionar:
   - MercadoPago notification URL
   - MercadoPago environment
   - AccessToken configured true/false
   - WebhookSecret configured true/false
   - opcional: fingerprint seguro do WebhookSecret:
     sha256(secret).Substring(0, 8)
   Nunca logar o secret real.

4. Adicionar endpoint interno apenas em Development/Testing, ou log de startup, para imprimir fingerprint seguro:
   - webhook_secret_fingerprint = primeiros 8 chars do SHA256(secret)
   Não imprimir em Production, se preferirem.

5. Documentar como validar operacionalmente:
   - O body.application_id do webhook deve corresponder à aplicação Mercado Pago onde o WebhookSecret foi copiado.
   - O AccessToken usado para POST /v1/orders deve ser da mesma aplicação.
   - Se AccessToken é de uma app e WebhookSecret é de outra, HMAC nunca vai bater.
   - Se o painel tiver URL modo teste e URL produção, usar o secret correto para o modo/credencial correto.
   - Evento correto: Order (Mercado Pago), tópico orders.

6. Testes:
   - logs não expõem secret/token.
   - signature mismatch com secret errado continua 401.
   - body application_id/user_id são extraídos sem quebrar quando ausentes.
   - evento válido continua processando normalmente.

Resultado esperado:
- Com o próximo webhook real, os logs devem permitir comparar:
  body_application_id/user_id/live_mode
  com a aplicação/conta configurada no painel Mercado Pago.
- Se app/secret estiverem desalinhados, ficará evidente operacionalmente.