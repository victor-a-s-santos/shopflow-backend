# Product images — contrato

## Upload (nova imagem)

`POST /api/catalog/products/{id}/images`  
`Content-Type: multipart/form-data`  
Campo do arquivo: **`file`** (`IFormFile`).

Validações:

- Máximo **10** imagens por produto (409 se exceder).
- Tamanho: 1 byte … **5 MB**.
- Extensão: `.png`, `.jpg`, `.jpeg`, `.webp`.
- Content-Type: `image/png`, `image/jpeg`, `image/jpg`, `image/webp`.
- Magic bytes (assinatura real) devem bater com o tipo declarado.
- Erros por campo: `file`.

A primeira imagem do produto vira principal automaticamente. Uploads seguintes entram com `isPrimary: false` e `sortOrder` incremental.

## Remoção

`DELETE /api/catalog/products/{productId}/images/{imageId}`

- `imageId` deve pertencer ao produto (senão 409 `PRODUCT_IMAGE_NOT_FOUND`).
- Se remover a principal, a próxima por `sortOrder` é promovida.

## Imagem principal

`POST /api/catalog/products/{productId}/images/{imageId}/primary`

## O que não existe (ainda)

- Payload JSON único com `existingImages` / `newImages` / `removedImageIds` / `primaryImageId`.
- Reordenação em batch.
- Deduplicação por hash de conteúdo no servidor.

O frontend deve: carregar detalhe → upload novos arquivos um a um → DELETE dos removidos → POST primary.

Paths internos de disco **nunca** são expostos na API (só URL pública).
