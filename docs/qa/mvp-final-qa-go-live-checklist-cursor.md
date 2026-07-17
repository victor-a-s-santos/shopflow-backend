Você está atuando como engenheiro sênior responsável por QA final, release readiness, segurança, DevOps e validação de MVP do projeto Shopflow.

Este prompt NÃO é para criar feature nova.

Objetivo:
Fazer uma auditoria final do MVP antes de entrega/go-live, validando documentação, envs, rotas, testes, migrations, fluxos críticos, riscos e pendências.

Contexto atual do Shopflow:
O projeto já possui:

Backend:

* Catálogo com produtos, SKUs, imagens e categorias.
* Estoque com reservas internas.
* CheckoutSession com reserva de estoque.
* Orders criadas a partir de CheckoutSession.
* PaymentsPix com Mercado Pago Orders API.
* Worker de expiração.
* Worker de reconciliação Mercado Pago Pix funcionando:

  * consulta GET /v1/orders/{ProviderOrderId};
  * se Mercado Pago retorna processed/accredited:

    * PixPayment vira Paid;
    * Order vira Paid;
    * reserva de estoque é confirmada.
* Webhook Mercado Pago ainda com assinatura inválida em sandbox, tratado como dívida técnica.
* Reconciliação via Worker é fallback operacional do MVP.
* GuestOrderAccessToken para status de pedido convidado.
* Admin Auth com cookie HttpOnly, CSRF e policy Backoffice.
* Customer Auth com cookie HttpOnly separado.
* Admin Orders backend:

  * GET /api/admin/orders
  * GET /api/admin/orders/{orderId}
* Customer Orders backend:

  * GET /api/customer/orders
  * GET /api/customer/orders/{orderId}
  * Order.CustomerUserId nullable;
  * pedido guest não aparece automaticamente por e-mail.

Frontend:

* Vitrine, produto, carrinho e checkout.
* Tela Pix com polling seguro.
* Admin Auth.
* Admin Orders:

  * /admin/orders
  * /admin/orders/:orderId
* Customer area.
* Customer Orders:

  * /account/orders
  * /account/orders/:id

Infra atual:

* Frontend Cloudflare Pages.
* Backend + Worker + Postgres em VPS via Docker Compose.
* Caddy como proxy/SSL.
* Cloudflare DNS/CDN.
* Ambientes test/HML/prod por subdomínios.
* Deploy via GitHub Actions/SSH ou manual.

==================================================

1. REGRA MAIS IMPORTANTE
   ==================================================

Não criar feature nova.

Não implementar:

* cancelamento;
* reembolso;
* fulfillment;
* envio;
* tracking;
* nota fiscal;
* segunda via Pix;
* claim de pedido guest;
* e-mails transacionais;
* dashboard financeiro;
* cupons;
* frete real;
* alteração manual de status;
* refatoração estrutural.

Você pode:

* validar;
* rodar testes;
* ler código;
* apontar riscos;
* apontar inconsistências;
* sugerir correções;
* criar um relatório final de QA;
* atualizar documentação de checklist, se necessário.

Se encontrar bug crítico:

* não resolver automaticamente sem registrar;
* documentar claramente:

  * impacto;
  * evidência;
  * arquivo/rota/config afetada;
  * severidade;
  * sugestão de correção;
  * se bloqueia ou não o MVP.

==================================================
2. SAÍDA OBRIGATÓRIA
====================

Criar um relatório final em:

docs/qa/MVP-FINAL-QA-GO-LIVE-REPORT.md

Se a pasta não existir, criar.

O relatório deve conter:

1. Resumo executivo
2. Status geral:

   * READY
   * READY WITH RISKS
   * NOT READY
3. Percentual estimado de prontidão
4. Fluxos validados
5. Fluxos não validados
6. Rotas backend validadas
7. Rotas frontend validadas
8. Configs/envs obrigatórias
9. Migrations aplicáveis
10. Testes executados e resultados
11. Smoke tests manuais recomendados
12. Riscos bloqueantes
13. Riscos não bloqueantes
14. Dívidas técnicas pós-MVP
15. Checklist de go-live test/HML
16. Checklist de go-live produção
17. Plano de rollback
18. Próximos passos recomendados

