# RUNBOOK-004 — Deploy automático na VPS via GitHub Actions

Runbook para publicar o **Shopflow Backend** na VPS existente usando GitHub Actions + SSH/rsync.

**Pré-requisitos:** [RUNBOOK-001-vps-setup-deploy.md](./RUNBOOK-001-vps-setup-deploy.md), [deploy/README.md](../../deploy/README.md)

**Workflow:** [`.github/workflows/deploy-vps.yml`](../../.github/workflows/deploy-vps.yml)

---

## Escopo

| Inclui | Não inclui |
|--------|------------|
| Push `develop` → rebuild `api-test` + `worker-test` | Produção |
| Push `staging` → rebuild `api-hml` + `worker-hml` | Cloudflare / frontend |
| `workflow_dispatch` manual (test ou hml) | Mercado Pago |
| Sync de código para `/opt/shopflow/app` | Envio de `.env` reais |
| Health check HTTPS das APIs | `docker compose down`, recreate de postgres/caddy |

PaymentsPix permanece Fake/Pending.

---

## 1. Gerar chave SSH exclusiva para GitHub Actions

Na sua máquina local (não na VPS), gere um par **somente** para o CI:

```bash
ssh-keygen -t ed25519 -C "github-actions-shopflow-backend" -f ./shopflow-gha-deploy -N ""
```

Arquivos gerados:

| Arquivo | Uso |
|---------|-----|
| `shopflow-gha-deploy` | Chave **privada** → secret `VPS_SSH_KEY` no GitHub |
| `shopflow-gha-deploy.pub` | Chave **pública** → `authorized_keys` na VPS |

Não reutilize a chave pessoal do dia a dia. Não commite nenhum dos dois arquivos.

---

## 2. Adicionar a chave pública na VPS

Conecte na VPS com seu usuário de deploy (o mesmo que irá no secret `VPS_USER`) e autorize a chave pública:

```bash
mkdir -p ~/.ssh
chmod 700 ~/.ssh
nano ~/.ssh/authorized_keys
# cole o conteúdo de shopflow-gha-deploy.pub em uma linha nova
chmod 600 ~/.ssh/authorized_keys
```

Confirme que o usuário consegue:

- ler/escrever `/opt/shopflow/app`
- executar `docker` / `docker compose` (grupo `docker` ou root, conforme o setup atual)

Teste a partir da máquina local:

```bash
ssh -i ./shopflow-gha-deploy USUARIO@HOST 'cd /opt/shopflow/app/deploy && docker compose ps'
```

---

## 3. Criar secrets no GitHub

No repositório backend: **Settings → Secrets and variables → Actions → New repository secret**.

| Secret | Conteúdo |
|--------|----------|
| `VPS_HOST` | Hostname ou IP da VPS (sem `user@`) |
| `VPS_USER` | Usuário SSH de deploy |
| `VPS_SSH_KEY` | Conteúdo completo da chave **privada** (`shopflow-gha-deploy`), incluindo linhas `BEGIN`/`END` |

Regras:

- Não coloque IP, usuário ou chave no YAML do workflow.
- Não armazene `deploy/.env*` no GitHub.
- Após cadastrar, apague a chave privada local se não for mais necessária, ou guarde em cofre fora do Git.

---

## 4. Como funciona `develop` → teste

1. Push (ou merge) na branch `develop`.
2. Job `deploy-test` roda com concurrency `shopflow-vps-deploy` (um deploy por vez na VPS).
3. Checkout do código no runner.
4. `rsync` para `/opt/shopflow/app`, **excluindo** `.env` reais, `apps/web`, `bin`/`obj`, uploads, dados Docker locais, etc.
5. Na VPS, em `/opt/shopflow/app/deploy`:
   - `docker compose config`
   - `docker compose build --no-cache api-test worker-test`
   - `docker compose up -d --force-recreate --no-deps api-test worker-test`
6. Health: `curl -fsS https://api-teste.vipassessoriadigital.com.br/health`
7. Em falha: `docker compose logs --tail=100 api-test worker-test`

`--no-deps` evita recriar `postgres` e `caddy`. Volumes (uploads, DataProtection, Postgres) permanecem.

---

