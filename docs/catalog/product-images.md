# Product images

Imagens de produto: upload via admin → backend → object storage → URL pública nos DTOs.

Integração R2: [`docs/integrations/cloudflare-r2-product-images.md`](../integrations/cloudflare-r2-product-images.md)  
Contrato HTTP: [`docs/catalog/product-images-contract.md`](./product-images-contract.md)

## Fluxo

```
Admin (multipart) → HttpApi → UploadProductImageCommand
  → validação (MIME / magic bytes / 5 MB / máx. 10)
  → IImageStorage / IObjectStorageService (Local | CloudflareR2)
  → product_images (Url, StoragePath, StorageProvider)
```

## Campos persistidos

| Campo | Uso |
|-------|-----|
| `Url` | URL pública (vitrine/admin) |
| `ObjectKey` | Object key (delete) — ex-`StoragePath` |
| `StorageProvider` | `Local` \| `CloudflareR2` |
| `ContentType` / `SizeBytes` | metadados do upload |

Config: `Storage__Provider` + `Storage__R2__*` — ver `docs/infra/RUNBOOK-005-cloudflare-r2-product-images.md`.

Backfill local→R2 (TEST manual): [`docs/qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md`](../qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md).

## Limites

- 5 MB; PNG / JPEG / WEBP; sem SVG
- 10 imagens / produto

## Frontend

Usa só a URL da API (`resolveProductImageUrl`). Sem credenciais R2. URLs absolutas (R2) passam direto; `/uploads/...` antigo recebe origin da API.

## Cypress

Smoke sugerido (com API + R2 ou Local): upload no admin → imagem na vitrine → delete.
Comando típico: `npx cypress run --spec cypress/e2e/admin-product-images-r2.cy.ts` (spec documenta o fluxo; pode precisar fixtures locais).
