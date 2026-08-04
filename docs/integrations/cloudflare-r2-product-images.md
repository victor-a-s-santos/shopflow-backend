# Cloudflare R2 — imagens de produto

Backend usa object storage S3-compatible (**Cloudflare R2**) quando `Storage__Provider=CloudflareR2`.
Development usa `Storage__Provider=Local` (`wwwroot/uploads`).

Runbook operacional: [`docs/infra/RUNBOOK-005-cloudflare-r2-product-images.md`](../infra/RUNBOOK-005-cloudflare-r2-product-images.md)

## Env (canônico)

```bash
Storage__Provider=CloudflareR2
Storage__R2__Endpoint=https://<ACCOUNT_ID>.r2.cloudflarestorage.com
Storage__R2__AccountId=<ACCOUNT_ID>
Storage__R2__Bucket=shopflow-products-test
Storage__R2__AccessKeyId=...
Storage__R2__SecretAccessKey=...
Storage__R2__Region=auto
Storage__R2__ForcePathStyle=true
Storage__R2__PublicBaseUrl=https://assets-teste.vipassessoriadigital.com.br
Storage__R2__KeyPrefix=products
```

## Keys / URL

- Upload admin: `products/{productId}/{imageId}-{slug}.{ext}`
- Seed: `products/seed/{productSlug}/{file}` (idempotente)
- Pública: `{PublicBaseUrl}/{ObjectKey}`
- Cache-Control (R2): `public, max-age=31536000, immutable`

## Persistência

`product_images`: `Url` (pública), `ObjectKey`, `StorageProvider`, `ContentType`, `SizeBytes`, …

## Compatibilidade

Imagens antigas com URL `/uploads/...` continuam válidas enquanto o arquivo existir.

## Backfill TEST (manual)

Migração local → R2 **somente TESTE**, sob demanda (CLI / script). Sem worker, migration ou startup.

- Dry-run / execute + confirmação: `deploy/scripts/backfill-product-images-r2-test.sh`
- Tool: `apps/api/tools/Vls.Shopflow.Tools` → `product-images backfill-r2`
- Flag: `R2ImageBackfill__Enabled` (default `false`; nunca em Production)
- Relatório / how-to: [`docs/qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md`](../qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md)

Guards: aborta Production, connection string “prod-looking”, provider ≠ CloudflareR2, source root ausente; execute exige flag + frase `TESTE_R2_IMAGE_BACKFILL`. DB só após upload OK; arquivos locais **não** são apagados.
