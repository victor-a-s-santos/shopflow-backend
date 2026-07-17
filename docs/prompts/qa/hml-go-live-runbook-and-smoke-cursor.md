Você está atuando como engenheiro sênior responsável por DevOps, QA final, HML go-live, validação operacional e smoke testing do projeto Shopflow.

Este prompt NÃO é para criar feature nova.

Objetivo:
Preparar e validar o ambiente HML do Shopflow para smoke test real, com Mercado Pago Sandbox, Worker de reconciliação, migrations, API, frontend, cookies, CORS, Admin Orders e Customer Orders.

Contexto:
O relatório final de QA está em:

docs/qa/MVP-FINAL-QA-GO-LIVE-REPORT.md

Status atual:

* MVP código: avançado.
* HML: READY WITH RISKS.
* Produção: ainda não pronta.
* Principal objetivo agora: transformar HML em HML READY.

O relatório indicou que HML depende de:

* PaymentsPix__Provider=MercadoPago;
* MercadoPago__Enabled=true;
* MercadoPago__Environment=Sandbox;
* MercadoPagoReconciliation__Enabled=true;
* MercadoPago__WebhookRawCaptureEnabled=false;
* migrations aplicadas, especialmente AddCustomerUserIdToOrders;
* api-hml e worker-hml ativos;
* smoke guest;
* smoke customer logado;
* Admin Orders OK;
* Customer Orders OK;
* Cypress crítico executado.

==================================================

1. REGRA PRINCIPAL
   ==================================================

Não criar feature nova.

Não implementar:

* cancelamento;
* reembolso;
* e-mail;
* frete real;
* nota fiscal;
* tracking;
* segunda via Pix;
* claim de pedido guest;
* alteração manual de status;
* novas telas;
* novos endpoints;
* refatoração estrutural.

Você pode:

* ler arquivos;
* validar env examples;
* revisar docker-compose/deploy scripts;
* criar documentação operacional;
* ajustar documentação se estiver desatualizada;
* ajustar somente arquivos `.example` ou docs, se necessário;
* gerar comandos de validação;
* apontar riscos;
* criar checklist de execução;
* criar runbook de troubleshooting.

Não alterar `.env` real.
Não commitar secrets.
Não imprimir secrets.
Não logar AccessToken, WebhookSecret, TokenHashSecret, cookies ou connection string real.

==================================================
2. ARQUIVOS PARA LER
====================

Ler obrigatoriamente:

* docs/qa/MVP-FINAL-QA-GO-LIVE-REPORT.md
* deploy/.env.hml.example
* deploy/.env.test.example
* deploy/docker-compose.yml ou docker-compose equivalente
* deploy/scripts/migrate-hml.sh
* deploy/scripts/migrate-test.sh
* .github/workflows/deploy-vps.yml, se existir
* apps/api/Dockerfile
* apps/api/Dockerfile.worker
* apps/api/src/ApiGateways/Vls.Shopflow.HttpApi/Program.cs
* apps/api/Workers/Vls.Shopflow.Worker/Program.cs
* apps/web/docs/admin-orders.md
* apps/web/docs/customer-orders.md
* docs/orders/admin-orders.md
* docs/orders/customer-orders.md
* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments/MP-PIX-003-webhook-raw-capture-temporary.md
* docs/payments-pix.md

Se o frontend estiver no workspace:

* apps/web/package.json
* apps/web/src/App.tsx
* apps/web/cypress/e2e/admin-orders.cy.ts
* apps/web/cypress/e2e/customer-orders.cy.ts
* apps/web/cypress/e2e/checkout-csrf-pix-flow.cy.ts, se existir

==================================================
3. SAÍDA OBRIGATÓRIA
====================

Criar o documento:

docs/qa/HML-GO-LIVE-RUNBOOK.md

Esse documento deve conter:

