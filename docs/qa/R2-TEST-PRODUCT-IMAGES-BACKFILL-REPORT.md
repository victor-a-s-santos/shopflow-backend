# R2 TEST — product images backfill report

Template + how-to for the **manual TEST-only** local → Cloudflare R2 product-image backfill.

**Never run against Production.** Prefer dry-run first. Local files are **not** deleted.

## Prerequisites (TEST)

- `Storage__Provider=CloudflareR2` and valid `Storage__R2__*` (bucket TEST, `PublicBaseUrl` TEST).
- Source root with local files (e.g. volume `/app/wwwroot/uploads` or host path).
- Postgres TEST connection (`ConnectionStrings__DefaultConnection`).
- For **execute** only: `R2ImageBackfill__Enabled=true` **and** `--confirm TESTE_R2_IMAGE_BACKFILL`.

## Commands

### Dry-run (safe — no upload / no DB write)

```bash
./deploy/scripts/backfill-product-images-r2-test.sh --dry-run

# or
dotnet run --project apps/api/tools/Vls.Shopflow.Tools -- \
  product-images backfill-r2 \
  --environment Testing \
  --source-root /path/to/uploads \
  --dry-run \
  --report docs/qa/artifacts/r2-backfill-dry-run.json
```

### Execute (TEST only)

```bash
export R2ImageBackfill__Enabled=true
./deploy/scripts/backfill-product-images-r2-test.sh \
  --execute --confirm TESTE_R2_IMAGE_BACKFILL
```

## Guards (abort)

| Guard | Effect |
|-------|--------|
| `ASPNETCORE_ENVIRONMENT` / `--environment` = Production | Abort |
| Connection string looks like production | Abort |
| `Storage__Provider` ≠ CloudflareR2 | Abort |
| Missing bucket / PublicBaseUrl / source root | Abort |
| Execute without `Enabled` + exact confirm phrase | Abort |

## Selection

Eligible: `StorageProvider` Local or null; file exists under source root (via `ObjectKey` or `/uploads/` in URL); allowed image extension.

Skip: already CloudflareR2; missing file; unsupported extension.

Keys: same as normal upload (`products/{productId}/…`) or seed (`products/seed/…`).

## Persistence rule

DB (`Url`, `ObjectKey`, `StorageProvider`, `ContentType`, `SizeBytes`) updates **only after** successful R2 upload. Upload failure → row unchanged.

## Report fields (no secrets)

Fill after each run (or attach JSON from `--report`):

| Field | Value |
|-------|-------|
| Mode | dry-run / execute |
| Environment | |
| Source root | |
| Bucket (name only) | |
| PublicBaseUrl host | |
| Candidates | |
| Migrated | |
| Skipped (already R2) | |
| Skipped (missing file) | |
| Failed | |
| Report path | |
| Operator | |
| Date (UTC) | |
| Notes | |

## Sample JSON shape

```json
{
  "mode": "DryRun",
  "environment": "Testing",
  "candidates": 12,
  "wouldMigrate": 10,
  "skippedAlreadyR2": 1,
  "skippedMissingFile": 1,
  "failed": 0,
  "items": []
}
```

Access keys, connection strings, and tokens must **never** appear in the report.
