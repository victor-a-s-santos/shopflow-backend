# RUNBOOK-006 — Setup da VPS de produção

Runbook para criar e operar a **VPS exclusiva de PROD**. Não usar a VPS de TESTE/HML.

**Decisão:** [ADR-005-production-infrastructure.md](./ADR-005-production-infrastructure.md)

**Compose:** [deploy/docker-compose.prod.yml](../../deploy/docker-compose.prod.yml)

**Deploy automático:** [`.github/workflows/deploy-prod.yml`](../../.github/workflows/deploy-prod.yml)

Este documento **não** executa deploy. Preenche o checklist e os comandos que devem rodar **depois** na VPS nova.

---

## Escopo

| Inclui | Não inclui |
|--------|------------|
| VPS Hetzner exclusiva | Alterar TESTE/HML |
| Docker Compose `caddy` + `api-prod` + `worker-prod` + `postgres` | Kubernetes / K3s |
| Banco `shopflow_prod` | Reutilizar secrets SSH/env de TESTE/HML |
| DataProtection persistente | Enviar `.env.prod` pelo Git |
| R2 bucket PROD + Brevo PROD | RabbitMQ / MassTransit |
| GitHub Actions `main` → PROD | Frontend (Cloudflare Pages) |

---

## 1. Checklist exato — criação da VPS

Execute nesta ordem. Não pule itens de isolamento.

### Conta e servidor

- [ ] Criar **VPS nova** na Hetzner (não reutilizar a de TESTE/HML).
- [ ] Imagem: Ubuntu 24.04 LTS (ou Debian estável equivalente).
- [ ] Tamanho inicial: 2 vCPU / 4 GB RAM / 40 GB SSD (ajustar se o catálogo crescer).
- [ ] Localidade: Falkenstein ou Nuremberg (mesma região da VPS de teste, se possível).
- [ ] IPv4 público dedicado. IPv6 opcional.
- [ ] Anotar IP público. **Não** apontar DNS de TESTE/HML para este IP.

### Acesso SSH

- [ ] Gerar par de chaves **novo** só para operação humana da VPS PROD (não reutilizar a chave da VPS TESTE).
- [ ] Instalar a pública no `authorized_keys` do usuário de deploy (ex.: `root` ou `deploy`).
- [ ] Confirmar login por chave.
- [ ] Depois do login validado: desabilitar `PasswordAuthentication` e `PermitRootLogin` por senha.
- [ ] Gerar **outro** par só para GitHub Actions (`shopflow_actions_vps_prod`). Não reutilizar `VPS_SSH_KEY_B64` de TESTE/HML.

### Firewall (host)

Liberar somente:

- [ ] `22/tcp` (SSH; preferir allowlist do seu IP + GitHub Actions se usar IP ranges, ou fail2ban)
- [ ] `80/tcp` (HTTP — ACME + redirect)
- [ ] `443/tcp` (HTTPS da API)

Bloquear:

- [ ] `5432/tcp` (Postgres **não** publica porta no Compose; confirme que nenhum `-p 5432` existe)
- [ ] Qualquer outra porta de aplicação (`8080`, `5127`, etc.)

### DNS / Cloudflare (ainda não apontar se a stack não estiver no ar)

Registros no domínio `vipassessoriadigital.com.br`:

| Nome | Tipo | Destino | Proxy |
|------|------|---------|-------|
| `@` | CNAME ou Pages | Cloudflare Pages (frontend) | Proxied |
| `www` | CNAME | `@` ou Pages | Proxied |
| `api` | A | **IP da VPS PROD** | DNS only (cinza) no primeiro TLS; depois pode proxiar se Caddy + CF estiverem alinhados |
| `assets` | CNAME | custom domain do R2 bucket PROD | Proxied |

- [ ] `api.vipassessoriadigital.com.br` → VPS PROD (não a VPS TESTE).
- [ ] `assets.vipassessoriadigital.com.br` → bucket R2 `shopflow-products-prod`.
- [ ] Frontend Pages: `https://vipassessoriadigital.com.br` e `https://www.vipassessoriadigital.com.br`.

### Cloudflare R2 PROD