## 5. Como funciona `staging` → HML

Igual ao fluxo de teste, com job `deploy-hml`:

- Branch: `staging`
- Serviços: `api-hml`, `worker-hml`
- Health: `https://api-hml.vipassessoriadigital.com.br/health`
- Logs em falha: `docker compose logs --tail=100 api-hml worker-hml`

---

## 6. Deploy manual (`workflow_dispatch`)

1. GitHub → **Actions** → workflow **Deploy VPS**
2. **Run workflow**
3. Escolha:
   - `test` → mesmo fluxo do job de teste
   - `hml` → mesmo fluxo do job de HML
4. A branch selecionada no dropdown do Actions é a que será sincronizada.

Use isso para republicar sem novo commit, ou para validar secrets após a configuração inicial.

---

## 7. Como validar logs

### No GitHub Actions

Abra a run falha/sucesso → steps **Build and recreate…** e **Health check…**.

### Na VPS

```bash
cd /opt/shopflow/app/deploy

docker compose ps
docker compose logs --tail=100 api-test worker-test
docker compose logs --tail=100 api-hml worker-hml

curl -fsS https://api-teste.vipassessoriadigital.com.br/health
curl -fsS https://api-hml.vipassessoriadigital.com.br/health
```

---

## 8. Rollback simples

Como as imagens são buildadas no deploy a partir do código sincronizado:

1. No GitHub, faça checkout/revert do commit bom na branch (`develop` ou `staging`).
2. Push (ou rode `workflow_dispatch` nessa branch).
3. O workflow reconstrói e recreia só os serviços do ambiente.

Alternativa manual na VPS (sem Actions):

```bash
cd /opt/shopflow/app
# restaure o código desejado (git checkout de tag/commit, se a pasta for um clone)
cd deploy
docker compose build --no-cache api-test worker-test   # ou api-hml worker-hml
docker compose up -d --force-recreate --no-deps api-test worker-test
```

Não use `docker compose down`. Não remova volumes.

---

## 9. Cuidados para não vazar secrets

- O rsync **nunca** envia `deploy/.env`, `deploy/.env.test`, `deploy/.env.hml`.
- Com `--delete`, arquivos excluídos no rsync **não** são apagados no destino (rsync não usa `--delete-excluded`).
- O workflow falha cedo se `.env` / `.env.test` ou `.env.hml` estiverem ausentes na VPS.
- Não imprima connection strings nos logs do Actions.
- Rotacione a chave SSH do CI se ela vazar.
- Frontend (`apps/web`) é excluído do sync.

---

## 10. Limitação: pasta única na VPS

A VPS usa **um único** diretório de código: `/opt/shopflow/app`.

| Efeito | Detalhe |
|--------|---------|
| Filesystem | O último deploy (develop **ou** staging) deixa o código daquela branch no disco |
| Containers | `api-test` / `api-hml` (e workers) são **imagens separadas**, buildadas no momento do deploy de cada ambiente |
| Risco | Deploy de `staging` sobrescreve arquivos no disco usados como contexto de build; o ambiente teste continua rodando a imagem anterior até o próximo deploy de `develop` |
| Mitigação | Concurrency única evita dois deploys ao mesmo tempo; não misture mudanças incompatíveis de compose entre branches sem coordenar |

Uploads e chaves DataProtection ficam em **volumes Docker**, não no sync do rsync.

---

## Checklist de configuração (uma vez)

- [ ] Par de chaves SSH exclusivo gerado
- [ ] Pública em `~/.ssh/authorized_keys` do `VPS_USER`
- [ ] Secrets `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` no GitHub
- [ ] `/opt/shopflow/app/deploy/.env`, `.env.test`, `.env.hml` já existem na VPS
- [ ] Branches `develop` e `staging` existem no remote
- [ ] Workflow `.github/workflows/deploy-vps.yml` na branch padrão (e mergeado nas branches de deploy)

---

## Referências

- [RUNBOOK-001-vps-setup-deploy.md](./RUNBOOK-001-vps-setup-deploy.md)
- [DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md](./DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md)
- [ADR-002-deploy-docker-compose-vps.md](./ADR-002-deploy-docker-compose-vps.md)
