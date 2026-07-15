Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, segurança, Mercado Pago Webhooks, observabilidade temporária e diagnóstico de integrações.

Problema:
O webhook real do Mercado Pago chega na API, application_id/user_id batem, query data.id existe, x-request-id existe, x-signature existe, mas o SDK oficial ainda retorna InvalidWebhookSignatureException.

Importante:
Não existe “secret enviada” pelo Mercado Pago no webhook. O Mercado Pago envia x-signature. A secret fica no painel/env e é usada pelo backend para validar x-signature.

Objetivo:
Criar uma captura temporária e controlada do webhook bruto recebido, apenas no ambiente Testing/HML, para diagnosticar por que o SDK rejeita a assinatura.

Não mexer no frontend.
Não alterar regra final de pagamento.
Não deixar isso ativo em Production.
Não logar AccessToken, WebhookSecret, guestAccessToken, copyPasteCode ou dados sensíveis de cliente.

==================================================
1. ESCOPO
==================================================

Implementar um modo temporário de diagnóstico habilitado por env:

MercadoPago__WebhookRawCaptureEnabled=true
MercadoPago__WebhookRawCaptureOrderId=ORDTST...   # opcional, para capturar só uma order específica
MercadoPago__WebhookRawCaptureMaxEvents=5

Quando desabilitado, comportamento atual permanece igual.

O recurso deve funcionar somente se:
- ASPNETCORE_ENVIRONMENT != Production
- MercadoPago__WebhookRawCaptureEnabled=true

Em Production, mesmo com env true, deve ignorar.

==================================================
2. O QUE CAPTURAR
==================================================

Capturar em log estruturado ou tabela temporária os seguintes campos:

- received_at
- request_method
- request_path
- raw_query_string
- query_data_id_exact
- query_type_exact
- header_x_request_id_exact
- header_x_signature_exact
- parsed_ts
- parsed_v1
- body_raw_json
- body_application_id
- body_user_id
- body_live_mode
- body_type
- body_action
- body_data_id
- body_data_status
- body_data_status_detail
- configured_application_id
- configured_user_id
- application_id_matches_config
- user_id_matches_config
- configured_environment
- webhook_secret_fingerprint
- webhook_secret_length
- secret_trimmed_changed
- sdk_signature_valid
- sdk_exception_type
- manual_signature_valid
- manual_failure_reason

Atenção:
- Não capturar WebhookSecret real.
- Não capturar AccessToken.
- Não capturar Authorization headers.
- Não capturar cookies.
- Não capturar guestAccessToken.
- Não capturar Pix copyPasteCode.
- Não capturar headers completos, apenas os headers listados.
- Se logar body_raw_json, limitar tamanho máximo, por exemplo 8 KB.
- Como é webhook Mercado Pago, o body não deve ter dados de cliente Shopflow, mas ainda assim limitar e usar somente Testing/HML.

==================================================
3. CUIDADO COM X-SIGNATURE
==================================================

header_x_signature_exact pode ser capturado temporariamente somente porque:
- não é o WebhookSecret;
- é necessário para reproduzir validação local;
- será usado apenas em Testing/HML;
- o recurso será removido depois.

Mas:
- colocar comentário forte no código: TEMPORARY DIAGNOSTIC ONLY.
- documentar que deve ser removido após diagnóstico.
- não logar em Production.
- não persistir por tempo longo.
- não enviar para observabilidade externa se houver.

==================================================
4. FORMATO DO LOG
==================================================

Preferir um único log estruturado por webhook capturado:

"MP_WEBHOOK_RAW_CAPTURE {@Capture}"

Ou, se o projeto preferir, criar tabela:

payments_pix.mercado_pago_webhook_raw_captures

Campos:
- Id
- ReceivedAt
- ProviderOrderId
- RawQueryString
- XRequestId
- XSignature
- BodyJson
- SdkSignatureValid
- ManualSignatureValid
- SdkExceptionType
- SecretFingerprint
- Environment

Se usar tabela:
- documentar migration.
- limitar inserções por MaxEvents.
- permitir apagar depois.

Mais simples: log estruturado.

==================================================
5. FILTRO POR ORDER
==================================================

Se MercadoPago__WebhookRawCaptureOrderId estiver preenchido:
- capturar apenas se query data.id == esse valor.
- comparação case-insensitive.

Se vazio:
- capturar no máximo MercadoPago__WebhookRawCaptureMaxEvents.

==================================================
6. DIAGNÓSTICO LOCAL OPCIONAL
==================================================

Criar um pequeno serviço/helper interno testável que recebe:

- xSignature
- xRequestId
- dataId
- secret

E retorna:
- sdk_valid
- manual_valid
- sdk_exception_type
- manual_failure_reason
- secret_fingerprint

Não expor endpoint público para isso.

==================================================
7. TESTES
==================================================

Criar/ajustar testes:

1. Raw capture não roda em Production mesmo com env true.
2. Raw capture roda em Testing com env true.
3. Raw capture não loga AccessToken.
4. Raw capture não loga WebhookSecret.
5. Raw capture captura x-signature somente em Testing/HML.
6. Filtro por orderId funciona.
7. MaxEvents limita capturas.
8. Com env false, não captura.
9. O fluxo normal de validação continua retornando 401 quando SDK rejeita.
10. Webhook válido continua processando normalmente.

==================================================
8. DOCUMENTAÇÃO
==================================================

Atualizar ou criar:

docs/payments/MP-PIX-003-webhook-raw-capture-temporary.md

Documentar:

- Este recurso é temporário.
- Mercado Pago não envia a secret.
- O que é x-signature.
- Como habilitar.
- Como filtrar por ProviderOrderId.
- Como desabilitar.
- Como remover depois.
- Riscos.
- Não usar em Production.
- Não compartilhar logs com x-signature publicamente.

==================================================
9. RESULTADO ESPERADO
==================================================

Ao final, retorne:

1. Arquivos alterados.
2. Como habilitar a captura temporária.
3. Exemplo de env.
4. Como filtrar por uma order específica.
5. Onde encontrar o log/captura.
6. Garantias de que não roda em Production.
7. Testes criados.
8. Resultado dotnet build/test.
9. Instrução de remoção após diagnóstico.

Critérios de aceite:

- Não captura WebhookSecret.
- Não captura AccessToken.
- Captura x-signature exato apenas em Testing/HML e com flag ligada.
- Permite reproduzir validação com SDK.
- Não muda regra de pagamento.
- Não roda em Production.
- dotnet build passa.
- dotnet test passa.