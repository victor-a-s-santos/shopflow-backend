Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, segurança, e-commerce e acesso público limitado por token.

Objetivo:
Implementar um token seguro de acesso para pedidos de clientes convidados, permitindo que o frontend consulte o status limitado de um pedido sem login e sem expor endpoints Backoffice.

Nome da feature:
Guest Order Access Token

Contexto:
O Shopflow já possui:

* Catálogo real de roupas.
* Carrinho por SKU.
* Checkout convidado.
* CheckoutSession.
* Orders.
* PaymentsPix.
* Mercado Pago Pix via Orders API.
* Webhook Mercado Pago Order.
* PixPayment Paid quando Mercado Pago retorna processed/accredited.
* Order Paid após webhook.
* Inventory reservation confirmada após pagamento.
* Frontend Pix pronto:

  * /checkout/pix/:orderId
  * QR Code
  * Pix copia-e-cola
  * ticketUrl
  * sessionStorage `shopflow.pendingPix.v1.<orderId>`
* Atualmente o frontend NÃO usa endpoints Backoffice:

  * não usa GET /api/orders/{id}
  * não usa GET /api/payments/pix/{id}
  * não usa GET /api/payments/pix/by-order/{orderId}

Problema:
Depois que o webhook do Mercado Pago confirma o pagamento, o frontend público ainda não consegue consultar com segurança se o pedido mudou para Paid.

Solução:
Criar um token de acesso público limitado para pedidos convidados.

Esse token deve permitir apenas consultar status limitado do pedido, sem login e sem PII sensível.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Antes de implementar, leia:

* docs/prompts/00-project-context.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/orders.md
* docs/cart-checkout.md
* docs/payments-pix.md
* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/expiration-worker.md
* módulo Orders completo
* módulo PaymentsPix completo
* módulo CartCheckout completo
* endpoint POST /api/orders/from-checkout-session
* endpoint POST /api/payments/pix/orders/{orderId}
* endpoints Orders protegidos
* endpoints PaymentsPix protegidos
* Identity Customer/Admin
* CSRF middleware
* rate limiting atual
* testes Orders/PaymentsPix/Checkout atuais

Seguir arquitetura existente.
Não criar arquitetura paralela.

==================================================
2. ESCOPO
=========

Implementar:

1. Geração de token seguro para pedido convidado.
2. Persistência apenas do hash do token.
3. Retorno do token uma única vez no fluxo de criação do pedido.
4. Endpoint público limitado para status do pedido por token.
5. Rate limit.
6. Testes.
7. Documentação.

Não implementar agora:

* Página frontend de acompanhamento.
* E-mail transacional.
* Admin Orders UI.
* Reenvio de token.
* Busca por e-mail.
* Login obrigatório.
* Exposição de dados completos do pedido.
* Endpoint público sem token.
* Mercado Pago novo.
* Alteração no fluxo do webhook, salvo se necessário para status.

==================================================
3. MODELO DE DADOS
==================

Criar entidade/tabela:

GuestOrderAccessToken

ou nome equivalente no padrão do módulo Orders.

Campos sugeridos:

* Id
* OrderId
* TokenHash
* TokenHashAlgorithm
* CreatedAt
* ExpiresAt
* RevokedAt
* LastUsedAt
* UsageCount
* CreatedByIp, opcional
* UserAgentHash, opcional
* Purpose = "GuestOrderStatus", se fizer sentido

Regras:

* Nunca persistir token bruto.
* Token bruto só deve ser retornado uma vez, na resposta de criação do pedido.
* Token deve ser forte: pelo menos 256 bits aleatórios.
* Usar RandomNumberGenerator.
* Token deve ser Base64Url.
* Hash sugerido:

  * HMAC-SHA256 com secret de aplicação;
  * ou SHA-256 se não houver secret, mas preferir HMAC.
* Se usar HMAC, criar config:
  GuestOrderAccess__TokenHashSecret
* Não logar token bruto.
* Não retornar TokenHash em DTO.
* Token pode expirar em 30 dias por padrão.

Configuração sugerida:

GuestOrderAccess:
Enabled: true
TokenTtlDays: 30
TokenHashSecret: ""
RateLimitPerMinute: 30

Env vars:

GuestOrderAccess__Enabled=true
GuestOrderAccess__TokenTtlDays=30
GuestOrderAccess__TokenHashSecret=<secret-forte>
GuestOrderAccess__RateLimitPerMinute=30

Regras:

* Em HML/Production, TokenHashSecret deve ser obrigatório se HMAC for usado.
* Não commitar secret real.
* Atualizar .env examples com placeholder.

==================================================
4. QUANDO GERAR O TOKEN
=======================

Gerar token no fluxo:

POST /api/orders/from-checkout-session

Quando:

* checkout for convidado;
* Order for criada com sucesso;
* Order ainda está PendingPayment.

