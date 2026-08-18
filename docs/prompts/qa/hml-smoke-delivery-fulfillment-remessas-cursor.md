Você está atuando como QA/Fullstack engineer sênior do projeto Shopflow, especialista em .NET, React, Cypress, Docker Compose, ambiente de teste, e-commerce, checkout, Pix, Orders, Delivery/Fulfillment, Remessas e Backoffice.

Objetivo:
Executar uma rodada de smoke/regressão no AMBIENTE DE TESTE do Shopflow para validar o fluxo operacional completo após as implementações de Delivery/Fulfillment e Remessas.

IMPORTANTE:
O alvo correto desta validação é o ambiente de TESTE, não HML.

O ambiente alimentado com catálogo, produtos, estoque e dados operacionais é o ambiente de teste.

Portanto:
- não validar api-hml;
- não validar frontend hml;
- não concluir BLOCKED por ausência de dados no HML;
- não redeployar HML neste prompt;
- não comparar HML como fonte principal;
- usar api-teste e frontend de teste como alvo da validação.

Ambiente alvo esperado:
- Frontend teste: domínio/subdomínio de teste configurado no projeto.
- API teste: api-teste.vipassessoriadigital.com.br ou domínio equivalente configurado.
- Containers esperados, adaptar aos nomes reais:
  - shopflow-api-test
  - shopflow-worker-test
  - shopflow-postgres
  - shopflow-caddy

Contexto:
O Shopflow agora possui:

1. Checkout com:
- preferência de método de entrega;
- data preferida com mínimo 2 dias úteis;
- observação do cliente;
- busca CEP via API Shopflow.

2. Orders com:
- OrderStatus separado de FulfillmentStatus;
- Customer/Guest DTO seguro;
- Admin DTO operacional.

3. Admin Orders com:
- filtro fulfillmentStatus;
- marcar pedido individual como enviado;
- marcar pedido individual como entregue;
- observação interna.

4. DeliveryBatch/Remessas com:
- criar remessa agrupada a partir de pedidos pendentes do mesmo cliente;
- listar remessas;
- detalhe da remessa;
- marcar remessa como enviada;
- marcar remessa como entregue;
- nota interna;
- alerta de endereços diferentes.

5. Pix/Mercado Pago:
- fluxo de teste pode depender de webhook ou reconciliation worker.

Objetivo desta rodada:
Validar o MVP operacional de ponta a ponta no ambiente correto de TESTE antes de seguir para novas features como WhatsApp, chat ou frete.

Não implementar nova feature neste prompt.
Não alterar regra de negócio.
Não mexer em Pix, Orders, DeliveryBatch ou Checkout salvo correção pequena e justificada de teste/documentação.
Não mascarar falhas reais como “ambiente”.
Não considerar o ambiente de teste aprovado se o fluxo manual de compra/pedido/remessa não for validado.

==================================================
1. CORREÇÃO DO ALVO DA VALIDAÇÃO
==================================================

O relatório anterior classificou o fluxo como BLOCKED em HML porque:

- HML não tinha rotas novas;
- HML retornava 404 para rotas DeliveryBatch/CEP/Admin Orders;
- CSRF HML retornava 500;
- catálogo HML estava vazio.

Porém, o ambiente que está sendo alimentado e usado para testes é TESTE, não HML.

Neste prompt, corrigir a validação para:

- ambiente de teste;
- API de teste;
- banco de teste;
- worker de teste;
- frontend de teste.

Não usar a ausência de dados em HML como bloqueio do ambiente de teste.

Atualizar o relatório para deixar claro:
- HML não era o alvo correto desta rodada;
- teste é o ambiente validado;
- qualquer conclusão deve se basear no runtime de teste.

==================================================
2. ESCOPO DA VALIDAÇÃO
==================================================

Validar no ambiente de TESTE:

A) Backend teste
- API teste sobe;
- Worker teste sobe;
- Postgres teste acessível;
- migrations aplicadas;
- postal code integration ativa;
- Mercado Pago/Reconciliation configurado;
- endpoints admin protegidos;
- endpoints públicos seguros.

B) Frontend teste
- build publicado ou executável;
- rotas admin;
- checkout;
- customer/guest order tracking;
- delivery UI;
- remessas UI.

C) Cypress/regressão
- specs focadas de checkout, orders, fulfillment e remessas.

