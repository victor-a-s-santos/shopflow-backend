Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Mercado Pago Orders API, Webhooks, HMAC-SHA256, segurança e logs seguros.

Problema:
O Shopflow já cria Pix real via Mercado Pago Orders API.

Exemplo real dos logs:

* Shopflow order: cf6e4075-eb47-41f5-986d-387f9d73110a
* ProviderOrderId: ORDTST01KXGT3VPJ322GGMGAN6P0G9S2
* ProviderTransactionId: PAY01KXGT3VQ646SXSEVKAZ2B4W1C
* PixPayment criado como Pending
* Webhook real chega no backend
* Backend rejeita com:
  Mercado Pago webhook signature invalid. Reason=Signature mismatch.

Também foi testado o simulador do painel Mercado Pago. O simulador chegou na API e, em um caso, avançou até GET /v1/orders/123456, que falhou com 400 porque 123456 não é uma Order real do fluxo Orders API.

Documentação oficial anexada:
A validação correta do webhook Mercado Pago Orders API deve usar:

* header x-signature
* header x-request-id
* query param data.id
* secret configurado no painel da aplicação

Manifest oficial sem SDK:

id:[data.id_url];request-id:[x-request-id_header];ts:[ts_header];

Regras da documentação:

* data.id vem dos query params da URL, não do body.
* Se data.id for alfanumérico em maiúsculas, converter para minúsculas antes do manifest.
* Exemplo:
  ORD01JQ4S4KY8HWQ6NA5PXB65B3D3
  deve virar:
  ord01jq4s4ky8hwq6na5pxb65b3d3
* x-request-id vem do header.
* ts e v1 vêm de x-signature.
* Se data.id ou x-request-id não estiverem presentes, remover esse item do manifest antes de calcular HMAC.
* Calcular HMAC SHA256 em hexadecimal usando WebhookSecret.
* Comparar exatamente com v1.
* Após receber e validar, responder 200 ou 201.
* Depois consultar GET /v1/orders/{id} para obter a informação real do recurso.

Objetivo:
Corrigir a validação de assinatura seguindo estritamente a documentação oficial e melhorar o tratamento de eventos simulados/inválidos sem marcar pagamento como pago indevidamente.

Não mexer no frontend.
Não mexer na criação do Pix.
Não expor AccessToken, WebhookSecret, x-signature completa, v1 completo, token do pedido ou payload sensível em logs.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Ler:

* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments-pix.md
* endpoint POST /api/payments/pix/webhooks/mercado-pago
* MercadoPagoWebhookSignatureValidator
* ProcessMercadoPagoPixWebhookCommandHandler
* MercadoPagoOrderClient
* MercadoPagoPixPaymentProvider
* PaymentsPix DbContext e entidades
* testes atuais de webhook
* deploy/.env.test.example
* deploy/.env.hml.example

Também considerar a documentação oficial anexada pelo usuário sobre:

* Configurar Webhooks
* Simular recepção da notificação
* Validar origem da notificação
* Manifest HMAC sem SDK

==================================================
2. CORRIGIR VALIDATOR CONFORME DOC OFICIAL
==========================================

Implementar/validar exatamente este algoritmo:

Entrada:

* xSignature = Request.Headers["x-signature"]
* xRequestId = Request.Headers["x-request-id"]
* dataId = Request.Query["data.id"]

Parse de x-signature:

* split por ","
* extrair:

  * ts
  * v1

Normalização:

* dataId deve vir da query string.
* dataId deve ser convertido com ToLowerInvariant().
* Não usar body.data.id para o HMAC.
* Não usar ProviderOrderId salvo no banco para o HMAC.
* Não usar Request.Query["id"].
* Não usar id do body raiz.
* Não incluir type=order no HMAC.
* Não incluir query params extras, como client, no HMAC.

Manifest:

* Criar lista de partes.
* Se dataId query estiver presente e não vazio:
  adicionar "id:{dataIdLowercase}"
* Se xRequestId estiver presente e não vazio:
  adicionar "request-id:{xRequestId}"
* Sempre adicionar "ts:{ts}"
* Manifest final:
  string.Join(";", parts) + ";"

Exemplo:
id:ordtst01kxgt3vpj322ggmgan6p0g9s2;request-id:2066ca19-c6f1-498a-be75-1923005edd06;ts:1742505638683;

HMAC:

* HMACSHA256 com secret MercadoPago__WebhookSecret
* Encoding.UTF8 no secret e no manifest
* hex lowercase
* comparar com v1 usando CryptographicOperations.FixedTimeEquals

Regras de erro:

* x-signature ausente → 401
* v1 ausente → 401
* ts ausente → 401
* secret ausente → erro claro de configuração
* assinatura inválida → 401
* timestamp fora da tolerância → 401, se a tolerância já estiver ativa no projeto