- [ ] Criar bucket **novo** `shopflow-products-prod` (não reutilizar `shopflow-products-test`).
- [ ] Custom domain `assets.vipassessoriadigital.com.br` no bucket PROD.
- [ ] Token R2 com Object Read & Write **somente** neste bucket.
- [ ] CORS do bucket permitindo GET/HEAD de `https://vipassessoriadigital.com.br` e `https://www.vipassessoriadigital.com.br`.
- [ ] Guardar Access Key / Secret **fora do Git** (só `.env.prod` na VPS).

### Brevo PROD

- [ ] API key **nova** (não a de TESTE).
- [ ] Sender `no-reply@vipassessoriadigital.com.br` verificado.
- [ ] `Brevo__Enabled=true` e `Brevo__SandboxMode=false` apenas no `.env.prod` da VPS.
- [ ] Inbox operacional em `AdminNotifications__ApprovalRequestsEmail`.

### Mercado Pago Production

- [ ] App / credenciais **Production** (não Sandbox).
- [ ] Webhook: `https://api.vipassessoriadigital.com.br/api/payments/pix/webhooks/mercado-pago`.
- [ ] `MercadoPago__Environment=Production`.
- [ ] `MercadoPago__SandboxPayerFirstNameOverride` vazio.
- [ ] `MercadoPago__WebhookRawCaptureEnabled=false`.

### GitHub Actions (secrets próprios)

No repositório: **Settings → Secrets and variables → Actions**. Criar **somente** estes (não copiar os de TESTE/HML):

| Secret | Conteúdo |
|--------|----------|
| `VPS_PROD_HOST` | IP ou hostname da VPS PROD |
| `VPS_PROD_USER` | Usuário SSH de deploy |
| `VPS_PROD_SSH_KEY_B64` | Chave privada ed25519 do Actions, em Base64 |

- [ ] Protection rule da branch `main` (review / ambiente `production` no GitHub, se desejar).
- [ ] Workflow [deploy-prod.yml](../../.github/workflows/deploy-prod.yml) mergeado em `main` **só depois** da primeira subida manual.

### Backup (obrigatório antes do go-live)

- [ ] Diretório `/opt/shopflow/backups` na VPS (fora do Git).
- [ ] `pg_dump` de `shopflow_prod` testado (ver §3).
- [ ] Retenção definida (ex.: 7 diários + 4 semanais).
- [ ] Restauração testada em volume descartável (não em TESTE/HML).
- [ ] R2 **não** substitui backup do banco.

---

## 2. Comandos na VPS (primeira subida)

Substitua `USER` e o IP. Não rode estes comandos na VPS de TESTE/HML.

### 2.1 Docker

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"
# saia e entre de novo no SSH para o grupo docker valer
docker version
docker compose version
```

### 2.2 Código

```bash
sudo mkdir -p /opt/shopflow/app /opt/shopflow/backups
sudo chown -R "$USER":"$USER" /opt/shopflow
# clone ou rsync do repositório (sem .env reais)
cd /opt/shopflow
git clone <URL_DO_REPO_BACKEND> app
cd /opt/shopflow/app
git checkout main
```

### 2.3 Variáveis (secrets só aqui)

```bash
cd /opt/shopflow/app/deploy
cp .env.example .env
cp .env.prod.example .env.prod
chmod 600 .env .env.prod
nano .env          # POSTGRES_USER + POSTGRES_PASSWORD fortes e exclusivos
nano .env.prod     # connection strings, R2, Brevo, MP, admin, CORS
```

A senha em `.env` **deve** ser a mesma das `ConnectionStrings__*` em `.env.prod`.

Confirme flags perigosas **antes** do primeiro `up`:

```bash
grep -E '^(ASPNETCORE_ENVIRONMENT|DemoCatalogSeed__Enabled|SHOPFLOW_ADMIN_RESET_PASSWORD|Storage__Provider|Storage__R2__Bucket|Brevo__Enabled|Brevo__SandboxMode|MercadoPago__Environment|MercadoPago__WebhookRawCaptureEnabled|R2ImageBackfill__Enabled)=' .env.prod
```

Esperado:

```
ASPNETCORE_ENVIRONMENT=Production
DemoCatalogSeed__Enabled=false
SHOPFLOW_ADMIN_RESET_PASSWORD=false
Storage__Provider=CloudflareR2
Storage__R2__Bucket=shopflow-products-prod
Brevo__Enabled=true
Brevo__SandboxMode=false
MercadoPago__Environment=Production
MercadoPago__WebhookRawCaptureEnabled=false
R2ImageBackfill__Enabled=false
```

### 2.4 Primeira subida da stack

```bash
cd /opt/shopflow/app/deploy
docker compose -f docker-compose.prod.yml config
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps
```

Containers esperados: `shopflow-caddy-prod`, `shopflow-postgres-prod`, `shopflow-api-prod`, `shopflow-worker-prod`.

### 2.5 Conferir banco e volumes

```bash
docker compose -f docker-compose.prod.yml exec postgres \
  psql -U shopflow -d shopflow_prod -c '\conninfo'
