#!/usr/bin/env bash
set -euo pipefail

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
docker compose -f "$COMPOSE_FILE" build api-prod worker-prod
docker compose -f "$COMPOSE_FILE" up -d postgres api-prod worker-prod caddy
# Bind-mounted Caddyfile.prod: reload so proxy header changes apply without full recreate.
docker compose -f "$COMPOSE_FILE" exec -T caddy caddy reload --config /etc/caddy/Caddyfile

echo "==> Status"
docker compose -f "$COMPOSE_FILE" ps api-prod worker-prod caddy postgres

echo "==> Deploy produção concluído."
echo "    API: https://api.vipassessoriadigital.com.br"
