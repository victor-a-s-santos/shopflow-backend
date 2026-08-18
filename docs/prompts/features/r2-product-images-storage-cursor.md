Você está atuando como engenheiro fullstack sênior do projeto Shopflow, especialista em .NET, EF Core, Clean Architecture, DDD, S3-compatible storage, Cloudflare R2, React/Vite, catálogo, admin product images e produção.

Objetivo:
Migrar o armazenamento de imagens da loja para Cloudflare R2, mantendo compatibilidade com o fluxo atual de cadastro/edição de produtos.

Contexto:
O Shopflow hoje já possui upload/gerenciamento de imagens de produto no admin, mas antes da versão de produção precisamos mover as imagens para object storage.

Decisão:
Usar Cloudflare R2 como storage de imagens da loja.

R2 deve ser acessado pelo backend via API S3-compatible.
O frontend não deve possuir credenciais R2.
O frontend apenas consome URLs públicas retornadas pela API.
Para produção, preferir servir imagens por domínio customizado do bucket, não por URL temporária local.

Não implementar CDN complexa.
Não implementar upload direto do browser para R2 nesta fase.
Não implementar múltiplos buckets por loja.
Não implementar edição/crop de imagem.
Não alterar regras de Catalog/Product/SKU além do necessário para storage.

==================================================
1. ESCOPO
==================================================

Implementar:

1. Abstração de storage:
- IObjectStorageService
- ProductImageStorageService ou equivalente

2. Provider R2:
- usa S3-compatible API;
- endpoint configurável;
- bucket configurável;
- access key/secret via env;
- public base URL configurável.

3. Upload de imagens de produto:
- admin envia imagem para backend;
- backend valida;
- backend salva objeto no R2;
- backend persiste metadata/URL/key no banco.

4. Delete de imagens:
- remover metadata no banco;
- remover objeto no R2 quando seguro;
- se falhar delete no R2, registrar erro controlado.

5. Read/list:
- ProductDetail/Admin/Public DTOs continuam retornando URLs de imagem.
- ProductCard/vitrine deve usar URL pública final.

6. Compatibilidade:
- imagens antigas locais devem continuar funcionando ou ter fallback documentado.
- se já houver registros com URL local, não quebrar tela.
- migração de imagens antigas pode ficar documentada como tarefa manual/futura se não houver massa real.

==================================================
2. CONFIGURAÇÃO
==================================================

Adicionar configuração backend:

R2Storage:
  Enabled: true
  Provider: "CloudflareR2"
  AccountId: ""
  BucketName: ""
  AccessKeyId: ""
  SecretAccessKey: ""
  ServiceUrl: "https://<ACCOUNT_ID>.r2.cloudflarestorage.com"
  PublicBaseUrl: "https://imagens.seudominio.com.br"
  ProductImagesPrefix: "products"
  MaxImageBytes: 5242880

Usar nomes conforme padrão atual do projeto.

Regras:
- secrets nunca em appsettings commitado;
- usar env vars;
- `.env.example` deve ter placeholders seguros;
- produção não pode depender de caminho local.

==================================================
3. MODELAGEM DE IMAGEM
==================================================

Auditar o modelo atual de ProductImage.

Verificar se já existe:
- id
- productId
- url
- fileName
- contentType
- size
- isPrimary
- displayOrder

Adicionar se necessário:
- StorageProvider
- StorageKey
- PublicUrl
- OriginalFileName
- ContentHash, opcional
- CreatedAt

Se o modelo atual já suporta URL, evitar migration desnecessária.
Mas é recomendado persistir pelo menos:
- storageProvider = R2
- storageKey = products/{productId}/{imageId}.{ext}
- url/publicUrl

Critério:
- o backend deve conseguir deletar objeto usando StorageKey.
- não depender apenas da URL para remover do R2.

==================================================
4. VALIDAÇÃO DE UPLOAD
==================================================

Manter/garantir validações existentes:

- tamanho máximo;
- MIME permitido:
  - image/jpeg
  - image/png
  - image/webp
- validação por magic bytes, se já implementada;
- extensão coerente;
- bloquear SVG se não houver sanitização;
- bloquear arquivos executáveis;
- nome de arquivo normalizado;
- não confiar em nome enviado pelo usuário.

Erros:
- imagem muito grande;
- tipo inválido;
- arquivo vazio;
- upload falhou.

Mensagens PT-BR/ProblemDetails conforme padrão atual.

==================================================
5. CHAVES/OBJECT KEYS
==================================================

Gerar keys previsíveis e seguras:

products/{productId}/{imageId}.{extension}

Ou:

products/{yyyy}/{MM}/{productId}/{imageId}.{extension}

