Você está atuando como engenheiro backend/DevOps sênior do projeto Shopflow, especialista em .NET, EF Core, Cloudflare R2, S3-compatible storage, scripts seguros de backfill, Docker Compose, ambientes TESTE/HML/PROD e migração controlada de arquivos.

Objetivo:
Preparar uma carga controlada para subir imagens de produto que hoje estão em pasta local para o bucket Cloudflare R2 APENAS no ambiente de TESTE.

Contexto:
O Shopflow já possui estrutura de object storage para imagens de produto:

- IObjectStorageService
- ProductImageStorageService
- R2ObjectStorageService
- LocalObjectStorageService
- ProductImage.StorageProvider
- ProductImage.StoragePath
- URL pública montada por PublicBaseUrl
- Dev/local pode continuar usando disco local
- Produção usará R2 para novas imagens

Problema:
Antes da migração para R2, algumas imagens de produto foram salvas em uma pasta local. Queremos fazer uma carga dessas imagens para o bucket R2 do ambiente de TESTE, para validar o fluxo real de imagens no ambiente de teste.

Decisão crítica:
NÃO subir nenhuma carga automática de imagens para produção.
NÃO rodar backfill em produção.
NÃO incluir backfill em migration EF.
NÃO incluir backfill automático no startup da API.
NÃO rodar backfill em deploy padrão.
NÃO rodar backfill no worker automaticamente.

O backfill deve ser:
- manual;
- explícito;
- idempotente;
- com dry-run;
- protegido contra Production;
- voltado para TESTE.

==================================================
1. ESCOPO
==================================================

Implementar ou preparar:

1. Um comando/script manual para backfill de imagens locais para R2 no ambiente de TESTE.
2. Validação forte para impedir execução em produção.
3. Dry-run obrigatório/recomendado antes do run real.
4. Relatório do que seria/enviado.
5. Upload para R2 apenas de imagens ainda locais.
6. Atualização do banco apenas após upload bem-sucedido.
7. Documentação de execução no TESTE.
8. Nenhuma alteração automática em produção.

Não implementar:
- carga em produção;
- migração automática de imagens antigas em produção;
- upload direto do frontend;
- transformação de imagem;
- thumbnails;
- limpeza agressiva de arquivos locais;
- alteração no fluxo normal de upload.

==================================================
2. AUDITORIA INICIAL
==================================================

Auditar a implementação atual:

Backend:
- ProductImage entity/model
- EF mapping de ProductImage
- ProductImageStorageService
- R2ObjectStorageService
- LocalObjectStorageService
- handlers de upload/delete imagem
- options R2Storage / Uploads
- docs Cloudflare R2

Banco:
- colunas de imagem:
  - StorageProvider
  - StoragePath
  - Url/PublicUrl, se existir
  - FileName/ContentType/Size, se existir
  - ProductId
  - IsPrimary
  - DisplayOrder

Arquivos locais:
- identificar pasta local atual de uploads.
- exemplos possíveis:
  - /opt/shopflow/app/uploads
  - /app/uploads
  - wwwroot/uploads
  - caminho configurado por Uploads__LocalRoot ou equivalente.

Objetivo da auditoria:
- entender como encontrar o arquivo físico local de cada ProductImage.
- entender como detectar imagem já migrada para R2.
- entender como atualizar StorageProvider/StoragePath sem quebrar URLs públicas.

==================================================
3. ESTRATÉGIA DE BACKFILL
==================================================

Criar uma ferramenta manual seguindo o padrão do projeto.

Preferências, nesta ordem:

A) Se já existir projeto de tools/maintenance:
- adicionar comando nele.

B) Se não existir:
- criar projeto console em:
  tools/Vls.Shopflow.Tools
  ou nome equivalente ao padrão do repo.

C) Se o projeto preferir scripts:
- criar script seguro em deploy/scripts/backfill-product-images-r2-test.sh
- esse script pode chamar o console/tool.

Recomendação:
Criar tool .NET para usar:
- DI;
- DbContext;
- IObjectStorageService;
- ProductImageStorageService;
- configurações reais;
- logging;
- validação de ambiente.

Comando sugerido:

dotnet run --project tools/Vls.Shopflow.Tools -- \
  product-images backfill-r2 \
  --environment Test \
  --source-root /opt/shopflow/app/uploads \
  --dry-run

Run real:

dotnet run --project tools/Vls.Shopflow.Tools -- \
  product-images backfill-r2 \
  --environment Test \
  --source-root /opt/shopflow/app/uploads \
  --execute

Os nomes podem ser ajustados ao padrão real do repo, mas deve haver:
- dry-run;
- execute explícito;
- target environment explícito;
- source-root explícito ou configurado.

