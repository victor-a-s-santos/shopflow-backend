# RUNBOOK-005 — Cloudflare R2 (imagens de produto)

Status: pronto para configurar em **TESTE**. Não alterar produção neste passo.

## Por que R2 (e não GitHub / volume da VPS)

- Imagens de catálogo são assets públicos de longa duração.
- GitHub não é CDN de mídia; volumes Docker na VPS não escalam, complicam backup e multi-instância.
- R2 é object storage S3-compatible com domínio customizado HTTPS e custo previsível.

## Buckets / domínios

| Ambiente | Bucket | PublicBaseUrl |
|----------|--------|----------------|
| TESTE | `shopflow-products-test` | `https://assets-teste.vipassessoriadigital.com.br` |
| PRODUÇÃO | `shopflow-products-prod` | `https://assets.vipassessoriadigital.com.br` |

Buckets e custom domains são criados **no painel Cloudflare** (não via código Shopflow).

## CORS (bucket público de leitura)

Para o browser carregar `<img src="https://assets-…">` a partir do storefront (Pages / outro origin), configure CORS no R2 permitindo GET do domínio da loja. Upload **não** é feito do browser — só o backend usa Access Key.

Exemplo (ajuste origins):

```json
[
  {
    "AllowedOrigins": [
      "https://teste.vipassessoriadigital.com.br",
      "https://vipassessoriadigital.com.br",
      "http://localhost:8080"
    ],
    "AllowedMethods": ["GET", "HEAD"],
    "AllowedHeaders": ["*"],
    "ExposeHeaders": ["ETag", "Content-Length", "Content-Type"],
    "MaxAgeSeconds": 86400
  }
]
```

## API tokens / Access Keys

1. Cloudflare Dashboard → R2 → Manage R2 API Tokens.
2. Criar token com permissão de Object Read & Write **somente** no bucket do ambiente.
3. Anotar `Access Key ID` + `Secret Access Key` + `Account ID`.
4. Colocar **apenas** em `deploy/.env.test` (VPS) — nunca no git, nunca em Issues/PRs.

## Env vars (canônicas)

```bash
Storage__Provider=CloudflareR2
Storage__R2__Endpoint=https://<ACCOUNT_ID>.r2.cloudflarestorage.com
Storage__R2__AccountId=<ACCOUNT_ID>
Storage__R2__Bucket=shopflow-products-test   # ou shopflow-products-prod
Storage__R2__AccessKeyId=...
Storage__R2__SecretAccessKey=...
Storage__R2__Region=auto
Storage__R2__ForcePathStyle=true
Storage__R2__PublicBaseUrl=https://assets-teste.vipassessoriadigital.com.br
Storage__R2__KeyPrefix=products
```

Placeholders: `deploy/.env.test.example`, `deploy/.env.prod.example`, raiz `.env.example`.

Development: `Storage__Provider=Local` (wwwroot/uploads). Compat: `Uploads__*` ainda preenche Local se vazio.

## Object keys

- Admin upload: `products/{productId}/{imageId}-{slug}.{ext}`
- Seed demo (idempotente): `products/seed/{productSlug}/{fileName}`
- Cache-Control no R2: `public, max-age=31536000, immutable`
- URL pública: `{PublicBaseUrl}/{ObjectKey}`

## Preencher `.env.test` na VPS

1. `cp deploy/.env.test.example deploy/.env.test` (ou editar o existente).
2. Preencher `Storage__R2__*` com valores reais do bucket **test**.
3. Garantir migration Catalog aplicada (`AddProductImageObjectKeyAndMetadata` / pending).
4. Recriar `api-test` / `worker-test` conforme RUNBOOK de deploy.
5. **Não** copiar essas keys para produção neste momento.

## Validar upload em TESTE

```bash
# 1) Login admin + CSRF (cookies)
# 2) Upload
curl -sS -b cookies.txt -H "X-CSRF-TOKEN: $TOKEN" \
  -F "file=@./sample.png;type=image/png" \
  "https://api-teste.vipassessoriadigital.com.br/api/catalog/products/{PRODUCT_ID}/images"

# 3) Resposta deve trazer url https://assets-teste.vipassessoriadigital.com.br/products/...
# 4) Abrir a URL no browser / curl -I (200, cache-control)
# 5) DELETE da imagem e conferir remoção no bucket (ou log se já ausente)
```

## Troubleshooting

| Sintoma | Verificação |
|---------|-------------|
| 500 no upload | Logs API: credenciais, Endpoint, Bucket; `Storage__Provider` |
| URL 404 | Custom domain apontando para o bucket certo; key correta |
| CORS no `<img>` | CORS do bucket + HTTPS do domínio assets |
| Seed duplica imagens | Idempotência por `ObjectKey` / filename; re-run não deve criar linhas novas |
| Ainda grava em disco | `Storage__Provider` ainda `Local` no env do container |

## Política de secrets

- Nunca commitar `.env.test` / `.env.prod` com keys.
- Rotacionar token se vazar.
- Preferir token com scope mínimo por bucket.

## Relacionados

- `docs/integrations/cloudflare-r2-product-images.md`
- `docs/catalog/product-images.md`
- `docs/infra/RUNBOOK-001-vps-setup-deploy.md`
- `docs/infra/RUNBOOK-004-github-actions-vps-deploy.md`