Resposta atual do endpoint deve ser estendida de forma retrocompatível.

Adicionar campos opcionais:

{
"orderId": "...",
"orderNumber": "...",
"status": "PendingPayment",
"...": "...",
"guestAccessToken": "<token-bruto>",
"guestAccessTokenExpiresAt": "..."
}

Regras:

* Se pedido for de cliente autenticado, avaliar:

  * pode não gerar guest token;
  * ou gerar token apenas se checkout tiver sido guest.
* Não quebrar frontend atual.
* Campo pode ser nullable.
* Token só deve aparecer nessa resposta.
* GETs protegidos de Orders não devem retornar guestAccessToken.
* PaymentsPix não deve retornar guestAccessToken.
* Webhook não deve retornar token.
* Logs não devem conter token.

Idempotência:

* Se POST /orders/from-checkout-session for chamado de novo e o pedido já existir:

  * não gerar infinitos tokens;
  * retornar token ativo existente apenas se for seguro e se ele ainda não foi retornado?
  * Como o token bruto não é armazenado, não é possível retornar o token bruto antigo.

Decisão recomendada:

* Para idempotência do endpoint, se Order já existe e já há token ativo, retornar Order sem guestAccessToken e documentar.
* Alternativa: gerar um novo token ativo e revogar o anterior apenas em chamada idempotente autenticada pelo checkout session. Se escolher essa alternativa, documentar claramente.
* Não gerar múltiplos tokens ativos sem controle.

Como o frontend cria Order uma vez e segue para Pix, o caminho normal deve receber o token na primeira resposta.

==================================================
5. ENDPOINT PÚBLICO LIMITADO
============================

Criar endpoint:

GET /api/orders/guest/{orderId}/status

Autorização:

* Header obrigatório:
  X-ORDER-ACCESS-TOKEN: <token>

Alternativa opcional:

* Não usar query string como principal, porque token em URL pode ir para logs/histórico.
* Se implementar query string futuramente para link de e-mail, documentar riscos.
* Nesta etapa, usar header.

Regras:

* Endpoint público, sem cookie.
* Não exige CSRF, porque é GET.
* Não exige Customer/Auth.
* Não exige Backoffice.
* Rate-limited.
* Valida token:

  * calcula hash/HMAC;
  * encontra token por OrderId + TokenHash;
  * verifica não expirado;
  * verifica não revogado;
  * atualiza LastUsedAt e UsageCount.
* Se inválido/expirado/revogado:

  * retornar 401 ou 403.
* Não revelar se OrderId existe quando token inválido.
* Evitar mensagens que permitam enumeração.

==================================================
6. DTO DE STATUS LIMITADO
=========================

Retornar apenas dados necessários para frontend público.

DTO sugerido:

{
"orderId": "...",
"orderNumber": "...",
"orderStatus": "PendingPayment",
"payment": {
"status": "Pending",
"provider": "MercadoPago",
"amount": 159.90,
"expiresAt": "...",
"paidAt": null,
"updatedAt": "..."
},
"items": [
{
"productName": "Camiseta Básica Algodão",
"skuId": "...",
"quantity": 1,
"unitPrice": 59.90,
"total": 59.90,
"attributes": {
"Cor": "Branco",
"Tamanho": "M"
},
"imageUrl": "/uploads/seed-products/camiseta-basica-branca.png"
}
],
"totals": {
"subtotal": 159.90,
"discount": 0,
"shipping": 0,
"total": 159.90
},
"customer": {
"name": "Vi***",
"email": "v***@gmail.com"
},
"access": {
"expiresAt": "...",
"lastUsedAt": "..."
}
}

Regras:

* Não retornar:

  * endereço completo;
  * telefone completo;
  * CPF/documento;
  * gateway raw response;
  * ProviderOrderId;
  * ProviderTransactionId;
  * TokenHash;
  * guestAccessToken;
  * dados internos de reserva;
  * logs/erros internos.
* Nome e e-mail devem ser mascarados.
* Se não houver imagem/atributos no Order atual, retornar o que existir.
* Não depender de endpoint Backoffice.

==================================================
7. STATUS MAPPING PARA FRONTEND
===============================

O endpoint deve permitir a tela Pix entender:

* PendingPayment + Pix Pending:
  "Aguardando pagamento"

* Paid + Pix Paid:
  "Pagamento aprovado"

* Expired:
  "Pedido expirado"

* Canceled:
  "Pedido cancelado"

* Failed:
  "Pagamento não aprovado"

Se houver divergência:

* Pix Paid mas Order ainda PendingPayment:
  retornar estado consistente ou `requiresReview`, se existir.
* Order Paid mas Pix status pendente:
  retornar Order Paid como fonte principal, mas logar inconsistência.

Não criar status fake.

==================================================
8. RATE LIMIT / SEGURANÇA
=========================