docker volume ls | grep -E 'shopflow-prod|shopflow_dataprotection_prod'
```

Volumes esperados:

- `shopflow-prod_postgres_data` (ou equivalente prefixado pelo project name `shopflow-prod`)
- `shopflow-prod_caddy_data`
- `shopflow-prod_caddy_config`
- `shopflow_dataprotection_prod` (nome explícito)

### 2.6 Health e CSRF

Só depois do DNS `api` apontar para esta VPS e as portas 80/443 estiverem abertas:

```bash
curl -sS https://api.vipassessoriadigital.com.br/health
# {"status":"ok","environment":"Production"}

curl -sS -o /dev/null -w "%{http_code}\n" \
  https://api.vipassessoriadigital.com.br/api/auth/csrf
# esperado: 200
```

### 2.7 Deploys seguintes (manual)

```bash
cd /opt/shopflow/app/deploy
./scripts/deploy-prod.sh
# migrations = restart da API (automáticas no startup)
./scripts/migrate-prod.sh
```

Não use `docker compose` **sem** `-f docker-compose.prod.yml` nesta VPS — o `docker-compose.yml` padrão é a stack TESTE/HML e não deve subir aqui.

### 2.8 Backup / restore (smoke)

```bash
cd /opt/shopflow/app/deploy
mkdir -p /opt/shopflow/backups
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_dump -U shopflow -d shopflow_prod --no-owner --format=custom \
  > /opt/shopflow/backups/shopflow_prod-$(date -u +%Y%m%dT%H%M%SZ).dump
```

Restore (somente em volume descartável / exercício controlado):

```bash
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_restore -U shopflow -d shopflow_prod --clean --if-exists \
  < /opt/shopflow/backups/ARQUIVO.dump
```

---

## 3. GitHub Actions depois da primeira subida

No Mac (par exclusivo PROD):

```bash
ssh-keygen -t ed25519 -C "github-actions-shopflow-vps-prod" -f ~/.ssh/shopflow_actions_vps_prod
cat ~/.ssh/shopflow_actions_vps_prod.pub
# cole em ~/.ssh/authorized_keys na VPS PROD (linha nova; não apague outras)
base64 -i ~/.ssh/shopflow_actions_vps_prod | pbcopy
```

Cole o Base64 no secret `VPS_PROD_SSH_KEY_B64`. Teste local:

```bash
ssh -i ~/.ssh/shopflow_actions_vps_prod USER@IP_VPS_PROD \
  'cd /opt/shopflow/app/deploy && docker compose -f docker-compose.prod.yml ps'
```

Push em `main` (ou **Actions → Deploy PROD → Run workflow**) só após este teste.

---

## 4. O que não fazer

- Não apontar `api-teste` / `api-hml` para a VPS PROD.
- Não copiar `.env.test` / `.env.hml` para `.env.prod`.
- Não reutilizar `VPS_HOST` / `VPS_SSH_KEY_B64` no workflow PROD.
- Não commitar `.env`, `.env.prod` ou API keys.
- Não expor Postgres.
- Não ligar `SHOPFLOW_ADMIN_RESET_PASSWORD=true` de forma permanente.
- Não ligar `DemoCatalogSeed__Enabled` nem `R2ImageBackfill__Enabled`.
- Não usar `docker compose down -v` (apaga volumes).

---

## Referências

- [ADR-005-production-infrastructure.md](./ADR-005-production-infrastructure.md)
- [deploy/README.md](../../deploy/README.md)
- [RUNBOOK-001-vps-setup-deploy.md](./RUNBOOK-001-vps-setup-deploy.md) (TESTE/HML — não usar nesta VPS)
- [RUNBOOK-004-github-actions-vps-deploy.md](./RUNBOOK-004-github-actions-vps-deploy.md) (TESTE/HML)
- [RUNBOOK-005-cloudflare-r2-product-images.md](./RUNBOOK-005-cloudflare-r2-product-images.md)
