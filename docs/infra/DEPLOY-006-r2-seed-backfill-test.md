# DEPLOY-006 — R2 demo seed fix + TEST backfill

Escopo: **somente ambiente TESTE**. Não executar em HML/PROD.

## Causa raiz

Com `Storage__Provider=CloudflareR2` ativo, o demo seed encontrava as 20 imagens já no banco pelo filename, fazia `images skipped=20` e **reescrevia só `Url`** para `assets-teste…` **sem** `UploadAsync`. O bucket `shopflow-products-test` ficou vazio; `ObjectKey` permaneceu `seed-products/…` e `StorageProvider` NULL.

## Correção do seed

Quando o provider é CloudflareR2:

- Linha **não** é considerada pronta só por existir no DB.
- Migra/upload se: `StorageProvider != CloudflareR2`, `ObjectKey` vazio, key legado `seed-products/*`, Url fora do `PublicBaseUrl`, ou objeto ausente (`ExistsAsync` / HeadObject).
- **Nunca** reescreve Url R2 sem upload (ou objeto já confirmado).
- Após sucesso: `StorageProvider`, `ObjectKey`, `Url`, `ContentType`, `SizeBytes`.
- Falha de upload: DB intacto.
- Arquivos locais **não** são apagados.

## Backfill TEST-only

Script: `deploy/scripts/backfill-product-images-r2-test.sh`  
Tool: `apps/api/tools/Vls.Shopflow.Tools` → `product-images backfill-r2`

Guards:

- ambiente **Testing** apenas
- bucket **`shopflow-products-test`**
- PublicBaseUrl host **`assets-teste.vipassessoriadigital.com.br`**
- dry-run padrão
- execute exige `R2ImageBackfill__Enabled=true` + `--confirm TESTE_R2_IMAGE_BACKFILL`
- source root padrão `/app/wwwroot/uploads`
- não apaga locais; DB só após upload OK (ou objeto já existente → só metadata)
- cobre as 20 seed + 4 admin; ignora órfãos sem row

### Dry-run

```bash
./deploy/scripts/backfill-product-images-r2-test.sh --dry-run
```

### Execute

```bash
# em deploy/.env.test (temporário):
R2ImageBackfill__Enabled=true

./deploy/scripts/backfill-product-images-r2-test.sh \
  --execute --confirm TESTE_R2_IMAGE_BACKFILL

# depois: R2ImageBackfill__Enabled=false
```

## Validação

1. Relatório: eligible / skipped / uploaded / unchanged / failed.
2. DB: `StorageProvider=CloudflareR2`, `ObjectKey` `products/…`, Url em `assets-teste…`.
3. Bucket: objetos sob `products/` (ListObjects / console).
4. Vitrine/admin: imagens carregam pelo domínio público TESTE.
5. Novo upload admin → R2 direto.

## Rollback

- Não apagar objetos R2 nesta etapa.
- Se Url/metadata errados: restaurar backup DB TESTE **ou** apontar Url de volta para `/uploads/…` enquanto os arquivos locais existirem.
- Locais em `/app/wwwroot/uploads` permanecem como rede de segurança até validação.

## Por que não apagar locais agora

Permite rollback rápido e reexecução do backfill sem recriar assets. Limpeza é passo posterior, explícito e opcional.
