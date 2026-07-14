Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Mercado Pago Orders API, webhooks, idempotência e observabilidade segura.

Problema:
Ao simular um teste pelo painel de webhook do Mercado Pago, o backend recebe o webhook e tenta consultar:

GET /v1/orders/123456

O Mercado Pago retorna 400 porque `123456` não é uma Order real da Orders API. O backend registra erro com stack trace:

Mercado Pago order lookup failed with status 400.

Objetivo:
Ajustar o processamento do webhook Mercado Pago para tratar eventos de teste/simulação ou IDs inválidos de forma segura, sem marcar pedido como pago, sem quebrar o endpoint e sem gerar erro crítico desnecessário.

Contexto:
O fluxo real usa Orders API.
Orders reais têm IDs como:
ORD...
ORDTST...

O webhook real deve processar ProviderOrderId real salvo em PixPayment.
O teste do painel pode enviar data.id genérico, como 123456.

Regras:
- Não marcar PixPayment Paid se o ID não for uma order real.
- Não marcar Order Paid.
- Não confirmar reserva.
- Não expor secrets.
- Não ignorar silenciosamente webhooks reais válidos.
- Não quebrar validação de assinatura.
- Não mexer no frontend.

Implementar:

1. Antes de chamar GET /v1/orders/{id}, validar formato básico do data.id para Orders API.
   - Aceitar IDs começando com ORD ou ORDTST, se esse padrão for consistente no projeto.
   - Se data.id for claramente inválido, como "123456", registrar evento como Ignored ou InvalidProviderOrderId.
   - Retornar 200/202 para evitar retry inútil do Mercado Pago.
   - Logar warning seguro:
     "Mercado Pago webhook ignored: invalid order id format."

2. Se chamar GET /v1/orders/{id} e Mercado Pago retornar 400/404:
   - Não lançar exceção não tratada.
   - Atualizar mercado_pago_webhook_events com:
     ProcessingStatus = LookupFailed ou Ignored
     ErrorMessage limitado e sem dados sensíveis.
   - Retornar 200/202, se assinatura era válida.
   - Não marcar pagamento como pago.

3. Se Mercado Pago retornar 401/403:
   - Tratar como erro de configuração AccessToken.
   - Logar erro seguro.
   - Pode retornar 500 ou 202 conforme decisão documentada, mas deve alertar claramente.

4. Se Mercado Pago retornar 5xx/timeout:
   - Manter erro processável/retry, conforme estratégia atual.
   - Não marcar pago.

5. Melhorar logs seguros:
   - webhook received
   - providerOrderId masked
   - dataId source
   - signature valid
   - lookup status code
   - processing status final
   - nunca logar AccessToken/WebhookSecret/x-signature completa.

6. Testes obrigatórios:
   - Webhook com assinatura válida e data.id=123456 não marca pago.
   - Webhook com data.id=123456 registra evento Ignored/LookupFailed.
   - Webhook com data.id=123456 retorna 200/202.
   - Webhook com ORDTST válido chama GET /v1/orders/{id}.
   - GET /v1/orders 400 não derruba endpoint.
   - GET /v1/orders 404 não derruba endpoint.
   - GET /v1/orders processed/accredited continua marcando PixPayment Paid, Order Paid e confirmando reserva.
   - Webhook inválido por assinatura continua 401.
   - Webhook não exige CSRF/cookie.

7. Documentação:
   Atualizar:
   - docs/payments/MP-PIX-002-orders-provider-and-webhook.md
   - docs/payments-pix.md
   - docs/ai-context/shopflow-current-state.md
   - docs/ai-context/technical-debt.md

Documentar:
- Simulação do painel pode enviar ID genérico.
- ID genérico não confirma pagamento.
- Teste real precisa usar checkout que cria ORD/ORDTST.
- Como consultar manualmente GET /v1/orders/{ProviderOrderId}.
- Como interpretar processed/accredited.

Resultado esperado:
- Eventos simulados com data.id inválido não poluem log como falha crítica.
- Webhooks reais ORD/ORDTST continuam processando normalmente.
- Pagamento só vira Paid com processed/accredited.
- dotnet build passa.
- dotnet test passa.