==================================================
4. GUARDS OBRIGATÓRIOS
==================================================

A ferramenta deve abortar se qualquer condição insegura ocorrer.

Obrigatório abortar quando:

1. ASPNETCORE_ENVIRONMENT=Production
2. DOTNET_ENVIRONMENT=Production
3. --environment Production
4. Connection string aparentar banco de produção
5. R2Storage__Enabled != true
6. R2Storage__Provider != CloudflareR2
7. R2Storage__BucketName vazio
8. R2Storage__PublicBaseUrl vazio
9. SourceRoot inexistente
10. Não houver flag explícita de execução

Adicionar flag de segurança:

ProductImageBackfill__Enabled=true

ou:

R2ImageBackfill__Enabled=true

Regras:
- default false;
- em produção sempre false;
- no teste pode true temporariamente;
- ferramenta exige essa flag true para rodar execute;
- dry-run pode rodar sem alterar nada, mas ainda deve abortar em Production.

Também exigir confirmação textual no execute:

--confirm TESTE_R2_IMAGE_BACKFILL

Sem essa confirmação:
- abortar.

Critério:
- deve ser muito difícil rodar isso por acidente.

==================================================
5. SELEÇÃO DE IMAGENS
==================================================

Selecionar apenas imagens elegíveis:

Elegíveis:
- ProductImage com StorageProvider null ou Local;
- StoragePath/Url apontando para arquivo local;
- arquivo existe no SourceRoot;
- tipo permitido;
- produto ainda existe.

Ignorar:
- StorageProvider = CloudflareR2;
- StoragePath já parecendo object key R2 válido, se provider R2;
- imagem sem arquivo local correspondente;
- imagem com arquivo inexistente;
- imagem com MIME inválido;
- imagem deletada/inconsistente.

Para cada imagem ignorada, registrar no relatório:
- imageId;
- productId;
- motivo.

==================================================
6. MAPEAMENTO LOCAL → R2
==================================================

Para cada imagem local elegível:

1. Localizar arquivo físico.
2. Validar extensão/contentType.
3. Gerar object key segura.

Key recomendada:

products/{productId}/{imageId}.{ext}

ou usar exatamente o padrão novo do ProductImageStorageService.

Importante:
- reaproveitar helper existente de key, se houver.
- não inventar formato diferente do upload novo.
- não usar nome original como key principal.
- não sobrescrever objeto existente sem decisão explícita.

Se objeto já existir no R2:
- comparar se possível;
- considerar idempotente;
- atualizar DB se fizer sentido;
- registrar como already-exists.

==================================================
7. UPLOAD E ATUALIZAÇÃO DO BANCO
==================================================

Para cada imagem:

Fluxo seguro:
1. Upload para R2.
2. Se upload OK:
   - atualizar ProductImage.StorageProvider = CloudflareR2
   - atualizar ProductImage.StoragePath = object key
   - atualizar URL/public url se houver coluna persistida.
3. Salvar DB.
4. Registrar sucesso.

Se upload falhar:
- não atualizar DB;
- registrar erro;
- continuar com próximas imagens, salvo se for erro crítico de config.

Transação:
- não precisa uma transação global para todas as imagens.
- usar transação por imagem ou salvar por item/lote pequeno.
- evitar deixar DB apontando para R2 sem objeto enviado.

Não apagar arquivo local automaticamente nesta fase.
A limpeza de arquivos locais deve ficar como etapa manual futura após validação.

==================================================
8. DRY-RUN
==================================================

Dry-run deve mostrar:

- ambiente detectado;
- bucket;
- public base url;
- source root;
- total de imagens no banco;
- total elegível;
- total já R2;
- total sem arquivo;
- total inválido;
- lista resumida do que seria enviado;
- object key planejada;
- nenhuma alteração no banco;
- nenhum upload real.

Dry-run não deve modificar nada.

==================================================
9. RELATÓRIO
==================================================

Gerar relatório em arquivo, por exemplo:

docs/qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md

Ou em:

artifacts/r2-backfill/report-YYYYMMDD-HHMM.md

Conteúdo:
- data/hora;
- ambiente;
- source root;
- bucket;
- public base url;
- modo: dry-run ou execute;
- total analisado;
- total enviado;
- total já migrado;
- total ignorado;
- total erro;
- erros por imagem;
- próximos passos.

Não incluir secrets.
Não incluir access key.
Não incluir secret key.

==================================================
10. SCRIPT DE EXECUÇÃO NO AMBIENTE TESTE
==================================================

Criar script opcional:

deploy/scripts/backfill-product-images-r2-test.sh

