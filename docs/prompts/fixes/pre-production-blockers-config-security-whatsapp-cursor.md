Você está atuando como DevOps/Fullstack engineer sênior do projeto Shopflow, especialista em segurança pré-produção, Docker Compose, variáveis de ambiente, Cloudflare Pages, Caddy, .NET, React/Vite, Mercado Pago, CSRF, DataProtection e checklist de go-live.

Objetivo:
Resolver os blockers identificados na validação pré-produção do Shopflow.

Relatório base:
docs/qa/PRE-PRODUCTION-GO-LIVE-RESULT.md

Decisão atual:
- TESTE operacional: APPROVED WITH RISKS
- Produção: BLOCKED

Problemas principais:
1. MercadoPago__WebhookRawCaptureEnabled=true
2. SHOPFLOW_ADMIN_RESET_PASSWORD=true
3. Production não validada
4. WhatsApp Pages/env ainda não smokeado em UI com número real
5. .env example/ambientes ainda podem conter placeholder confuso
6. Senha admin TESTE precisa rotação
7. Checklist final está truncado em docs/prompts/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md
8. Cypress focado não rodado nesta rodada
9. DataProtection/logs não validados
10. MP Production/live ainda não exercitado

Não implementar nova feature.
Não alterar regra de negócio.
Não alterar Delivery/Fulfillment/Remessas.
Não alterar Pix flow, exceto configuração segura.
Não remover CSRF.
Não mascarar falhas críticas.

==================================================
1. ESCOPO DESTE PROMPT
==================================================

Corrigir/validar:

A) Flags perigosas
- Desligar raw capture de webhook.
- Desligar reset password admin.
- Garantir que produção nunca suba com essas flags ligadas.

B) Admin security
- Rotacionar senha admin do ambiente TESTE.
- Documentar procedimento.
- Confirmar que reset password admin está desabilitado.

C) WhatsApp
- Configurar número real no ambiente de teste/Pages.
- Garantir que placeholder não seja usado em ambientes reais.
- Smoke UI do WhatsApp.

D) Checklist
- Corrigir arquivo truncado.
- Garantir checklist completo em docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md.
- Se existir cópia errada em docs/prompts/qa, ajustar/remover/explicar.

E) Validação curta
- Cypress focado.
- CSRF.
- DataProtection/logs.
- Rotas principais.
- Sem secrets nos logs.

F) Atualizar relatório
- Atualizar PRE-PRODUCTION-GO-LIVE-RESULT.md com reexecução.

==================================================
2. FLAGS PERIGOSAS
==================================================

Auditar arquivos de ambiente e deploy:

- deploy/.env.test
- deploy/.env.hml.example
- deploy/.env.prod.example
- docker-compose*.yml
- GitHub Actions / secrets docs, se houver
- Cloudflare Pages env docs, se houver
- appsettings.Staging.json
- appsettings.Production.json
- README/deploy docs

Procurar:

MercadoPago__WebhookRawCaptureEnabled
SHOPFLOW_ADMIN_RESET_PASSWORD

Regras obrigatórias:

1. Em TESTE:
- MercadoPago__WebhookRawCaptureEnabled=false
- SHOPFLOW_ADMIN_RESET_PASSWORD=false

2. Em HML:
- MercadoPago__WebhookRawCaptureEnabled=false
- SHOPFLOW_ADMIN_RESET_PASSWORD=false

3. Em Produção:
- MercadoPago__WebhookRawCaptureEnabled=false
- SHOPFLOW_ADMIN_RESET_PASSWORD=false

4. Se precisar manter documentação de diagnóstico:
- deixar explicitamente comentado;
- nunca como true;
- escrever que só pode ser ligado temporariamente em ambiente controlado e desligado antes de qualquer smoke/go-live.

Critério:
- nenhum ambiente real deve ficar com essas flags true.
- se o código tiver guard contra Production, verificar e documentar.
- se não tiver guard, documentar como risco ou implementar proteção mínima se já houver padrão seguro.

==================================================
3. VALIDAR RUNTIME TESTE APÓS CONFIG
==================================================

Depois de alterar env:

- recriar API/Worker teste;
- validar containers;
- validar logs.

Comandos sugeridos, adaptar aos nomes reais:

docker compose up -d --force-recreate api-test worker-test
docker compose ps
docker logs --tail=150 shopflow-api-test
docker logs --tail=150 shopflow-worker-test

Inspecionar env mascarado:

