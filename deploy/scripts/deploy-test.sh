#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(dirname "$SCRIPT_DIR")"
SHARED_ENV="${DEPLOY_DIR}/.env"
ENV_FILE="${DEPLOY_DIR}/.env.test"

cd "$DEPLOY_DIR"

if [[ ! -f "$SHARED_ENV" ]]; then
  echo "Arquivo .env não encontrado."
  echo "Execute: cp .env.example .env && edite POSTGRES_USER e POSTGRES_PASSWORD."
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Arquivo .env.test não encontrado."
  echo "Execute: cp .env.test.example .env.test && edite os valores."
  exit 1
fi

echo "==> Build e deploy — ambiente TESTE"
docker compose build api-test worker-test
docker compose up -d postgres api-test worker-test caddy
# Bind-mounted Caddyfile: reload so proxy header changes apply without full recreate.
docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile

echo "==> Status"
docker compose ps api-test worker-test caddy postgres

echo "==> Deploy teste concluído."
echo "    API: https://api-teste.vipassessoriadigital.com.br"
