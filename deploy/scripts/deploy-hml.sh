#!/usr/bin/env bash
set -euo pipefail

# Manual HML deploy — same contract as .github/workflows/deploy-vps.yml (staging):
# syncing source to the VPS does NOT publish. Always rebuild + force-recreate API/worker.

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
echo "    (código no disco ≠ containers; rebuild obrigatório)"

docker compose config >/dev/null
docker compose up -d postgres
docker compose build --no-cache api-hml worker-hml
docker compose up -d --force-recreate --no-deps api-hml worker-hml
# Bind-mounted Caddyfile: reload so proxy header changes apply without full recreate.
docker compose up -d caddy
docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile

echo "==> Status"
docker compose ps api-hml worker-hml caddy postgres
docker compose images api-hml worker-hml

echo "==> Deploy homologação concluído."
echo "    API: https://api-hml.vipassessoriadigital.com.br"
