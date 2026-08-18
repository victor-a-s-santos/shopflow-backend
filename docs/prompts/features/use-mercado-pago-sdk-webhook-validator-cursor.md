Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Mercado Pago SDK, Webhooks, HMAC-SHA256, segurança e arquitetura limpa.

Problema:
O Shopflow cria Pix real via Mercado Pago Orders API com sucesso, mas o webhook real continua falhando com Signature mismatch.

Estado atual observado:

* Provider MercadoPago ativo.
* Ambiente Sandbox.
* AccessToken configurado.
* WebhookSecret configurado.
* NotificationUrl configurada.
* Webhook real chega no backend.
* body_application_id bate com configured_application_id.
* body_user_id bate com configured_user_id.
* body_live_mode=False e configured_environment=Sandbox.
* query data.id existe.
* body data.id existe.
* x-signature existe.
* x-request-id existe.
* ts/v1 existem.
* timestamp dentro da tolerância.
* Manifest atual inclui id/request-id/ts.
* data.id alfanumérico é convertido para lowercase.
* Mesmo assim, computed_official_prefix != received_v1_prefix.

Exemplo real:
ProviderOrderId:
ORDTST01KXHE3K4RBCANN4BPFDFHJV85

Logs:

* action=order.action_required / waiting_transfer
* depois action=order.processed / accredited
* signature_valid=False
* failure_reason=signature_mismatch

Documentação oficial anexada pelo usuário:
A doc mostra validação com SDK oficial em C#:

using MercadoPago.Error;
using MercadoPago.Webhook;

WebhookSignatureValidator.Validate(
xSignature: Request.Headers["x-signature"],
xRequestId: Request.Headers["x-request-id"],
dataId: Request.Query["data.id"],
secret: secret);

Também mostra a validação manual:
manifest:
id:[data.id_url];request-id:[x-request-id_header];ts:[ts_header];

Regras:

* data.id vem da query string.
* Se data.id for alfanumérico em maiúsculas, converter para minúsculas no manifest manual.
* ts e v1 vêm de x-signature.
* x-request-id vem do header.
* Secret vem da tela Webhooks da aplicação.
* Partes ausentes devem ser omitidas do manifest manual.

Objetivo:
Usar o SDK oficial do Mercado Pago como validador primário ou, no mínimo, como oráculo diagnóstico para comparar contra o validador manual.

Não mexer no frontend.
Não mexer no fluxo de criação Pix.
Não expor AccessToken, WebhookSecret, x-signature completa, v1 completo ou payload completo em logs.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Ler:

* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments-pix.md
* MercadoPagoWebhookSignatureValidator atual
* ProcessMercadoPagoPixWebhookCommandHandler
* PaymentsPixEndpoints
* MercadoPago options/config
* testes atuais de webhook
* arquivos .csproj relacionados ao módulo PaymentsPix/HttpApi
* documentação anexada pelo usuário sobre Webhooks Mercado Pago

==================================================
2. VALIDAR DISPONIBILIDADE DO SDK
=================================

Verificar se o SDK Mercado Pago .NET já existe no projeto.

Se não existir, avaliar adicionar o pacote oficial NuGet adequado, provavelmente:
MercadoPago

Antes de adicionar:

* confirmar namespace disponível:
  MercadoPago.Webhook.WebhookSignatureValidator
  MercadoPago.Error.InvalidWebhookSignatureException
* confirmar compatibilidade com .NET atual do projeto.
* adicionar no projeto correto, preferencialmente Infrastructure ou HttpApi, sem contaminar Application com dependência externa se isso ferir a arquitetura.

Se o SDK atual não contiver o validador de webhook:

* não forçar gambiarra.
* documentar versão testada.
* manter manual como fallback.
* criar teste de compatibilidade, se possível.

==================================================
3. CRIAR ABSTRAÇÃO
==================

Criar uma abstração interna, por exemplo:

IMercadoPagoWebhookSignatureValidator

Método sugerido:

MercadoPagoWebhookSignatureValidationResult Validate(
string? xSignature,
string? xRequestId,
string? queryDataId,
string? secret,
CancellationToken cancellationToken = default
)

Ou manter assinatura síncrona se for simples.

Implementações possíveis:

1. MercadoPagoSdkWebhookSignatureValidator

   * usa WebhookSignatureValidator.Validate do SDK oficial.

2. ManualMercadoPagoWebhookSignatureValidator

   * implementação atual manual, preservada para diagnóstico/fallback.

3. CompositeMercadoPagoWebhookSignatureValidator

   * roda SDK e manual em modo diagnóstico.
   * decisão final preferencial: SDK.
   * loga divergência segura:
     sdk_valid=true/false
     manual_valid=true/false
     sdk_error_type
     manual_failure_reason
   * nunca loga secret, assinatura completa ou payload.

Decisão desejada:

* Em ambiente real, usar SDK como fonte primária da validação.
* Manter manual apenas como fallback/teste/documentação, se fizer sentido.
* Se SDK rejeitar e manual aceitar, rejeitar e logar divergência.
* Se SDK aceitar e manual rejeitar, aceitar e logar que o manual divergiu, para posterior correção.
* Se ambos rejeitarem, rejeitar 401.
* Se SDK indisponível, usar manual e documentar.

==================================================
4. USO EXATO DO SDK
===================

O uso deve seguir a doc oficial:

WebhookSignatureValidator.Validate(
xSignature: Request.Headers["x-signature"],
xRequestId: Request.Headers["x-request-id"],
dataId: Request.Query["data.id"],
secret: secret
);

Importante:

* Passar para o SDK o dataId da query como recebido.
* Não passar body.data.id.
* Não passar ProviderOrderId salvo no banco.
* Não passar Request.Query["id"].
* Não montar manifest antes de chamar o SDK.
* Não fazer lowercase manual antes do SDK, a menos que a documentação/SDK exija explicitamente.
* O SDK deve receber o valor conforme `Request.Query["data.id"]`.
* Secret deve vir de MercadoPago__WebhookSecret, com trim seguro se o valor tiver espaços acidentais nas pontas.

==================================================
5. TRIM SEGURO DO SECRET
========================

Investigar se o secret no env pode estar com:

* aspas,
* espaço no final,
* quebra de linha,
* carriage return,
* caractere invisível.

Implementar proteção conservadora:

* trim apenas nas pontas: secret.Trim()
* não alterar conteúdo interno.
* logar apenas:
  secret_configured=true
  secret_length
  secret_trimmed_changed=true/false
  webhook_secret_fingerprint=sha256(secret.Trim()).Substring(0,8)
  Nunca logar o secret.

Atualizar docs:

* env não deve usar aspas.
* env não deve ter espaço final.
* após redefinir secret no painel, recriar container.

==================================================
6. LOGS DE DIAGNÓSTICO SEGUROS
==============================

No webhook, logar:

* sdk_signature_valid=true/false
* manual_signature_valid=true/false
* signature_validator_final=Sdk/ManualFallback/Rejected
* sdk_exception_type, sem stack trace completo para mismatch comum
* manual_failure_reason
* has_x_signature
* has_x_request_id
* has_query_data_id
* has_body_data_id
* query_data_id_masked
* body_data_id_masked
* body_application_id
* configured_application_id
* application_id_matches_config
* body_user_id
* configured_user_id
* user_id_matches_config
* body_live_mode
* configured_environment
* type
* action
* data_status
* data_status_detail
* webhook_secret_fingerprint
* secret_length
* secret_trimmed_changed

Nunca logar:

* AccessToken
* WebhookSecret
* x-signature completa
* v1 completo
* payload completo
* copyPasteCode
* guestAccessToken

==================================================
7. PROCESSAMENTO APÓS VALIDAÇÃO
===============================

Se assinatura final válida:

1. Usar query data.id original como ProviderOrderId.
2. Se data.id ausente:

   * registrar MissingQueryDataId/Ignored.
   * não marcar pago.
3. Se data.id não parecer Order real do Shopflow:

   * SimulatorEvent/Ignored.
   * não marcar pago.
4. Buscar PixPayment por ProviderOrderId.
5. Se não existir:

   * Ignored/NotFound.
   * não marcar pago.
6. Consultar:
   GET /v1/orders/{ProviderOrderId}