Regras:
- não usar nome original como key principal;
- evitar caracteres especiais;
- não expor path local;
- manter extensão correta.

==================================================
6. PUBLIC URL
==================================================

A URL pública deve ser:

{PublicBaseUrl}/{StorageKey}

Exemplo:

https://imagens.vipassessoriadigital.com.br/products/{productId}/{imageId}.webp

Não usar URL assinada nesta fase para imagem pública de produto.
Não usar endpoint backend proxy para servir imagem pública, salvo fallback.

==================================================
7. BACKEND SERVICES
==================================================

Criar ou ajustar:

- ObjectStorageOptions
- IObjectStorageService
- R2ObjectStorageService
- ProductImageStorageService

Operações mínimas:
- UploadAsync(key, stream, contentType, cancellationToken)
- DeleteAsync(key, cancellationToken)
- BuildPublicUrl(key)

Usar AWS SDK S3 se já aceito no projeto.
Configurar:
- ServiceURL;
- ForcePathStyle, se necessário;
- Region/Authentication conforme R2;
- timeout e retry controlados.

==================================================
8. ADMIN PRODUCT IMAGES
==================================================

Auditar endpoints existentes:
- upload image;
- delete image;
- set primary;
- reorder, se houver.

Ajustar para R2:
- upload salva no R2;
- delete remove do R2 e do banco;
- update product não perde imagens;
- set primary continua funcionando;
- ProductDetail retorna URLs públicas.

Não alterar:
- Product description;
- isActive;
- salesRule;
- category;
- inventory.

==================================================
9. FRONTEND
==================================================

Auditar se frontend depende de path local.

Ajustar se necessário:
- AdminProductForm/Edit image preview;
- ProductCard;
- ProductDetail;
- Cart snapshots, se salvam imageUrl;
- Account/Orders, se exibem imagens de itens.

Critérios:
- frontend usa URL retornada pela API.
- não monta caminho local.
- fallback visual se imagem quebrar.
- não usa credencial R2.

==================================================
10. TESTES
==================================================

Backend unit:
1. gera StorageKey seguro.
2. BuildPublicUrl monta URL correta.
3. upload chama provider com key/contentType.
4. delete chama provider com key.
5. valida MIME permitido.
6. rejeita MIME inválido.
7. rejeita arquivo grande.
8. DTO retorna publicUrl.
9. delete metadata não quebra se objeto já não existir, conforme decisão.
10. imagem antiga/local continua renderizável ou fallback documentado.

Frontend unit:
1. ProductCard renderiza imageUrl pública.
2. Admin image preview renderiza URL pública.
3. fallback quando imagem ausente.
4. upload/delete mantêm comportamento.

Cypress:
- admin faz upload de imagem;
- produto aparece na vitrine com imagem;
- imagem permanece após editar produto;
- delete remove imagem da UI;
- set primary funciona.

Se Cypress não puder rodar:
- criar/ajustar spec;
- documentar comando.

==================================================
11. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/integrations/cloudflare-r2-product-images.md
- docs/catalog/product-images.md
- docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- apps/web/docs/ai-context/api-contracts.md
- apps/web/docs/ai-context/frontend-next-actions.md

Documentar:
- bucket;
- domínio público;
- env vars;
- formato das keys;
- limites de upload;
- fallback para imagens antigas;
- como validar em TESTE;
- checklist produção.

==================================================
12. NÃO FAZER
==================================================

Não implementar:
- upload direto do frontend para R2;
- presigned upload;
- transformação de imagem;
- thumbnails automáticos;
- crop;
- cache avançado;
- múltiplos buckets;
- imagens privadas;
- migração automática complexa de imagens antigas, salvo se trivial.

Não alterar:
- Pix;
- Orders;
- Delivery;
- Remessas;
- Brevo;
- Inventory;
- Customer auth.

==================================================
13. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Configurações/envs criadas.
3. Abstração de storage criada.
4. Provider R2 criado.
5. Como upload/delete funciona.
6. Como URL pública é montada.
7. Mudanças no banco/migration, se houve.
8. Mudanças no frontend, se houve.
9. Testes criados/alterados.
10. Resultado dotnet build/test.
11. Resultado npm typecheck/build, se frontend alterado.
12. Cypress criado/alterado e resultado, se executado.
13. Docs atualizadas.
14. Pendências restantes para produção.

Critérios de aceite:
- produto novo salva imagem no R2;
- vitrine carrega imagem pública do R2;
- admin preview funciona;
- delete funciona;
- set primary funciona;
- frontend não usa credenciais;
- envs não expõem secrets;
- build/testes passam.