D) Smoke manual teste
- simular compra real no ambiente de teste;
- confirmar pedido pago;
- criar remessa;
- marcar enviada/entregue;
- validar cliente/guest.

==================================================
3. CHECKS DE AMBIENTE TESTE
==================================================

Verificar/documentar:

Containers do ambiente teste:

docker compose ps

Logs, adaptar nomes reais:

docker logs --tail=150 shopflow-api-test
docker logs --tail=150 shopflow-worker-test
docker logs --tail=150 shopflow-caddy

Health da API teste:

curl -i https://api-teste.vipassessoriadigital.com.br/health

Se o domínio real for outro, usar o domínio configurado no projeto.

Verificar envs principais da API teste, mascarando segredos:
- ASPNETCORE_ENVIRONMENT
- PaymentsPix__Provider
- MercadoPago__Enabled
- MercadoPago__Environment
- MercadoPago__AccessToken
- MercadoPago__WebhookSecret
- MercadoPago__NotificationUrl
- MercadoPagoReconciliation__Enabled
- GuestOrderAccess__Enabled
- GuestOrderAccess__TokenHashSecret
- PostalCodeLookup__Enabled
- PostalCodeLookup__Provider
- PostalCodeLookup__BaseUrl
- PostalCodeLookup__TimeoutSeconds
- AllowedOrigins/CORS
- DataProtection, se existir

Comando sugerido:

docker exec shopflow-api-test sh -lc 'printenv | sort | grep -E "^(ASPNETCORE_ENVIRONMENT|AllowedOrigins|Cors|PaymentsPix__|MercadoPago__|MercadoPagoReconciliation__|GuestOrderAccess__|PostalCodeLookup__|DataProtection__|Cookie__|Auth__|Csrf__)" | sed -E "s/(AccessToken|WebhookSecret|PublicKey|TokenHashSecret|Password|Secret|Key)=.*/\1=***MASKED***/"'

Nunca imprimir secrets em texto claro.

==================================================
4. VALIDAR ROTAS NOVAS NO AMBIENTE TESTE
==================================================

Validar que a API teste expõe as rotas novas.

Sem autenticação, endpoints admin devem retornar 401/403, não 404:

curl -i https://api-teste.vipassessoriadigital.com.br/api/admin/orders
curl -i https://api-teste.vipassessoriadigital.com.br/api/admin/delivery-batches

Resultado esperado:
- 401/403 se sem cookie admin;
- nunca 404.

CEP:

curl -i https://api-teste.vipassessoriadigital.com.br/api/integrations/postal-code/br/02310000

Resultado esperado:
- 200 found true/false;
- ou 503 controlado se provider estiver indisponível;
- nunca 404.

CSRF:

curl -i https://api-teste.vipassessoriadigital.com.br/api/auth/csrf

Resultado esperado:
- 200;
- Set-Cookie se aplicável;
- body/header com token conforme contrato atual;
- nunca 500.

Se alguma rota der 404 no teste:
- confirmar se a API teste está no commit correto;
- confirmar se Caddy aponta para container de teste correto;
- confirmar se o path /api está roteado corretamente;
- confirmar se endpoints foram registrados no Program/Extensions;
- confirmar se a imagem/container do ambiente teste foi recriado.

Não diagnosticar HML neste prompt.

==================================================
5. MIGRATIONS NO BANCO DE TESTE
==================================================

Confirmar que migrations novas foram aplicadas no banco de TESTE.

Obrigatórias recentes:
- AddOrderDeliveryFulfillment
- AddCheckoutDeliveryPreference
- AddDeliveryBatch
- AddProductDescription
- AddProductStorefrontDisplayOrder
- AddCategorySlug
- AddGuestOrderAccessTokens
- AddCustomerUserIdToOrders
- AddOrderNumberToOrders
- AddPixPaymentOrdersApiFields
- AddSkuSalesRule

A lista exata deve seguir o histórico real do projeto.

Verificar tabela de migrations EF no banco de teste.

Exemplo, adaptar usuário/database reais:

docker exec -it shopflow-postgres psql -U shopflow -d shopflow_test -c "
select * from \"__EFMigrationsHistory\" order by \"MigrationId\" desc limit 30;
"

Ou schema correto, caso migrations history esteja por DbContext/schema.

Verificar tabelas/colunas:

orders.delivery_batches
orders.delivery_batch_orders

orders.orders:
- PreferredDeliveryMethod
- PreferredDeliveryDate
- CustomerOrderNote
- InternalOrderNote
- FulfillmentStatus
- FinalDeliveryMethod
- TrackingCode
- ShippedAt
- DeliveredAt
- FulfillmentUpdatedAt
- FulfillmentUpdatedByAdminId

checkout.checkout_sessions:
- PreferredDeliveryMethod
- PreferredDeliveryDate
- CustomerOrderNote

Se migrations não estiverem aplicadas no TESTE:
- aplicar migrations do ambiente teste pelo script oficial;
- capturar erro se falhar;
- não editar banco manualmente salvo como último recurso e com documentação.

==================================================
6. VALIDAR CATÁLOGO DO AMBIENTE TESTE
==================================================

O catálogo a ser validado é o de TESTE.

Validar que existe massa mínima:

- categoria com slug válido;
- produto ativo;
- SKU ativo;
- estoque disponível;
- preço válido;
- salesRule válida/default;
- produto aparece na vitrine.

Endpoint público:

curl -s "https://api-teste.vipassessoriadigital.com.br/api/catalog/products?page=1&pageSize=16" | jq

Esperado:
- totalItems > 0;
- produto ativo retornado;
- salesSummary coerente.

Se catálogo teste estiver vazio:
- popular catálogo teste;
- não tratar HML vazio como bloqueio deste smoke;
- criar/usar produto mínimo para smoke.

Requisitos do produto smoke:
- ativo;
- com categoria;
- com SKU ativo;
- com estoque físico disponível;
- com preço regular;
- com regra Unit ou salesRule válida.

==================================================
7. VALIDAR WORKER / PIX NO TESTE
==================================================

Confirmar Worker teste:

docker logs --tail=150 shopflow-worker-test

Verificar:
- sem crash;
- reconciliation habilitado;
- logs de pending payments sem erro crítico.

Verificar env:
- PaymentsPix__Provider=MercadoPago
- MercadoPago__Enabled=true
- MercadoPago__Environment=Sandbox
- MercadoPagoReconciliation__Enabled=true
- MercadoPago__AccessToken presente mascarado
- GuestOrderAccess config presente

Não é necessário resolver webhook signature agora se reconciliation funciona, mas documentar.

==================================================
8. TESTES AUTOMATIZADOS FRONTEND
==================================================

Rodar no frontend:

cd apps/web
npm run typecheck
npm run build

Rodar Cypress focado apontando para ambiente local/teste conforme configuração do projeto:

npx cypress run --spec cypress/e2e/checkout-delivery-preferences.cy.ts
npx cypress run --spec cypress/e2e/admin-order-fulfillment.cy.ts
npx cypress run --spec cypress/e2e/admin-orders-list-polish.cy.ts
npx cypress run --spec cypress/e2e/admin-delivery-batches-list.cy.ts
npx cypress run --spec cypress/e2e/admin-delivery-batch-detail.cy.ts
npx cypress run --spec cypress/e2e/admin-create-delivery-batch-from-order.cy.ts

Rodar também, se ambiente permitir:

npx cypress run --spec cypress/e2e/checkout-csrf-pix-flow.cy.ts
npx cypress run --spec cypress/e2e/customer-orders.cy.ts
npx cypress run --spec cypress/e2e/order-sales-display.cy.ts

Se precisar credenciais:
ADMIN_EMAIL=...
ADMIN_PASSWORD=...

Documentar:
- specs passadas;
- specs falhadas;
- flakes/retries;
- se algo não rodou por ambiente.

==================================================
9. TESTES BACKEND
==================================================

Rodar testes focados:

dotnet build

Testes sugeridos:
- Orders
- CartCheckout
- Shipping
- PaymentsPix, se rápidos/estáveis
- Inventory, se afetado pelo smoke

Registrar comandos reais executados.

Se suíte ampla falhar por limitação de ambiente, documentar separando:
- falha funcional;
- falha de ambiente;
- evidência alternativa.

Importante:
Não classificar falha como ambiente sem evidência clara.

==================================================
10. SMOKE MANUAL TESTE — COMPRA INDIVIDUAL
==================================================

Executar fluxo manual no ambiente de TESTE:

1. Admin cria ou usa produto ativo:
- produto ativo;
- categoria correta;
- SKU ativo;
- estoque disponível;
- preço válido;
- salesRule válida, se houver.

2. Cliente/convidado acessa vitrine de teste:
- produto aparece;
- categoria/sort/paginação funcionam;
- ProductCard não quebra.

3. Cliente adiciona ao carrinho.

4. Checkout:
- endereço com CEP;
- busca CEP via API Shopflow teste;
- método de entrega preferido;
- data preferida válida;
- observação do cliente;
- criar sessão.

5. Pix:
- criar pedido;
- gerar Pix;
- confirmar pagamento via sandbox/reconciliation/webhook do ambiente teste;
- pedido muda para Paid.

6. Pós-compra:
- página de Pix/confirmado mostra pedido;
- guest tracking funciona com token;
- se customer logado, pedido aparece em Meus pedidos.

7. Admin:
- pedido aparece em /admin/orders;
- status pagamento/pedido correto;
- fulfillmentStatus = AwaitingShipment;
- dados de entrega aparecem;
- observação do cliente aparece;
- internalOrderNote só no admin.

8. Admin marca pedido individual como enviado:
- método final;
- tracking/reference;
- nota interna;
- pedido vira Shipped.

9. Admin marca pedido individual como entregue:
- pedido vira Delivered.

10. Cliente/guest:
- vê status de entrega;
- vê tracking/reference se houver;
- não vê internalOrderNote.

Resultado esperado:
- fluxo individual aprovado no ambiente de teste.

==================================================
11. SMOKE MANUAL TESTE — REMESSA AGRUPADA
==================================================

Executar fluxo manual no ambiente de TESTE:

1. Criar dois pedidos pagos do mesmo cliente:
- mesmo customerUserId, se logado;
ou
- mesmo e-mail + telefone normalizados, se guest.

2. Ambos devem estar:
- OrderStatus = Paid;
- FulfillmentStatus = AwaitingShipment;
- não vinculados a remessa.

3. Admin abre um dos pedidos.

4. Card de remessa:
- buscar candidates;
- pedidos elegíveis aparecem;
- pedido base vem selecionado;
- outro pedido do mesmo cliente aparece;
- pedidos de outro cliente não aparecem.

5. Criar remessa:
- selecionar 2 pedidos;
- método de entrega;
- tracking/reference opcional;
- nota interna opcional;
- se endereços diferentes, exigir confirmação;
- criar.

6. Após criar:
- navegar para /admin/delivery-batches/:batchId;
- exibir Remessa #30000+;
- pedidos aparecem na remessa;
- pedidos individuais mostram link para Remessa.

7. Marcar remessa como enviada:
- POST ship;
- remessa vira Shipped;
- todos os pedidos vinculados viram Shipped;
- tracking/método aparecem.

8. Marcar remessa como entregue:
- POST deliver;
- remessa vira Delivered;
- todos os pedidos vinculados viram Delivered.

9. Cliente/guest:
- cada pedido mostra status seguro da entrega;
- não mostra batch interna;
- não mostra internalNote.

Resultado esperado:
- fluxo de remessa aprovado no ambiente de teste.

==================================================
12. VALIDAÇÕES DE SEGURANÇA NO TESTE
==================================================

Validar:

1. Customer/guest não recebe:
- internalOrderNote;
- fulfillmentUpdatedByAdminId;
- dados internos da remessa;
- deliveryBatchId/deliveryBatchNumber, se a decisão foi não expor.

2. Admin endpoints exigem Backoffice:
- delivery-batches;
- candidates;
- ship/deliver;
- internal-note.

3. Mutations admin exigem CSRF.

4. Guest order tracking exige token.

5. Admin não usa endpoints públicos para dados operacionais.

Documentar evidências.

==================================================
13. VALIDAÇÕES DE CEP NO TESTE
==================================================

Validar:

1. Frontend não chama ViaCEP direto.
2. Frontend chama:
GET /api/integrations/postal-code/br/{cep}

3. CEP válido preenche:
- rua;
- bairro;
- cidade;
- UF.

4. CEP inválido:
- não bloqueia checkout;
- permite preenchimento manual.

5. Provider indisponível:
- mostra fallback amigável;
- permite preenchimento manual.

6. Backend:
- CEP inválido não chama provider;
- 503 controlado se provider falhar.