7. Usar a resposta do Mercado Pago como fonte de verdade.
8. processed/accredited:

   * confirmar reserva;
   * PixPayment Paid;
   * Order Paid.
9. action_required/waiting_transfer:

   * manter Pending.
10. webhook duplicado:

* idempotente.

==================================================
8. TESTES OBRIGATÓRIOS
======================

Criar ou ajustar testes para:

1. SDK validator é chamado com:

   * xSignature do header
   * xRequestId do header
   * dataId de Request.Query["data.id"]
   * secret configurado
2. body.data.id nunca é usado no SDK.
3. ProviderOrderId do banco nunca é usado no SDK.
4. Quando SDK aceita e manual rejeita:

   * webhook é aceito;
   * loga divergência segura.
5. Quando SDK rejeita e manual aceita:

   * webhook é rejeitado, salvo decisão explícita diferente documentada.
6. Quando ambos rejeitam:

   * retorna 401.
7. Quando ambos aceitam:

   * processa normalmente.
8. Secret com espaço nas pontas:

   * trim seguro aplicado;
   * fingerprint usa secret trimado;
   * loga secret_trimmed_changed=true.
9. Secret sem espaço:

   * fingerprint estável.
10. Simulador 123456 válido:

* Ignored/SimulatorEvent;
* não marca pago.

11. Webhook real ORDTST válido:

* chama GET /v1/orders/{ORDTST...}.

12. processed/accredited:

* PixPayment Paid;
* Order Paid;
* reserva confirmada.

13. action_required/waiting_transfer:

* mantém Pending.

14. Endpoint webhook não exige CSRF.
15. Endpoint webhook não exige cookie.
16. Logs não expõem secret/token/x-signature/v1 completos.

Se for difícil mockar o SDK estático:

* encapsular o SDK em adapter testável.
* testar adapter com amostras controladas.
* testar handler com fake IMercadoPagoWebhookSignatureValidator.

Não chamar Mercado Pago real nos testes.

==================================================
9. TESTE DIAGNÓSTICO COM PAYLOAD REAL
=====================================

Criar, se possível, um teste ou utilitário interno de diagnóstico para reproduzir um webhook salvo.

Entrada:

* x-signature
* x-request-id
* query data.id
* body opcional
* secret via env

Saída segura:

* sdk_valid true/false
* manual_valid true/false
* fingerprints/prefixes mascarados

Não versionar dados reais sensíveis.
Não colocar secret real em fixture.

==================================================
10. DOCUMENTAÇÃO
================

Atualizar:

* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments-pix.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/technical-debt.md
* deploy/.env.test.example
* deploy/.env.hml.example

Documentar:

* A validação agora usa SDK oficial do Mercado Pago.
* O SDK recebe x-signature, x-request-id, query data.id e secret.
* body.data.id não é usado na assinatura.
* Secret deve ser o da tela Webhooks da mesma aplicação.
* Não usar aspas/espaço/quebra de linha no .env.
* URLs configuradas na criação do pagamento podem ter prioridade sobre as URLs do painel, mas a assinatura continua ligada à aplicação/secret.
* Como comparar fingerprint do secret:
  printf '%s' "$SECRET" | sha256sum | cut -c1-8
* Como validar Pix real:
  action_required/waiting_transfer mantém Pending.
  order.processed/accredited deve marcar Paid.
* Como interpretar sdk_valid/manual_valid.

==================================================
11. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Se SDK oficial estava disponível e qual pacote/versão foi usado.
2. Arquivos alterados.
3. Como a validação final ficou.
4. Se manual validator foi removido, mantido ou usado como diagnóstico.
5. Como o secret é tratado/trimado.
6. Logs seguros adicionados.
7. Testes criados/alterados.
8. Resultado dotnet build.
9. Resultado dotnet test.
10. Próximo teste operacional no ambiente de teste.

Critérios de aceite:

* SDK oficial valida assinatura quando correta.
* SDK recebe query data.id, não body.
* Webhook real ORDTST com assinatura válida não dá Signature mismatch.
* Se SDK rejeitar, 401 continua correto.
* Nenhum secret/token é logado.
* Simulador não marca pedido como pago.
* processed/accredited real marca PixPayment Paid e Order Paid.
* dotnet build passa.
* dotnet test passa.