1. Objetivo do runbook
2. Pré-requisitos
3. Checklist de env HML
4. Checklist de Mercado Pago Sandbox
5. Checklist de Docker/containers
6. Ordem correta de deploy HML
7. Ordem correta de migrations
8. Comandos para subir API e Worker
9. Comandos para validar logs
10. Comandos curl de saúde/autenticação/endpoints críticos
11. Checklist de frontend/Cloudflare Pages
12. Cypress a executar
13. Smoke test guest passo a passo
14. Smoke test customer logado passo a passo
15. Smoke test admin orders passo a passo
16. Validação de banco com queries úteis
17. Troubleshooting
18. Critérios para declarar HML READY
19. Critérios para declarar HML NOT READY
20. O que NÃO fazer em HML
21. Próximos passos após HML READY

Também criar, se fizer sentido:

docs/qa/HML-SMOKE-TEST-CHECKLIST.md

Com checklist marcável, mais curto, para o usuário executar manualmente.

==================================================
4. VALIDAR ENV HML
==================

Validar se `deploy/.env.hml.example` orienta corretamente HML.

Para HML, o esperado é:

PaymentsPix:

* PaymentsPix__Provider=MercadoPago

Mercado Pago:

* MercadoPago__Enabled=true
* MercadoPago__Environment=Sandbox
* MercadoPago__BaseUrl=https://api.mercadopago.com
* MercadoPago__AccessToken=<preencher secret real fora do git>
* MercadoPago__WebhookSecret=<preencher secret real fora do git>
* MercadoPago__ApplicationId=<preencher>
* MercadoPago__UserId=<preencher>
* MercadoPago__NotificationUrl=https://api-hml.../api/payments/pix/webhooks/mercado-pago
* MercadoPago__SendNotificationUrlInOrderCreate=false
* MercadoPago__WebhookRawCaptureEnabled=false
* MercadoPago__SandboxPayerFirstNameOverride=APRO, opcional apenas HML/teste

Reconciliação:

* MercadoPagoReconciliation__Enabled=true
* MercadoPagoReconciliation__IntervalSeconds=60
* MercadoPagoReconciliation__BatchSize=20
* MercadoPagoReconciliation__MaxAgeMinutes=180

Guest:

* GuestOrderAccess__Enabled=true
* GuestOrderAccess__TokenHashSecret=<segredo forte>
* GuestOrderAccess__TokenTtlDays=30
* GuestOrderAccess__RateLimitPerMinute=30

Segurança:

* AllowedOrigins apontando para frontend HML
* DataProtection__KeysPath em volume persistente
* SHOPFLOW_ADMIN_RESET_PASSWORD=false
* DemoCatalogSeed__Enabled=true pode ficar em HML, mas deve estar documentado como proibido em produção
* Scalar/OpenAPI não deve ficar aberto se o projeto decidiu restringir a Development

Se o `.env.hml.example` estiver com `PaymentsPix__Provider=Fake`, `MercadoPago__Enabled=false` ou `MercadoPagoReconciliation__Enabled=false`, documentar como risco e, se seguro, atualizar o `.example` para refletir HML real com placeholders sem secrets.

Não alterar `.env.hml` real.

==================================================
5. VALIDAR ORDEM DE DEPLOY HML
==============================

O runbook precisa deixar a ordem explícita:

1. Fazer pull do código no servidor.
2. Validar docker compose config.
3. Buildar API e Worker.
4. Subir/recriar API primeiro para aplicar migrations.
5. Conferir logs da API e migrations.
6. Subir/recriar Worker depois.
7. Conferir logs do Worker.
8. Validar /health.
9. Validar frontend Cloudflare apontando para API HML.
10. Rodar Cypress crítico.
11. Rodar smoke guest.
12. Rodar smoke customer logado.
13. Rodar smoke admin.
14. Registrar resultado.

Incluir comandos adaptáveis, por exemplo:

cd deploy
git pull
docker compose config
docker compose build api-hml worker-hml
./scripts/migrate-hml.sh
docker compose up -d --force-recreate api-hml
docker compose logs --tail=120 api-hml
docker compose up -d --force-recreate worker-hml
docker compose logs --tail=120 worker-hml
docker compose ps

