#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(dirname "$SCRIPT_DIR")"
SHARED_ENV="${DEPLOY_DIR}/.env"
ENV_FILE="${DEPLOY_DIR}/.env.hml"

cd "$DEPLOY_DIR"

if [[ ! -f "$SHARED_ENV" ]]; then
  echo "Arquivo .env não encontrado."
  echo "Execute: cp .env.example .env && edite POSTGRES_USER e POSTGRES_PASSWORD."
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Arquivo .env.hml não encontrado."
  echo "Execute: cp .env.hml.example .env.hml && edite os valores."
  exit 1
fi

echo "==> Build e deploy — ambiente HOMOLOGAÇÃO"
docker compose build api-hml worker-hml
docker compose up -d postgres api-hml worker-hml caddy

echo "==> Status"
docker compose ps api-hml worker-hml caddy postgres

echo "==> Deploy homologação concluído."
echo "    API: https://api-hml.vipassessoriadigital.com.br"