Importante:
A documentação diz que, se data.id ou x-request-id não estiverem presentes, o campo deve ser removido do manifest. Porém, para o fluxo Shopflow, sem data.id na query não deve haver processamento de pagamento como Paid. Nesse caso, pode até validar a assinatura conforme o manifest sem id, mas deve registrar como Ignored/MissingDataId e não marcar Order/Pix como Paid.

==================================================
3. NÃO USAR BODY PARA VALIDAR ASSINATURA
========================================

O body pode conter:

{
"id": "123456",
"type": "order",
"data": {
"id": "ORD..."
}
}

Mas, segundo a doc, a assinatura deve usar o query param:

Request.Query["data.id"]

Portanto:

* body.data.id não deve substituir query data.id no HMAC.
* body.data.id pode ser usado apenas para diagnóstico/coerência.
* Se query data.id existe e body.data.id existe, comparar.
* Se divergirem, logar warning seguro e não processar como pagamento.
* Se query data.id não existe, não processar como pagamento.

==================================================
4. DIAGNÓSTICO SEGURO PARA SIGNATURE MISMATCH
=============================================

Adicionar logs seguros quando houver mismatch.

Não logar:

* WebhookSecret
* AccessToken
* x-signature completa
* v1 completo
* payload completo
* token do pedido

Logar apenas:

* has_x_signature = true/false
* has_x_request_id = true/false
* has_query_data_id = true/false
* has_body_data_id = true/false
* query_data_id_masked = primeiros 6 + últimos 4
* body_data_id_masked = primeiros 6 + últimos 4
* data_id_query_was_lowercased = true/false
* ts_present = true/false
* v1_present = true/false
* request_id_masked = primeiros 6 + últimos 4
* secret_configured = true/false
* timestamp_age_seconds, se houver validação de tolerância
* timestamp_within_tolerance = true/false
* received_v1_prefix = primeiros 8 caracteres
* computed_official_prefix = primeiros 8 caracteres
* manifest_parts_included = id/request-id/ts
* failure_reason = missing_signature/missing_ts/missing_v1/missing_secret/timestamp_out_of_tolerance/signature_mismatch

Não logar o manifest completo se ele contiver request-id inteiro.
Não logar query string completa.
Não logar headers completos.

==================================================
5. REVISAR CONFIGURAÇÃO DE SECRET / APP / AMBIENTE
==================================================

Validar e documentar:

* MercadoPago__WebhookSecret está carregado dentro do container.
* O secret usado é da mesma aplicação Mercado Pago do AccessToken usado para criar a order.
* O evento configurado é Order (Mercado Pago), não payment.
* A URL configurada é:
  https://api-teste.vipassessoriadigital.com.br/api/payments/pix/webhooks/mercado-pago
* A URL recebe query param data.id enviado pelo Mercado Pago.
* O Caddy/proxy não remove query string.
* O endpoint não faz redirect que possa perder query string.
* O endpoint webhook está excluído de CSRF.
* O endpoint webhook não exige cookie.
* O endpoint webhook não exige Backoffice/Customer.

Adicionar log seguro no startup:

* PaymentsPix provider: MercadoPago/Fake
* MercadoPago environment: Sandbox/Production
* MercadoPago access token configured: true/false
* MercadoPago webhook secret configured: true/false
* MercadoPago notification URL configured: true/false

Nunca logar valores reais de secret/token.

==================================================
6. TRATAR SIMULADOR DO PAINEL MERCADO PAGO
==========================================

A simulação do painel pode enviar payload genérico e/ou Data ID manual.

Exemplos observados/documentados:

* body raiz id = 123456
* body data.id pode ser ORD...
* query data.id deve ser usada para assinatura
* o ID 123456 não é necessariamente uma Order real criada pelo Shopflow

Regra:

* Se assinatura for válida, mas query data.id não corresponder a ProviderOrderId salvo em PixPayment:

  * registrar evento como Ignored ou NotFound
  * retornar 200/202
  * não marcar PixPayment Paid
  * não marcar Order Paid
  * não confirmar reserva
* Se query data.id for claramente inválido para o fluxo Orders API real, como "123456":

  * registrar Ignored/SimulatorEvent
  * não chamar GET /v1/orders/123456, ou tratar 400/404 como LookupFailed sem stack trace crítico
  * retornar 200/202 se assinatura válida
* Se assinatura for inválida:

  * retornar 401

==================================================
7. PROCESSAMENTO DO WEBHOOK REAL
================================

Depois da assinatura válida:

1. Usar query data.id original como ProviderOrderId para lookup externo.

   * Para HMAC usa lowercase.
   * Para GET /v1/orders/{id}, usar o ID original recebido na query, preservando case, salvo se o client Mercado Pago aceitar lowercase.
   * Preferência: preservar o original para chamada GET.