docker exec shopflow-api-test sh -lc 'printenv | sort | grep -E "^(MercadoPago__WebhookRawCaptureEnabled|SHOPFLOW_ADMIN_RESET_PASSWORD|ASPNETCORE_ENVIRONMENT|DataProtection__|AllowedOrigins|Cors|PostalCodeLookup__|MercadoPago__|GuestOrderAccess__)" | sed -E "s/(AccessToken|WebhookSecret|PublicKey|TokenHashSecret|Password|Secret|Key)=.*/\1=***MASKED***/"'

Validar:
- raw capture false;
- reset password false;
- API sobe;
- worker sobe;
- logs sem erro crítico.

==================================================
4. ROTAÇÃO DA SENHA ADMIN TESTE
==================================================

Rotacionar senha admin do ambiente TESTE.

Regras:
- não registrar senha em docs;
- não commitar senha;
- usar mecanismo seguro existente;
- se houver script/endpoint temporário de reset, garantir que a flag esteja desligada depois;
- validar login admin após rotação;
- registrar no relatório apenas que foi rotacionada, sem valor.

Critério:
- admin login funciona;
- SHOPFLOW_ADMIN_RESET_PASSWORD=false;
- nenhuma senha em logs/docs.

==================================================
5. WHATSAPP PAGES / ENV REAL
==================================================

Auditar envs frontend:

- .env.example
- .env.development
- .env.test.example
- .env.hml.example
- docs de deploy Cloudflare Pages
- variáveis reais no Pages, se acessível/documentado

Regras:

1. `.env.example`:
- pode manter placeholder, mas deve ficar claramente não-real.
- recomendado:
  VITE_SUPPORT_WHATSAPP_ENABLED=false
  VITE_SUPPORT_WHATSAPP_PHONE=55DDDNUMERO

2. `.env.development`:
- pode usar telefone fake apenas local, mas preferir disabled por padrão.

3. Ambiente TESTE/Pages:
- usar número real validado pelo cliente.
- VITE_SUPPORT_WHATSAPP_ENABLED=true
- VITE_SUPPORT_WHATSAPP_PHONE=<numero real só dígitos>

4. Produção:
- usar número real final.
- não usar placeholder.

Importante:
- Como VITE_* entra no build, qualquer alteração exige rebuild/redeploy do frontend.

Validação UI:
- abrir PDP;
- abrir checkout;
- abrir pós-pix/pedido se possível;
- abrir guest tracking/account order detail, se possível;
- clicar CTA;
- confirmar wa.me com número real;
- confirmar mensagem com Pedido # quando aplicável;
- confirmar que link não contém token/GUID/internalNote/dados sensíveis.

==================================================
6. CHECKLIST TRUNCADO
==================================================

Problema:
docs/prompts/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md está truncado em aproximadamente 105 linhas.

Corrigir:

1. Criar/atualizar checklist completo no local correto:
docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md

2. Se existir checklist em docs/prompts/qa:
- manter apenas se for prompt;
- não usar como checklist oficial;
- corrigir referência nos docs.

3. Atualizar PRE-PRODUCTION-GO-LIVE-RESULT.md para apontar para:
docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md

4. Garantir que o checklist contenha, no mínimo:
- código/branches;
- envs backend/frontend;
- flags perigosas;
- migrations;
- infra/DNS/Caddy/SSL;
- CSRF/DataProtection;
- Mercado Pago;
- CEP;
- WhatsApp;
- catálogo/estoque;
- checkout/Pix;
- Orders;
- Delivery/Fulfillment;
- Remessas;
- segurança customer/guest/admin;
- Cypress;
- smoke manual;
- logs/monitoramento;
- rollback;
- critérios APPROVED / APPROVED WITH RISKS / BLOCKED;
- pendências pós-go-live.

Critério:
- checklist oficial não truncado.
- relatório aponta para o arquivo correto.

==================================================
7. CYPRESS FOCADO
==================================================

Rodar, se ambiente permitir:

cd apps/web

npm run typecheck
npm run build

npx cypress run --spec cypress/e2e/checkout-delivery-preferences.cy.ts
npx cypress run --spec cypress/e2e/checkout-csrf-pix-flow.cy.ts
npx cypress run --spec cypress/e2e/customer-orders.cy.ts
npx cypress run --spec cypress/e2e/admin-orders-list-polish.cy.ts
npx cypress run --spec cypress/e2e/admin-order-fulfillment.cy.ts
npx cypress run --spec cypress/e2e/admin-delivery-batches-list.cy.ts
npx cypress run --spec cypress/e2e/admin-delivery-batch-detail.cy.ts
npx cypress run --spec cypress/e2e/admin-create-delivery-batch-from-order.cy.ts
npx cypress run --spec cypress/e2e/whatsapp-contact.cy.ts