Não mascarar problemas. Ser direto.

==================================================
3. VALIDAÇÃO DE DOCUMENTAÇÃO
============================

Validar se existem e estão coerentes, quando aplicável:

Backend docs:

* docs/orders/admin-orders.md
* docs/orders/customer-orders.md
* docs/payments/MP-PIX-002-orders-provider-and-webhook.md
* docs/payments/MP-PIX-003-webhook-raw-capture-temporary.md
* docs/payments-pix.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/backend-next-actions.md
* docs/ai-context/technical-debt.md

Frontend docs, se o repo frontend estiver disponível no workspace:

* apps/web/docs/admin-orders.md
* apps/web/docs/customer-orders.md
* apps/web/docs/ai-context/api-contracts.md
* apps/web/docs/ai-context/frontend-next-actions.md
* apps/web/docs/ai-context/frontend-technical-debt.md
* apps/web/docs/ai-context/shopflow-frontend-context.md

Verificar:

* endpoints documentados batem com implementação;
* campos sensíveis omitidos estão documentados;
* status do webhook Mercado Pago está documentado como dívida técnica;
* reconciliação Mercado Pago está documentada como fallback operacional;
* raw capture está documentada como temporária;
* Customer Orders documenta que guest não aparece por e-mail;
* Admin Orders documenta que não expõe QR/copia-e-cola/tokens/secrets.

Registrar inconsistências no relatório.

==================================================
4. VALIDAÇÃO DE ENVS
====================

Validar arquivos de exemplo e documentação de envs, como:

* deploy/.env.test.example
* deploy/.env.hml.example
* deploy/.env.prod.example, se existir
* docker-compose*.yml
* GitHub Actions/deploy scripts, se existirem

Checar obrigatórios:

PaymentsPix:

* PaymentsPix__Provider=MercadoPago

Mercado Pago:

* MercadoPago__Enabled=true
* MercadoPago__Environment=Sandbox em teste/HML
* MercadoPago__Environment=Production em produção
* MercadoPago__BaseUrl=https://api.mercadopago.com
* MercadoPago__AccessToken
* MercadoPago__WebhookSecret
* MercadoPago__NotificationUrl
* MercadoPago__ApplicationId
* MercadoPago__UserId
* MercadoPago__PixExpirationMinutes
* MercadoPago__SendNotificationUrlInOrderCreate=false, se essa for a decisão atual
* MercadoPago__SandboxPayerFirstNameOverride=APRO apenas em test/HML, nunca produção

Webhook raw capture:

* MercadoPago__WebhookRawCaptureEnabled=false para entrega/go-live
* Confirmar que não está ativo em Production mesmo se env vier errado

Reconciliação:

* MercadoPagoReconciliation__Enabled=true
* MercadoPagoReconciliation__IntervalSeconds
* MercadoPagoReconciliation__BatchSize
* MercadoPagoReconciliation__MaxAgeMinutes

Guest order:

* GuestOrderAccess__Enabled=true
* GuestOrderAccess__TokenHashSecret
* GuestOrderAccess__TokenTtlDays
* GuestOrderAccess__RateLimitPerMinute

Segurança:

* AllowedOrigins / CORS corretos por ambiente
* Cookie Secure/SameSite coerente com HTTPS/subdomínios
* DataProtection persistido em volume
* ConnectionStrings corretas
* ASPNETCORE_ENVIRONMENT correto
* Admin seed/reset não ativo indevidamente em produção

Registrar:

* envs ausentes;
* envs perigosas;
* envs de sandbox que não podem ir para produção;
* secrets que não devem aparecer em docs/logs.

Não imprimir secrets.

==================================================
5. VALIDAÇÃO DE MIGRATIONS
==========================

Verificar migrations relevantes:

* Orders
* PaymentsPix
* GuestOrderAccessTokens
* CustomerUserId em Orders
* Admin/Identity, se aplicável
* Inventory
* CheckoutSession

Validar:

