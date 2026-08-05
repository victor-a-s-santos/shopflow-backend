#!/usr/bin/env bash
# Manual TEST-only backfill: local product images → Cloudflare R2.
# NEVER run against Production / HML.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(dirname "$SCRIPT_DIR")"
REPO_ROOT="$(cd "$DEPLOY_DIR/.." && pwd)"
API_DIR="$REPO_ROOT/apps/api"
ENV_FILE="${DEPLOY_DIR}/.env.test"

MODE="dry-run"
CONFIRM=""
SOURCE_ROOT=""
ENVIRONMENT="Testing"

ALLOWED_BUCKET="shopflow-products-test"
ALLOWED_PUBLIC_HOST="assets-teste.vipassessoriadigital.com.br"

usage() {
  cat <<'EOF'
Usage:
  ./deploy/scripts/backfill-product-images-r2-test.sh --dry-run
  ./deploy/scripts/backfill-product-images-r2-test.sh --execute --confirm TESTE_R2_IMAGE_BACKFILL

Options:
  --dry-run                 Plan only (default)
  --execute                 Upload + update DB (requires confirm + R2ImageBackfill__Enabled=true)
  --confirm PHRASE          Must be TESTE_R2_IMAGE_BACKFILL for execute
  --source-root PATH        Local uploads root (default: /app/wwwroot/uploads)
  --environment NAME        Must be Testing (default: Testing)
  -h|--help

Requires deploy/.env.test with Storage__Provider=CloudflareR2,
bucket shopflow-products-test, PublicBaseUrl assets-teste.
Does NOT delete local files. Does NOT target HML/production.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run) MODE="dry-run"; shift ;;
    --execute) MODE="execute"; shift ;;
    --confirm) CONFIRM="${2:-}"; shift 2 ;;
    --source-root) SOURCE_ROOT="${2:-}"; shift 2 ;;
    --environment) ENVIRONMENT="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown arg: $1"; usage; exit 2 ;;
  esac
done

if [[ "${ENVIRONMENT}" != "Testing" ]]; then
  echo "ABORT: environment must be Testing (got ${ENVIRONMENT})"
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ABORT: missing $ENV_FILE"
  echo "Copy deploy/.env.test.example → deploy/.env.test and fill R2 + Catalog connection."
  exit 1
fi

# shellcheck disable=SC1090
set -a
[[ -f "$DEPLOY_DIR/.env" ]] && source "$DEPLOY_DIR/.env"
source "$ENV_FILE"
set +a

if [[ "${ASPNETCORE_ENVIRONMENT:-}" == "Production" || "${DOTNET_ENVIRONMENT:-}" == "Production" ]]; then
  echo "ABORT: ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT=Production"
  exit 1
fi

export ASPNETCORE_ENVIRONMENT=Testing
export DOTNET_ENVIRONMENT=Testing

BUCKET="${Storage__R2__Bucket:-}"
PUBLIC_BASE="${Storage__R2__PublicBaseUrl:-}"
if [[ "$BUCKET" != "$ALLOWED_BUCKET" ]]; then
  echo "ABORT: Storage__R2__Bucket must be ${ALLOWED_BUCKET} (got '${BUCKET}')"
  exit 1
fi
case "$PUBLIC_BASE" in
  *"://${ALLOWED_PUBLIC_HOST}"|*"://${ALLOWED_PUBLIC_HOST}/"*) ;;
  *)
    echo "ABORT: Storage__R2__PublicBaseUrl host must be ${ALLOWED_PUBLIC_HOST}"
    exit 1
    ;;
esac

SOURCE_ROOT="${SOURCE_ROOT:-${Storage__Local__RootPath:-${Uploads__RootPath:-/app/wwwroot/uploads}}}"
REPORT_DIR="$REPO_ROOT/artifacts/r2-backfill"
mkdir -p "$REPORT_DIR"
REPORT_FILE="$REPORT_DIR/report-$(date -u +%Y%m%d-%H%M%S).md"

ARGS=(product-images backfill-r2 --environment Testing --source-root "$SOURCE_ROOT" --report "$REPORT_FILE")

if [[ "$MODE" == "execute" ]]; then
  if [[ "$CONFIRM" != "TESTE_R2_IMAGE_BACKFILL" ]]; then
    echo "ABORT: execute requires --confirm TESTE_R2_IMAGE_BACKFILL"
    exit 1
  fi
  if [[ "${R2ImageBackfill__Enabled:-false}" != "true" && "${R2ImageBackfill__Enabled:-false}" != "True" ]]; then
    echo "ABORT: set R2ImageBackfill__Enabled=true in .env.test for execute"
    exit 1
  fi
  ARGS+=(--execute --confirm "$CONFIRM")
else
  ARGS+=(--dry-run)
fi

echo "==> Running TEST product-images R2 backfill ($MODE)"
echo "    env=Testing source-root=$SOURCE_ROOT bucket=$ALLOWED_BUCKET"
echo "    report=$REPORT_FILE"

cd "$API_DIR"
dotnet run --project tools/Vls.Shopflow.Tools -- "${ARGS[@]}"
echo "==> Done. Review $REPORT_FILE"