Se os nomes dos services forem diferentes, instruir o usuário a rodar:

docker compose ps --services

e adaptar.

==================================================
6. MIGRATIONS
=============

Validar que o runbook destaque:

* API aplica migrations no boot.
* Worker não aplica migrations.
* API deve subir/reiniciar antes do Worker.
* Migration AddCustomerUserIdToOrders é obrigatória.
* Se essa migration não subir, Customer Orders quebra.
* Conferir logs da API após restart.

Incluir comandos:

cd deploy
./scripts/migrate-hml.sh

ou, se o script só reinicia a API:

docker compose up -d --force-recreate api-hml
docker compose logs --tail=150 api-hml | grep -iE "migration|migrate|error|exception|CustomerUserId"

==================================================
7. VALIDAR CONTAINERS E LOGS
============================

Incluir comandos:

docker compose ps
docker compose logs -f api-hml
docker compose logs -f worker-hml
docker compose logs -f caddy
docker compose logs -f postgres

Logs focados:

docker compose logs -f api-hml | grep -iE "PaymentsPix|MercadoPago|Webhook|Reconciliation|DataProtection|CORS|CSRF|error|exception"

docker compose logs -f worker-hml | grep -iE "reconciliation|MercadoPago|processed|accredited|Paid|Expiration|reservation|error|exception"

Critérios esperados:

* API sobe sem erro.
* Worker sobe sem erro.
* PaymentsPix provider aparece MercadoPago.
* Reconciliation worker started.
* Expiration worker active.
* Raw capture não aparece ativo.
* Não aparecem AccessToken/WebhookSecret em logs.

==================================================
8. VALIDAR HEALTH E ENDPOINTS COM CURL
======================================

Criar seção com comandos curl usando placeholders:

API_HML=https://api-hml.seudominio.com.br
WEB_HML=https://hml.seudominio.com.br

Health:

curl -i "$API_HML/health"

CSRF:

curl -i "$API_HML/api/auth/csrf"

Catalog:

curl -i "$API_HML/api/catalog/products"

Imagem demo:

curl -i "$API_HML/uploads/seed-products/camiseta-basica-branca.png"

Admin sem cookie deve bloquear:

curl -i "$API_HML/api/admin/orders"

Esperado: 401/redirect conforme padrão.

Customer sem cookie deve bloquear:

curl -i "$API_HML/api/customer/orders"

Esperado: 401.

Guest status sem token deve bloquear:

curl -i "$API_HML/api/orders/guest/{orderId}/status"

Esperado: 401/403/400 conforme implementação.

Não incluir secrets nos curls.

==================================================
9. VALIDAR FRONTEND HML
=======================

Se o frontend estiver disponível:

Validar:

* variável `VITE_API_BASE_URL` ou equivalente aponta para API HML;
* frontend faz requests com credentials include;
* imagens `/uploads` resolvem pela origem da API;
* rotas funcionam:

  * /
  * /cart
  * /checkout
  * /checkout/pix/:orderId
  * /admin/login
  * /admin/orders
  * /account/orders

Criar comandos sugeridos:

cd apps/web
npm run typecheck
npm run build
npx cypress run --spec cypress/e2e/admin-orders.cy.ts
npx cypress run --spec cypress/e2e/customer-orders.cy.ts

Se existir spec Pix:

npx cypress run --spec cypress/e2e/checkout-csrf-pix-flow.cy.ts

Se Cypress precisar de env:

* documentar as variáveis necessárias;
* não criar secrets;
* orientar criação local de cypress.env.json ignorado pelo git.

==================================================
10. SMOKE TEST GUEST
====================

Criar checklist operacional detalhado:

1. Abrir frontend HML sem login.
2. Escolher produto demo.
3. Verificar imagem.
4. Adicionar SKU ao carrinho.
5. Ir para checkout.
6. Preencher dados de convidado.
7. Criar pedido.
8. Gerar Pix Mercado Pago Sandbox.
9. Confirmar que tela Pix mostra QR/copia-e-cola ou instrução de pagamento.
10. Pagar no sandbox.
11. Aguardar Worker reconciliação.
12. Ver logs do Worker:

    * Candidates=1
    * GET /v1/orders/{ORDTST...}
    * processed/accredited
    * Outcome=Paid ou MarkedPaid=1
13. Tela Pix deve atualizar para Pagamento aprovado.
14. Entrar no Admin.
15. Abrir /admin/orders.
16. Ver pedido como Paid.
17. Abrir detalhe.
18. Confirmar cliente, entrega, itens, total e payment Paid.
19. Confirmar que esse pedido guest não aparece em /account/orders apenas por e-mail.

Critério de sucesso:

* Order Paid.
* PixPayment Paid.
* Reserva confirmada.
* Admin vê pedido.
* Customer area não lista guest por e-mail.

==================================================
11. SMOKE TEST CUSTOMER LOGADO
==============================

Criar checklist:

1. Registrar ou logar customer.
2. Confirmar /api/auth/customer/me.
3. Escolher produto.
4. Adicionar ao carrinho.
5. Checkout logado.
6. Criar pedido.
7. Gerar Pix.
8. Pagar sandbox.
9. Worker reconcilia.
10. Admin vê pedido Paid.
11. Customer abre /account/orders.
12. Pedido aparece.
13. Abrir /account/orders/:id.
14. Ver resumo, entrega, itens, totais e pagamento.
15. Confirmar que não aparecem providerOrderId, providerPaymentId, QR, copia-e-cola, ticketUrl, token ou secret.

Critério de sucesso:

* Pedido logado fica vinculado a CustomerUserId.
* Customer vê apenas seus pedidos.

==================================================
12. SMOKE TEST ADMIN
====================

Criar checklist:

1. Login admin.
2. Abrir /admin/orders.
3. Filtrar por Paid.
4. Buscar pelo e-mail/nome/id do pedido.
5. Abrir detalhe.
6. Conferir:

   * cliente;
   * entrega;
   * itens;
   * subtotal;
   * frete;
   * total;
   * payment provider/status.
7. Confirmar ausência de:

   * QR Code;
   * copia-e-cola;
   * ticketUrl;
   * AccessToken;
   * WebhookSecret;
   * GuestAccessToken;
   * x-signature.

Critério de sucesso:

* Admin consegue operar pedido pago.

==================================================
13. VALIDAÇÃO DE BANCO
======================

Incluir queries adaptadas ao projeto:

Pix por ProviderOrderId:

SELECT "Id", "OrderId", "Status", "Provider", "ProviderOrderId",
"ProviderStatus", "ProviderStatusDetail",
"ProviderTransactionStatus", "ProviderTransactionStatusDetail",
"PaidAt", "ExpiresAt", "CreatedAt"
FROM payments_pix.pix_payments
WHERE "ProviderOrderId" = 'ORDTST...';

Order:

SELECT "Id", "Status", "Total", "CustomerUserId", "PaidAt", "CreatedAt"
FROM orders.orders
WHERE "Id" = '...';

Índice CustomerUserId:

SELECT indexname
FROM pg_indexes
WHERE schemaname = 'orders'
AND indexname = 'IX_orders_CustomerUserId_CreatedAt';

Reservas:

SELECT "Id", "SkuId", "Status", "Quantity", "ExpiresAt"
FROM inventory.stock_reservations
ORDER BY "ExpiresAt" DESC
LIMIT 50;

Deixar claro:

* Não marcar Paid manualmente sem evidência processed/accredited no Mercado Pago.
* Preferir reativar Worker se algo ficar Pending.

==================================================
14. TROUBLESHOOTING
===================

Criar uma seção de problemas comuns:

A) API não sobe

* checar logs;
* connection string;
* migrations;
* variáveis obrigatórias.

B) Worker não encontra candidatos