* migration AddCustomerUserIdToOrders existe;
* campo Order.CustomerUserId nullable existe;
* índice IX_orders_CustomerUserId_CreatedAt existe ou equivalente;
* migrations recentes estão incluídas no projeto;
* não há migration pendente esquecida;
* scripts/estratégia de aplicar migrations em HML/prod está clara.

Se possível, rodar comando de verificação de migrations conforme padrão do projeto.

Registrar no relatório:

* migrations obrigatórias antes do deploy;
* comando recomendado;
* risco se não aplicar.

==================================================
6. VALIDAÇÃO DE ROTAS BACKEND
=============================

Listar e validar, por inspeção/teste, os principais endpoints do MVP:

Public/catalog:

* produtos/lista
* produto detalhe
* SKUs públicos safe

Checkout:

* POST /api/checkout/sessions
* GET /api/checkout/sessions/{id}
* POST /api/checkout/sessions/{id}/cancel

Orders:

* POST /api/orders/from-checkout-session
* GET /api/orders/guest/{orderId}/status

Payments Pix:

* POST /api/payments/pix/orders/{orderId}
* webhook Mercado Pago
* GETs Backoffice-only de Pix, se existirem

Admin:

* POST /api/auth/admin/login
* POST /api/auth/admin/logout
* GET /api/auth/admin/me
* GET /api/auth/csrf
* Admin Catalog
* Admin Inventory
* GET /api/admin/orders
* GET /api/admin/orders/{orderId}

Customer:

* register
* login
* logout
* me
* forgot/reset/confirm, se existirem
* GET /api/customer/orders
* GET /api/customer/orders/{orderId}

Health:

* /health

Validar para cada grupo:

* auth correta;
* policy correta;
* não vaza PII em endpoints públicos;
* GETs admin de orders são Backoffice-only;
* GETs customer são CustomerCookie-only;
* guest status exige token;
* customer não acessa pedido alheio;
* pedido guest não aparece por e-mail.

Registrar lacunas.

==================================================
7. VALIDAÇÃO DE ROTAS FRONTEND
==============================

Se o frontend estiver disponível no workspace, validar:

Públicas:

* /
* /products
* /products/:slug
* /cart
* /checkout
* /checkout/pix/:orderId

Admin:

* /admin/login
* /admin
* /admin/products, se existir
* /admin/inventory, se existir
* /admin/orders
* /admin/orders/:orderId

Customer:

* /login
* /register
* /forgot-password
* /account
* /account/profile
* /account/addresses
* /account/orders
* /account/orders/:id

Validar:

* rotas protegidas por AdminRouteGuard/CustomerRouteGuard;
* 401 redireciona corretamente;
* customer não acessa admin;
* admin não acessa customer como se fosse customer;
* página Pix faz polling seguro;
* Admin Orders não exibe QR/copia-e-cola/tokens/secrets;
* Customer Orders não exibe providerOrderId/providerPaymentId/providerTransactionId.

Registrar no relatório.

==================================================
8. TESTES AUTOMATIZADOS
=======================

Rodar, quando possível:

Backend:

* dotnet build
* dotnet test

Se o projeto for grande e demorado, rodar pelo menos:

* Orders unit tests
* PaymentsPix unit tests
* Worker tests
* HttpApi build

Frontend, se disponível:

* npm/pnpm/yarn typecheck
* build
* Cypress specs principais:

  * admin-orders.cy.ts
  * customer-orders.cy.ts
  * checkout/pix flow, se existir
  * admin-auth, se existir
  * customer-auth, se existir

Comandos sugeridos, adaptar ao projeto:

Backend:
dotnet build
dotnet test

Frontend:
cd apps/web
npm run typecheck
npm run build
npx cypress run --spec cypress/e2e/admin-orders.cy.ts
npx cypress run --spec cypress/e2e/customer-orders.cy.ts

Se algum teste não puder rodar por falta de env/stack:

* registrar como NOT RUN;
* explicar por quê;
* listar comando para rodar no ambiente correto.

Não inventar resultado.

==================================================
9. SMOKE TESTS MANUAIS OBRIGATÓRIOS
===================================