Regras:
- script aponta para ambiente TESTE.
- aborta se ENV não for Test/Staging de teste.
- não roda em produção.
- não usa banco produção.
- exige confirmação.

Exemplo:

./deploy/scripts/backfill-product-images-r2-test.sh --dry-run
./deploy/scripts/backfill-product-images-r2-test.sh --execute --confirm TESTE_R2_IMAGE_BACKFILL

O script deve:
- carregar env teste;
- rodar a tool;
- salvar log;
- retornar exit code != 0 em falha crítica.

Não criar script para produção.

==================================================
11. CONFIGURAÇÃO TESTE
==================================================

Documentar envs necessárias no ambiente TESTE:

R2Storage__Enabled=true
R2Storage__Provider=CloudflareR2
R2Storage__AccountId=...
R2Storage__BucketName=...
R2Storage__AccessKeyId=...
R2Storage__SecretAccessKey=...
R2Storage__ServiceUrl=https://<account-id>.r2.cloudflarestorage.com
R2Storage__PublicBaseUrl=https://imagens-teste.seudominio.com.br
R2Storage__ProductImagesPrefix=products

R2ImageBackfill__Enabled=true

Uploads__LocalRoot=/opt/shopflow/app/uploads

Ou nomes equivalentes reais.

Produção:
- R2Storage__Enabled=true para uploads novos;
- R2ImageBackfill__Enabled=false;
- nenhum script de backfill produção.

==================================================
12. TESTES
==================================================

Criar testes unitários para a lógica de backfill:

1. aborta em Production.
2. aborta sem flag Enabled.
3. aborta sem execute/confirm.
4. dry-run não chama upload.
5. dry-run não altera DB.
6. ignora imagem já R2.
7. seleciona imagem Local/null.
8. ignora arquivo inexistente.
9. gera key no padrão esperado.
10. upload bem-sucedido atualiza provider/path.
11. upload com erro não atualiza DB.
12. não apaga arquivo local.
13. relatório não contém secrets.

Se criar script shell:
- revisar shellcheck se disponível;
- pelo menos documentar execução manual.

==================================================
13. VALIDAÇÃO EM TESTE
==================================================

Após implementar, executar em TESTE:

1. Configurar bucket R2 teste.
2. Configurar PublicBaseUrl teste.
3. Garantir que arquivos locais existem.
4. Rodar dry-run.
5. Validar relatório dry-run.
6. Rodar execute com confirmação.
7. Validar relatório execute.
8. Conferir banco:
   - imagens migradas com StorageProvider=CloudflareR2;
   - StoragePath como object key.
9. Conferir bucket:
   - objetos enviados.
10. Conferir vitrine:
   - imagens carregam pelo domínio público R2.
11. Conferir admin edit:
   - preview funciona.
12. Fazer novo upload:
   - vai direto para R2.
13. Delete:
   - remove metadata e tenta apagar objeto.

Cypress recomendado:
- admin-product-images-r2.cy.ts

Se não conseguir executar Cypress:
- documentar NOT RUN.

==================================================
14. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/integrations/cloudflare-r2-product-images.md
- docs/catalog/product-images.md
- docs/qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md
- docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar claramente:

- backfill é apenas TESTE;
- produção não deve receber carga automática;
- uploads novos em produção usam R2 normalmente;
- imagens antigas de produção, se existirem, exigem decisão manual futura;
- como rodar dry-run;
- como rodar execute;
- como validar;
- como reverter em teste, se necessário.

==================================================
15. NÃO FAZER
==================================================

Não fazer:

- backfill automático em Production.
- script de produção.
- migration EF que envie arquivos.
- startup task que envie arquivos.
- worker automático que envie arquivos antigos.
- apagar arquivos locais após upload.
- subir secrets para repo.
- logar secrets.
- upload direto do frontend.
- mexer em Brevo.
- mexer em Orders/Pix/Delivery.
- alterar regras de produto.

==================================================
16. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Estratégia escolhida: tool, script ou ambos.
2. Arquivos criados/alterados.
3. Guards anti-production implementados.
4. Como dry-run funciona.
5. Como execute funciona.
6. Como imagens elegíveis são selecionadas.
7. Como object keys são geradas.
8. Como banco é atualizado.
9. Como relatório é gerado.
10. Testes criados/alterados.
11. Resultado dotnet build/test.
12. Script/comando para rodar no TESTE.
13. Evidência de que produção não roda backfill.
14. Pendências restantes.

Critérios de aceite:
- dry-run funciona sem alteração.
- execute exige confirmação explícita.
- Production é bloqueado.
- imagens locais de TESTE sobem para R2.
- DB só atualiza após upload OK.
- arquivos locais não são apagados.
- relatório sem secrets.
- produção não recebe carga automática.