* verificar PixPayment Status Pending;
* Provider MercadoPago;
* ProviderOrderId preenchido;
* MaxAgeMinutes;
* banco/connection string do worker.

C) Worker acha candidato mas não marca Paid

* verificar GET /v1/orders;
* AccessToken Sandbox;
* status/status_detail;
* processed/accredited;
* reserva expirada;
* logs de exception.

D) Pix continua Pending na tela

* verificar guest access token;
* polling frontend;
* status no banco;
* worker logs;
* API guest status.

E) Customer Orders vazio após compra logada

* migration CustomerUserId;
* pedido realmente criado com CustomerCookie;
* frontend checkout enviando credentials;
* Order.CustomerUserId não null.

F) Admin Orders não abre

* admin cookie;
* CSRF/login;
* CORS;
* AdminRouteGuard;
* endpoint Backoffice.

G) Cookie não persiste em HML

* HTTPS;
* SameSite;
* Secure;
* domínio;
* DataProtection persistido;
* CORS AllowCredentials.

H) Imagem demo 404

* seed assets;
* Dockerfile copiando pasta;
* volume uploads;
* Caddy servindo /uploads.

I) Webhook Mercado Pago continua falhando assinatura

* não bloquear HML se Worker reconcilia;
* manter raw capture off;
* abrir suporte MP;
* validar secret/app/user;
* validar notification URL no painel.

==================================================
15. CRITÉRIOS DE HML READY
==========================

Declarar HML READY somente se:

* API HML sobe.
* Worker HML sobe.
* /health OK.
* migrations sem erro.
* PaymentsPix Provider MercadoPago.
* Reconciliation Enabled true.
* RawCapture false.
* Admin login OK.
* Customer login/register OK.
* Produto demo visível.
* Checkout guest gera Pix.
* Pagamento sandbox guest vira Paid via Worker.
* Admin vê pedido guest Paid.
* Checkout customer logado gera Pix.
* Pagamento sandbox customer vira Paid via Worker.
* Customer vê pedido em Meus pedidos.
* Admin Orders e Customer Orders não vazam campos sensíveis.
* Cypress admin-orders e customer-orders passam, ou se não rodarem, motivo registrado.
* Logs sem secrets.

==================================================
16. CRITÉRIOS DE HML NOT READY
==============================

Declarar HML NOT READY se:

* API não sobe.
* Worker não sobe.
* migrations falham.
* Provider continua Fake.
* Reconciliation off.
* Pix não é criado.
* Worker não marca Paid mesmo com Mercado Pago processed/accredited.
* Admin não vê pedido pago.
* Customer logado não vê pedido próprio.
* Cookies/CORS impedem login.
* Logs expõem secrets.
* Raw capture ativa em HML sem necessidade.
* DataProtection não persistido e cookies quebram a cada deploy.

==================================================
17. O QUE NÃO FAZER EM HML
==========================

Não:

* usar credenciais Production;
* commitar `.env.hml` real;
* expor secrets;
* ativar raw capture sem necessidade;
* deixar provider Fake para validar MVP;
* marcar pedidos como Paid manualmente sem evidência MP;
* desativar Worker;
* alterar código de negócio durante smoke;
* misturar teste de produção real com HML.

==================================================
18. RESULTADO ESPERADO NO CHAT
==============================

Ao final, retorne:

1. Arquivo criado/alterado.
2. Se atualizou algum `.example` ou apenas docs.
3. Lista de riscos HML que continuam.
4. Comandos principais para o usuário executar.
5. Checklist resumido para declarar HML READY.
6. Se encontrou alguma inconsistência crítica no relatório anterior.
7. Se recomenda HML READY, HML READY WITH RISKS ou HML NOT READY antes do smoke.

Critérios de aceite:

* Nenhuma feature nova criada.
* Runbook HML criado.
* Checklist manual criado ou incluído.
* Comandos de deploy/validação documentados.
* Smoke guest/customer/admin documentados.
* Troubleshooting documentado.
* Critérios HML READY/NOT READY claros.
* Sem secrets em docs.