Criar no relatório uma checklist manual para:

A) Compra guest:

1. Abrir loja sem login.
2. Adicionar produto ao carrinho.
3. Fazer checkout convidado.
4. Gerar Pix.
5. Pagar no sandbox.
6. Worker marca Order Paid.
7. Tela Pix mostra aprovado.
8. Admin vê pedido pago.
9. Pedido não aparece em /account/orders só por e-mail.

B) Compra customer logado:

1. Login customer.
2. Adicionar produto.
3. Checkout logado.
4. Gerar Pix.
5. Pagar no sandbox.
6. Worker marca Order Paid.
7. Admin vê pedido.
8. Customer vê pedido em /account/orders.
9. Customer abre detalhe.

C) Admin:

1. Login admin.
2. Abrir /admin/orders.
3. Filtrar por Paid.
4. Abrir detalhe.
5. Conferir cliente, entrega, itens, total e Pix.
6. Confirmar ausência de QR/copia-e-cola/tokens/secrets.

D) Segurança:

1. Sem login não acessa admin orders.
2. Customer não acessa admin orders.
3. Sem login não acessa customer orders.
4. Customer A não acessa pedido de Customer B.
5. Guest token não acessa área customer.

E) Expiração:

1. Pix não pago deve expirar.
2. Pedido pendente deve expirar conforme TTL.
3. Reserva deve ser cancelada.
4. Pedido pago não deve expirar.

==================================================
10. VALIDAÇÃO DO MERCADO PAGO
=============================

Validar documentação e configs:

* Provider MercadoPago ativo.
* Orders API usada, não Payments API legada.
* POST /v1/orders cria Pix.
* GET /v1/orders/{ProviderOrderId} usado na reconciliação.
* Webhook Mercado Pago chega mas assinatura falha em sandbox.
* Reconciliação é fallback operacional do MVP.
* Worker marca Paid apenas com processed/accredited.
* Worker é idempotente.
* Raw capture desligado.
* SendNotificationUrlInOrderCreate=false, se decisão atual mantida.

Classificar o webhook inválido:

* bloquear MVP? provavelmente NÃO, se reconciliação estiver funcionando;
* bloquear produção? avaliar risco;
* registrar recomendação:

  * abrir chamado/suporte Mercado Pago;
  * revisar secret/app;
  * testar em produção controlada com valor mínimo antes de venda real;
  * manter reconciliação ativa.

==================================================
11. VALIDAÇÃO DE SEGURANÇA
==========================

Checar:

* Admin usa cookie HttpOnly.
* Customer usa cookie HttpOnly separado.
* CSRF ativo em mutações.
* GETs sensíveis protegidos por auth.
* CORS allowlist por ambiente.
* DataProtection persistido.
* Secrets não logados.
* WebhookSecret não logado.
* AccessToken não logado.
* GuestAccessToken só retornado uma vez.
* GuestAccessToken armazenado no frontend apenas para status do pedido guest.
* Admin Orders não expõe QR/copia-e-cola/tokens.
* Customer Orders não expõe IDs internos de Mercado Pago.
* Stock reserve/confirm/cancel não é público.
* Endpoints públicos de Orders/Payments não expõem PII.
* Raw capture não roda em Production.

Registrar riscos.

==================================================
12. VALIDAÇÃO DE WORKERS
========================

Validar:

Worker de reconciliação:

* Enabled por env.
* IntervalSeconds configurado.
* BatchSize configurado.
* MaxAgeMinutes configurado.
* Usa AccessToken Mercado Pago.
* Busca somente Pending + MercadoPago + ProviderOrderId.
* processed/accredited => Paid.
* idempotente.
* logs suficientes.
* failures não param batch.

Worker de expiração:

* Enabled por env.
* TTL checkout.
* TTL Pix.
* Cancela reservas expiradas.
* Não expira pedido pago.
* Não conflita com reconciliação.

Registrar:

* comando para ver logs;
* como saber se worker está ativo;
* riscos se worker cair.

==================================================
13. VALIDAÇÃO DE BANCO
======================

