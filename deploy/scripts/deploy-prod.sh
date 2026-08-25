#!/usr/bin/env bash
set -euo pipefail

# Manual PROD deploy — same contract as .github/workflows/deploy-prod.yml:
# rsync/git pull alone does NOT publish. API/worker only run rebuilt images.
# Prefer GitHub Actions (push main / workflow_dispatch) when possible.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(dirname "$SCRIPT_DIR")"
SHARED_ENV="${DEPLOY_DIR}/.env"
ENV_FILE="${DEPLOY_DIR}/.env.prod"
COMPOSE_FILE="${DEPLOY_DIR}/docker-compose.prod.yml"

cd "$DEPLOY_DIR"

if [[ ! -f "$SHARED_ENV" ]]; then
  echo "Arquivo .env não encontrado."
  echo "Execute: cp .env.example .env && edite POSTGRES_USER e POSTGRES_PASSWORD."
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Arquivo .env.prod não encontrado."
  echo "Execute: cp .env.prod.example .env.prod && edite os valores."
  exit 1
fi

echo "==> Build e deploy — ambiente PRODUÇÃO"
echo "    (rsync/código no disco ≠ containers em execução; rebuild obrigatório)"

docker compose -f "$COMPOSE_FILE" config >/dev/null
docker compose -f "$COMPOSE_FILE" up -d postgres
docker compose -f "$COMPOSE_FILE" build --no-cache api-prod worker-prod
docker compose -f "$COMPOSE_FILE" up -d --force-recreate --no-deps api-prod worker-prod
# Bind-mounted Caddyfile.prod: reload so proxy header changes apply without full recreate.
docker compose -f "$COMPOSE_FILE" up -d caddy
docker compose -f "$COMPOSE_FILE" exec -T caddy caddy reload --config /etc/caddy/Caddyfile

echo "==> Status"
docker compose -f "$COMPOSE_FILE" ps api-prod worker-prod caddy postgres
docker compose -f "$COMPOSE_FILE" images api-prod worker-prod

echo "==> Deploy produção concluído."
echo "    API: https://api.vipassessoriadigital.com.br"
echo "    Confirme versão nova: health OK + imagem Created recente (acima)."
