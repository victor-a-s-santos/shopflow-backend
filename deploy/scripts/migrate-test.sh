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

echo "==> Aplicando migrations — ambiente TESTE"
echo "    (migrations rodam automaticamente no startup da API)"

docker compose up -d postgres
docker compose restart api-test

echo "==> Aguardando logs recentes..."
sleep 3
docker compose logs --tail=30 api-test

echo "==> Migrations teste concluídas (verifique erros nos logs acima)."