2. Verificar se existe PixPayment com ProviderOrderId igual ao query data.id.

   * Comparação deve considerar o formato salvo.
   * Como o banco salva ORDTST em uppercase, buscar com valor original.
   * Se necessário, fazer comparação case-insensitive documentada.

3. Se não existir PixPayment:

   * registrar Ignored/NotFound
   * retornar 200/202
   * não marcar pago.

4. Consultar Mercado Pago:
   GET /v1/orders/{ProviderOrderId}

5. Usar a resposta do GET como fonte de verdade.
   Não confiar apenas no body do webhook.

6. Só marcar Paid se:

   * order.status = processed
   * order.status_detail = accredited
   * transação/pagamento, se presente, também processed/accredited
   * payment_method for Pix quando disponível
   * amount bate com PixPayment.Amount

7. Se action_required/waiting_transfer:

   * manter Pending.

8. Se canceled/expired/failed:

   * aplicar status correspondente se já existir regra de domínio.

9. Confirmar reserva.

10. Marcar PixPayment Paid.

11. Marcar Order Paid.

12. Garantir idempotência:

* webhook duplicado não quebra;
* se Order já Paid, retorna sucesso.

==================================================
8. TESTES OBRIGATÓRIOS
======================

Criar/ajustar testes para:

1. Assinatura válida com query data.id numérico conforme manifest oficial.
2. Assinatura válida com query data.id ORDTST uppercase, usando lowercase no manifest.
3. Assinatura inválida retorna 401.
4. x-signature ausente retorna 401.
5. ts ausente retorna 401.
6. v1 ausente retorna 401.
7. x-request-id ausente:

   * manifest remove request-id;
   * mas evento não deve processar pagamento sem diagnóstico claro, se essa for a regra do domínio.
8. data.id query ausente:

   * manifest remove id;
   * evento não processa pagamento como Paid.
9. body.data.id não substitui query data.id no HMAC.
10. query data.id diferente de body.data.id não processa pagamento.
11. Webhook simulado com ID não encontrado registra Ignored/SimulatorEvent.
12. Webhook simulado não marca PixPayment Paid.
13. Webhook simulado não marca Order Paid.
14. Webhook real válido chama GET /v1/orders/{ProviderOrderId}.
15. GET /v1/orders action_required/waiting_transfer mantém Pending.
16. GET /v1/orders processed/accredited marca PixPayment Paid.
17. GET /v1/orders processed/accredited marca Order Paid.
18. GET /v1/orders processed/accredited confirma reserva.
19. Webhook duplicado é idempotente.
20. Endpoint webhook não exige CSRF.
21. Endpoint webhook não exige cookie.
22. Logs não expõem secret/token/x-signature/v1 completos.

Não chamar Mercado Pago real nos testes. Usar fake HttpMessageHandler/mock client.

==================================================
9. DOCUMENTAÇÃO
===============

Atualizar:

* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments-pix.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/technical-debt.md
* deploy/.env.test.example
* deploy/.env.hml.example

Documentar:

* Evento correto: Order (Mercado Pago).
* Assinatura usa query param data.id, não body.
* data.id alfanumérico deve entrar lowercase no manifest.
* Manifest:
  id:<data.id>;request-id:<x-request-id>;ts:<ts>;
* Se data.id/x-request-id ausentes, remover do manifest para HMAC, mas não processar como pagamento se não houver data.id confiável.
* Simulador do painel pode não representar uma Order Pix real do Shopflow.
* Após assinatura válida, consultar GET /v1/orders/{id}.
* Responder 200/201 para notificações recebidas/ignoradas com assinatura válida.
* Não retornar 500 para evento simulado inválido.
* Como diagnosticar Signature mismatch.
* Como conferir secret/app/ambiente.
* Como validar próximo Pix real.

==================================================
10. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Causa raiz encontrada ou hipótese mais forte.
2. Se o validador estava usando body, case errado, manifest errado, secret errado ou query ausente.
3. Arquivos alterados.
4. Como o manifest oficial foi implementado.
5. Como o simulador é tratado.
6. Como o webhook real ORDTST é tratado.
7. Logs seguros adicionados.
8. Testes criados/alterados.
9. Resultado dotnet build.
10. Resultado dotnet test.
11. Docs atualizadas.
12. Como validar no próximo Pix real.

Critérios de aceite:

* Webhook real ORDTST com assinatura válida não falha por Signature mismatch.
* HMAC usa query data.id lowercased.
* Body data.id não substitui query.
* x-request-id vem do header.
* ts/v1 vêm do x-signature.
* Manifest termina com ponto e vírgula.
* Simulador sem ProviderOrderId real não marca pagamento como Paid.
* Processed/accredited real via GET /v1/orders marca PixPayment Paid e Order Paid.
* Pending real mantém Pending.
* Endpoint não exige CSRF/cookie.
* Nenhum secret aparece em log.
* dotnet build passa.
* dotnet test passa.