Adicionar rate limit para:

GET /api/orders/guest/{orderId}/status

Sugestão:

* 30/min por IP em HML/Prod
* 100/min em Dev/Test, se necessário para testes

Regras:

* Não permitir enumeração massiva.
* Token forte reduz risco, mas rate limit ainda é necessário.
* Logs sem token bruto.
* Se logar tentativa inválida, mascarar OrderId parcialmente se desejado.

==================================================
9. CSRF / AUTH
==============

Endpoint:

* GET público com token.
* Não exige CSRF.
* Não exige cookie.
* Não usa Customer/Auth.
* Não usa Backoffice.

POST /orders/from-checkout-session:

* manter regras atuais.
* Se já é endpoint público do checkout, preservar.
* Não transformar em endpoint autenticado.

==================================================
10. TESTES OBRIGATÓRIOS
=======================

Criar testes para:

1. POST /orders/from-checkout-session para guest retorna guestAccessToken.
2. Token bruto não é persistido no banco.
3. TokenHash é persistido.
4. GET /orders/guest/{orderId}/status com token válido retorna 200.
5. GET com token ausente retorna 401/403.
6. GET com token inválido retorna 401/403.
7. GET com token expirado retorna 401/403.
8. GET com token revogado retorna 401/403.
9. GET com token de outro pedido não acessa pedido.
10. GET não retorna TokenHash.
11. GET não retorna guestAccessToken.
12. GET não retorna ProviderOrderId/ProviderTransactionId.
13. GET não retorna endereço completo/telefone/documento.
14. Nome/e-mail vêm mascarados.
15. LastUsedAt e UsageCount são atualizados.
16. Endpoint não exige cookie admin/customer.
17. Endpoint não exige CSRF.
18. Endpoint respeita status Paid após webhook.
19. Endpoint mostra Pending antes do webhook.
20. Endpoint mostra Expired quando worker expira.
21. GET protegido Backoffice continua protegido.
22. PaymentsPix GET Backoffice continua protegido.

Executar:

dotnet build
dotnet test

==================================================
11. MIGRATIONS
==============

Se criar tabela nova, criar migration.

Nome sugerido:

AddGuestOrderAccessTokens

Garantir:

* FK para Orders.
* Índice por OrderId.
* Índice por TokenHash.
* Índice único por OrderId + TokenHash.
* Índice para ExpiresAt, se útil.
* Não criar unique global em TokenHash se HMAC e randomness já tornam único, mas pode ser ok.
* Soft revoke por RevokedAt.

==================================================
12. DOCUMENTAÇÃO
================

Criar:

* docs/security/SEC-006-guest-order-access-token.md
* docs/orders/ORD-002-guest-order-status.md, se estrutura existir

Atualizar:

* docs/orders.md
* docs/cart-checkout.md
* docs/payments-pix.md
* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* deploy/.env.test.example
* deploy/.env.hml.example
* docker-compose.yml, se necessário
* docs/testing.md, se existir

Documentar:

* objetivo do token;
* onde o token é gerado;
* que token bruto só retorna uma vez;
* header X-ORDER-ACCESS-TOKEN;
* endpoint público limitado;
* campos retornados;
* campos que nunca retornam;
* TTL;
* rate limit;
* como frontend deve usar;
* limitações;
* próximo passo frontend.

==================================================
13. BUILD / TESTES
==================

Executar:

dotnet build
dotnet test

Se docker/env mudar:

docker compose build api worker

Não pular testes.
Não remover testes existentes.
Se algum teste quebrar, corrigir causa real.

==================================================
14. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Entidade/tabela criada.
3. Migration criada.
4. Como o token é gerado.
5. Como o token é armazenado.
6. Como o token é retornado na criação do pedido.
7. Endpoint público criado.
8. DTO limitado retornado.
9. Campos sensíveis removidos/mascarados.
10. Rate limit aplicado.
11. Como CSRF/Auth foram tratados.
12. Testes criados.
13. Resultado build/test.
14. Docs atualizadas.
15. Limitações.
16. Próximo passo recomendado.

Critérios de aceite:

* Pedido guest recebe guestAccessToken na criação.
* Token bruto não é salvo no banco.
* Apenas hash é salvo.
* GET /api/orders/guest/{orderId}/status funciona com X-ORDER-ACCESS-TOKEN.
* Token inválido/ausente/expirado/revogado é negado.
* Endpoint não expõe PII sensível.
* Endpoint não expõe ProviderOrderId/ProviderTransactionId.
* Endpoint não expõe TokenHash.
* Endpoint não exige cookie.
* Endpoint não exige CSRF.
* Endpoints Backoffice continuam protegidos.
* Status Paid após webhook fica visível de forma segura.
* dotnet build passa.
* dotnet test passa.
* Docs refletem estado real.