Criar no relatório consultas úteis, sem executar se não houver acesso, para validar:

Pix por ProviderOrderId:

* status;
* paidAt;
* providerStatus;
* providerStatusDetail.

Order:

* status;
* paidAt;
* total;
* customerUserId.

Reserva:

* status confirmada/cancelada.

Exemplo de queries deve respeitar nomes reais das tabelas/colunas encontrados no projeto.

==================================================
14. GO-LIVE HML CHECKLIST
=========================

Criar checklist objetiva:

* Migrations aplicadas.
* API sobe.
* Worker sobe.
* Frontend aponta para API correta.
* /health OK.
* Admin login OK.
* Customer login OK.
* Produto demo visível.
* Checkout guest OK.
* Checkout logado OK.
* Pix sandbox OK.
* Worker reconcilia OK.
* Admin Orders OK.
* Customer Orders OK.
* Raw capture OFF.
* Logs sem secrets.
* CORS/cookies OK.
* DataProtection persistido.

==================================================
15. GO-LIVE PRODUÇÃO CHECKLIST
==============================

Criar checklist separada:

* Trocar MercadoPago__Environment=Production.
* Usar AccessToken produção.
* Usar WebhookSecret produção.
* Remover SandboxPayerFirstNameOverride=APRO.
* Configurar URL produção do webhook.
* Confirmar domínio produção.
* Confirmar HTTPS.
* Confirmar AllowedOrigins produção.
* Confirmar Cookie Secure.
* Confirmar backup Postgres.
* Confirmar volumes Docker.
* Confirmar DataProtection persistido.
* Confirmar logs sem secrets.
* Confirmar worker ativo.
* Fazer compra real de baixo valor.
* Conferir Order Paid.
* Conferir Admin Orders.
* Conferir Customer Orders.
* Conferir estoque.
* Definir operação manual de entrega/frete.

==================================================
16. RISCOS E CLASSIFICAÇÃO
==========================

Classificar riscos em:

BLOCKER:

* impede venda ou causa perda de dinheiro/dados.

HIGH:

* não impede MVP, mas pode causar problema operacional sério.

MEDIUM:

* problema contornável manualmente.

LOW:

* melhoria pós-MVP.

Avaliar obrigatoriamente:

* webhook Mercado Pago inválido;
* dependência do worker para marcar Paid;
* worker cair;
* migrations não aplicadas;
* customerUserId não preenchido em checkout logado;
* pedido guest não recuperável sem token;
* ausência de e-mails;
* ausência de frete real;
* ausência de cancelamento/reembolso;
* logs muito verbosos com SQL/PII;
* raw capture temporária;
* admin seed/reset em produção;
* CORS/cookies em subdomínios.

==================================================
17. PLANO DE ROLLBACK
=====================

Criar seção com plano mínimo:

* Reverter frontend para versão anterior.
* Reverter containers API/Worker para imagem anterior.
* Manter banco com migrations forward-only, sem rollback destrutivo automático.
* Desabilitar MercadoPagoReconciliation se causar problema.
* Desabilitar provider MercadoPago e voltar Fake somente em test/HML, nunca produção de venda real.
* Pausar vendas removendo botão/checkout ou colocando manutenção.
* Como identificar pedidos pagos no Mercado Pago manualmente.
* Como reconciliar manualmente com banco, se necessário, apenas com procedimento controlado.

==================================================
18. RESULTADO FINAL
===================

Ao final da execução, retorne no chat:

1. Caminho do relatório criado.
2. Status geral:

   * READY
   * READY WITH RISKS
   * NOT READY
3. Percentual estimado.
4. Testes executados.
5. Testes não executados.
6. Principais blockers, se houver.
7. Principais riscos high/medium.
8. Próximos 5 passos recomendados.
9. Comandos que o usuário deve rodar manualmente, se houver.

Critério de aceite deste prompt:

* Nenhuma feature nova criada.
* Relatório final criado.
* Rotas/envs/migrations/docs/testes/riscos validados.
* Go-live checklist HML e produção documentados.
* Riscos classificados.
* Smoke tests documentados.
* Plano de rollback documentado.