Documentar:
- passou;
- falhou;
- not run;
- flakes/retry;
- motivo de falha se existir.

Não bloquear por Cypress não executado se houver limitação clara, mas documentar como risco.

==================================================
8. DATAPROTECTION / LOGS
==================================================

Validar:

1. DataProtection:
- path configurado;
- volume persistido;
- API consegue ler/escrever;
- não há erro de key ring;
- reiniciar API não invalida login de forma inesperada, se possível testar.

2. Logs:
- API sem erro crítico;
- Worker sem erro crítico;
- Caddy sem erro crítico;
- nenhum secret impresso;
- raw webhook capture desligado;
- reset password desligado.

Comandos sugeridos:

docker logs --tail=300 shopflow-api-test
docker logs --tail=300 shopflow-worker-test
docker logs --tail=300 shopflow-caddy

==================================================
9. ROTAS MÍNIMAS
==================================================

Validar no ambiente TESTE:

curl -i https://api-teste.vipassessoriadigital.com.br/health
curl -i https://api-teste.vipassessoriadigital.com.br/api/auth/csrf
curl -i https://api-teste.vipassessoriadigital.com.br/api/admin/orders
curl -i https://api-teste.vipassessoriadigital.com.br/api/admin/delivery-batches
curl -i https://api-teste.vipassessoriadigital.com.br/api/integrations/postal-code/br/02310000
curl -i "https://api-teste.vipassessoriadigital.com.br/api/catalog/products?page=1&pageSize=16"

Esperado:
- health 200;
- csrf 200;
- admin sem auth 401/403, não 404/500;
- postal-code 200/503 controlado, não 404;
- catalog 200 com itens.

==================================================
10. PRODUÇÃO
==================================================

Não validar produção de ponta a ponta neste prompt, salvo se explicitamente solicitado.

Preparar lista de pendências para produção:

- env produção completa;
- DataProtection produção;
- CORS domínio produção;
- MP production/live;
- NotificationUrl produção;
- Worker produção;
- WhatsApp produção;
- domínio/SSL;
- backup banco;
- rollback drill;
- smoke produção controlado.

Produção continua BLOCKED até esses itens serem executados.

==================================================
11. ATUALIZAR RELATÓRIO
==================================================

Atualizar:

docs/qa/PRE-PRODUCTION-GO-LIVE-RESULT.md

Adicionar seção:
"Reexecução pós-correção de blockers"

Conteúdo:
1. Data/hora.
2. Ambiente: TESTE.
3. Commits/branches.
4. Flags corrigidas.
5. Senha admin rotacionada: sim/não.
6. WhatsApp env/Pages validado: sim/não.
7. Checklist corrigido: sim/não.
8. Cypress: resultados.
9. DataProtection/logs: resultados.
10. Rotas mínimas: códigos HTTP.
11. Bugs remanescentes.
12. Riscos remanescentes.
13. Decisão atualizada:
   - TESTE: APPROVED / APPROVED WITH RISKS / BLOCKED
   - Produção: APPROVED / APPROVED WITH RISKS / BLOCKED

Critério esperado após esta correção:
- TESTE pode continuar APPROVED WITH RISKS ou subir para APPROVED se tudo passar.
- Produção provavelmente continua BLOCKED até MP live/produção/smoke real serem feitos.

==================================================
12. NÃO FAZER
==================================================

Não implementar:
- WhatsApp Business API;
- chat;
- frete;
- rastreio;
- cancelamento de remessa;
- desvincular pedido;
- status avançados;
- feriados.

Não desabilitar:
- CSRF;
- auth admin;
- rate limits.

Não commitar:
- secrets;
- senha admin;
- access token Mercado Pago;
- webhook secret;
- número privado se o projeto decidir tratar env real fora do repo.

==================================================
13. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Flags corrigidas.
3. Evidência de runtime com flags false.
4. Senha admin TESTE rotacionada ou motivo se não foi.
5. WhatsApp Pages/UI validado ou motivo se não foi.
6. Checklist completo corrigido.
7. Cypress executado e resultados.
8. DataProtection/logs validados.
9. Rotas mínimas validadas.
10. Relatório atualizado.
11. Decisão final para TESTE.
12. Decisão final para produção.
13. Próximos passos para liberar produção.

Critérios de aceite:
- raw capture false;
- reset password false;
- senha admin TESTE rotacionada;
- WhatsApp real configurado/validado no ambiente de teste;
- checklist não truncado;
- relatório atualizado;
- produção não marcada como aprovada sem validação real.