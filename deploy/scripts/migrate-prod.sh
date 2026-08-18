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

echo "==> Aplicando migrations — ambiente PRODUÇÃO"
echo "    (migrations rodam automaticamente no startup da API)"

docker compose -f "$COMPOSE_FILE" up -d postgres
docker compose -f "$COMPOSE_FILE" restart api-prod

echo "==> Aguardando logs recentes..."
sleep 3
docker compose -f "$COMPOSE_FILE" logs --tail=30 api-prod

echo "==> Migrations produção concluídas (verifique erros nos logs acima)."