Endpoint alvo:

https://api-teste.vipassessoriadigital.com.br/api/integrations/postal-code/br/{cep}

==================================================
14. RELATÓRIO FINAL
==================================================

Criar ou atualizar documento:

docs/qa/TESTE-DELIVERY-FULFILLMENT-REMESSAS-SMOKE-REPORT.md

Se o relatório anterior HML existir, adicionar nota nele ou substituir a conclusão dizendo:
- aquele relatório mirava HML por engano;
- o ambiente correto desta rodada é TESTE;
- a decisão final deve estar no relatório TESTE.

Conteúdo obrigatório do novo relatório:

1. Data/hora da validação.
2. Ambiente validado: TESTE.
3. Domínio frontend teste.
4. Domínio API teste.
5. Branch/commit backend.
6. Branch/commit frontend.
7. Containers/imagens validadas.
8. Migrations aplicadas.
9. Resultado das rotas:
   - admin/orders
   - admin/delivery-batches
   - postal-code
   - auth/csrf
10. Resultado do build backend.
11. Resultado dos testes backend.
12. Resultado do typecheck/build frontend.
13. Resultado Cypress.
14. Resultado catálogo teste.
15. Resultado worker/reconciliation.
16. Resultado smoke individual.
17. Resultado smoke remessa.
18. Resultado validação segurança.
19. Resultado validação CEP.
20. Bugs encontrados.
21. Classificação dos bugs:
   - blocker
   - high
   - medium
   - low
22. Decisão:
   - APPROVED
   - APPROVED WITH RISKS
   - BLOCKED
23. Riscos conhecidos.
24. Próximos passos recomendados.

==================================================
15. CRITÉRIOS DE APROVAÇÃO DO AMBIENTE TESTE
==================================================

Aprovar o ambiente de TESTE apenas se:

- API teste expõe as rotas novas;
- CSRF teste funciona;
- catálogo teste tem produto comprável;
- checkout cria pedido com entrega;
- Pix/pagamento confirma pedido;
- admin vê pedido pago;
- admin marca enviado/entregue individualmente;
- dois pedidos do mesmo cliente podem virar remessa;
- remessa enviada atualiza todos os pedidos;
- remessa entregue atualiza todos os pedidos;
- cliente/guest não vê dados internos;
- CEP funciona via API Shopflow teste;
- não há erro crítico em API/worker;
- Cypress focado passa ou falhas são justificadas e não funcionais.

Classificar como APPROVED WITH RISKS se:
- fluxo principal funciona;
- existem falhas de ambiente/teste documentadas;
- existe algum ajuste médio/baixo não bloqueante.

Classificar como BLOCKED se:
- compra não finaliza;
- Pix não confirma;
- pedido não fica Paid;
- remessa não cria;
- ship/deliver não atualiza pedidos;
- internalNote vaza para cliente/guest;
- admin endpoints aceitam acesso indevido;
- API teste não expõe rotas novas;
- CSRF teste retorna 500;
- catálogo teste não permite compra.

==================================================
16. NÃO FAZER
==================================================

Não implementar:
- WhatsApp;
- chat;
- frete;
- rastreio automático;
- cancelamento de remessa;
- desvincular pedido;
- status avançados;
- feriados.

Não alterar regra de negócio.
Não alterar migrations sem necessidade.
Não fazer refactor amplo.
Não ignorar falha crítica.

Não validar HML neste prompt.
Não redeployar HML neste prompt.
Não usar HML vazio como bloqueio do teste.
Não marcar TESTE aprovado sem smoke manual.

==================================================
17. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Ambiente validado.
2. Base URL frontend teste.
3. Base URL API teste.
4. Comandos executados.
5. Containers/imagens validadas.
6. Migrations verificadas.
7. Rotas validadas e códigos HTTP.
8. Catálogo teste validado.
9. Worker teste validado.
10. Testes que passaram.
11. Testes que falharam.
12. Smoke individual: PASS/FAIL.
13. Smoke remessa: PASS/FAIL.
14. Segurança: PASS/FAIL.
15. CEP: PASS/FAIL.
16. Arquivo de relatório criado.
17. Bugs encontrados.
18. Classificação final:
   - APPROVED
   - APPROVED WITH RISKS
   - BLOCKED
19. Próximo passo recomendado.