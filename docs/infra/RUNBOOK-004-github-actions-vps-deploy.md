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

A chave SSH do CI é armazenada no GitHub como **Base64** (`VPS_SSH_KEY_B64`) para evitar perda de quebras de linha no secret (erro `libcrypto` / `Permission denied`).

---

## 1. Gerar chave SSH exclusiva para GitHub Actions

No Mac (máquina local), gere um par **somente** para o CI:

```bash
ssh-keygen -t ed25519 -C "github-actions-shopflow-vps" -f ~/.ssh/shopflow_actions_vps
```

Arquivos gerados:

| Arquivo | Uso |
|---------|-----|
| `~/.ssh/shopflow_actions_vps` | Chave **privada** → codificar em Base64 → secret `VPS_SSH_KEY_B64` |
| `~/.ssh/shopflow_actions_vps.pub` | Chave **pública** → `authorized_keys` na VPS |

Não reutilize a chave pessoal do dia a dia. Não commite nenhum dos dois arquivos.

---

## 2. Adicionar a chave pública na VPS

No Mac, copie a pública:

```bash
cat ~/.ssh/shopflow_actions_vps.pub
```

Na VPS, conecte com o usuário de deploy (`VPS_USER`) e **adicione** a linha (não apague chaves existentes):

```bash
mkdir -p ~/.ssh
chmod 700 ~/.ssh
nano ~/.ssh/authorized_keys
# cole a linha da .pub em uma linha NOVA no final do arquivo
chmod 600 ~/.ssh/authorized_keys
```

**Importante:** não remova outras entradas de `authorized_keys`. A chave do Actions é adicional.

Confirme que o usuário consegue:

- ler/escrever `/opt/shopflow/app`
- executar `docker` / `docker compose`

---

## 3. Gerar o secret Base64 no Mac

```bash
base64 -i ~/.ssh/shopflow_actions_vps | pbcopy
```

Isso copia o Base64 da chave privada para o clipboard (uma linha, sem quebras problemáticas).

---

## 4. Criar secrets no GitHub

No repositório backend: **Settings → Secrets and variables → Actions**.

| Secret | Conteúdo |
|--------|----------|
| `VPS_HOST` | Hostname ou IP da VPS (sem `user@`) |
| `VPS_USER` | Usuário SSH de deploy |
| `VPS_SSH_KEY_B64` | Saída do `base64 -i … \| pbcopy` (chave privada em Base64) |

Remova o secret antigo `VPS_SSH_KEY` se ainda existir (não é mais usado).

Regras:

- Não coloque IP, usuário ou chave no YAML.
- Não armazene `deploy/.env*` no GitHub.
- Não imprima o Base64 nem a chave privada nos logs.

---

## 5. Como funciona `develop` → teste

1. Push (ou merge) na branch `develop`.
2. Job `deploy-test` roda com concurrency `shopflow-vps-deploy`.
3. Setup SSH: decodifica `VPS_SSH_KEY_B64` → `~/.ssh/id_ed25519`.
4. `rsync` com `-e "ssh -i ~/.ssh/id_ed25519 …"` para `/opt/shopflow/app`, excluindo `.env` reais, `apps/web`, etc.
5. Na VPS, em `/opt/shopflow/app/deploy`:
   - `docker compose config`
   - `docker compose build --no-cache api-test worker-test`
   - `docker compose up -d --force-recreate --no-deps api-test worker-test`
6. Health: `curl -fsS https://api-teste.vipassessoriadigital.com.br/health`
7. Em falha: `docker compose logs --tail=100 api-test worker-test`

`--no-deps` evita recriar `postgres` e `caddy`. Volumes permanecem.

---

## 6. Como funciona `staging` → HML

Igual ao fluxo de teste, com job `deploy-hml`:

- Branch: `staging`
- Serviços: `api-hml`, `worker-hml`
- Health: `https://api-hml.vipassessoriadigital.com.br/health`
- Logs em falha: `docker compose logs --tail=100 api-hml worker-hml`

---

## 7. Deploy manual (`workflow_dispatch`)

1. GitHub → **Actions** → workflow **Deploy VPS**
2. **Run workflow**
3. Escolha `test` ou `hml`
4. A branch selecionada no dropdown é a sincronizada

---

## 8. Como testar localmente (Mac → VPS)

Substitua `IP_DA_VPS` / usuário conforme seus secrets:

```bash
ssh -i ~/.ssh/shopflow_actions_vps root@IP_DA_VPS
```

Ou, se `VPS_USER` não for root:

```bash
ssh -i ~/.ssh/shopflow_actions_vps USUARIO@IP_DA_VPS 'cd /opt/shopflow/app/deploy && docker compose ps'
```

Se o login local com essa chave falhar, o Actions também falhará.

---

## 9. Como validar no Actions

1. Push em `develop` (ou **Run workflow** com `test`).
2. Abra a run → steps **Setup SSH**, **Sync code**, **Build and recreate…**, **Health check…**.
3. Sucesso esperado no health: JSON com `"status":"ok"`.
4. Em falha de health, o job imprime `docker compose logs --tail=100` dos serviços do ambiente.

Na VPS:

```bash
cd /opt/shopflow/app/deploy
docker compose ps
curl -fsS https://api-teste.vipassessoriadigital.com.br/health
curl -fsS https://api-hml.vipassessoriadigital.com.br/health
```

---

## 10. Troubleshooting

### `Load key …: error in libcrypto`

Causa comum: secret com chave PEM colada e quebras de linha corrompidas.

Correção:

1. Use **apenas** `VPS_SSH_KEY_B64` (Base64 da privada).
2. Regenere o secret no Mac: `base64 -i ~/.ssh/shopflow_actions_vps | pbcopy`
3. Cole o valor inteiro no secret (sem aspas, sem espaços extras).
4. Remova o secret antigo `VPS_SSH_KEY` se ainda existir.

### `Permission denied (publickey)`

- Pública correspondente não está em `~/.ssh/authorized_keys` do **mesmo** usuário (`VPS_USER`).
- Chave errada (privada Base64 de outro par).
- Teste local: `ssh -i ~/.ssh/shopflow_actions_vps USUARIO@HOST`.
- Não apague outras chaves ao editar `authorized_keys`; apenas **adicione** a linha da `.pub`.

### `rsync error code 255`

Quase sempre falha de SSH (auth ou host). Resolva `libcrypto` / `publickey` primeiro. Confira `VPS_HOST`, `VPS_USER` e que o step Setup SSH terminou sem erro.

---

## 11. Rollback simples

1. Revert/checkout do commit bom em `develop` ou `staging`.
2. Push (ou `workflow_dispatch`).
3. O workflow reconstrói só os serviços do ambiente.

Não use `docker compose down`. Não remova volumes.

---

## 12. Cuidados com secrets

- Rsync nunca envia `deploy/.env`, `.env.test`, `.env.hml`.
- Arquivos excluídos no rsync não são apagados no destino (`--delete` sem `--delete-excluded`).
- Não imprima a chave nem o Base64 nos logs.
- Rotacione o par SSH do CI se vazar.
- Frontend (`apps/web`) é excluído do sync.

---

## 13. Limitação: pasta única na VPS

`/opt/shopflow/app` é compartilhado. O último deploy deixa o código da branch mais recente no disco; containers `api-test` / `api-hml` são imagens separadas buildadas no momento do deploy. Concurrency única evita dois deploys ao mesmo tempo.

---

## Checklist de configuração (uma vez)

- [ ] `ssh-keygen … -f ~/.ssh/shopflow_actions_vps`
- [ ] Pública **adicionada** (sem apagar outras) em `authorized_keys` na VPS
- [ ] `base64 -i ~/.ssh/shopflow_actions_vps | pbcopy` → secret `VPS_SSH_KEY_B64`
- [ ] Secrets `VPS_HOST`, `VPS_USER` mantidos
- [ ] Secret antigo `VPS_SSH_KEY` removido
- [ ] Teste local `ssh -i ~/.ssh/shopflow_actions_vps …` OK
- [ ] `.env` / `.env.test` / `.env.hml` existem em `/opt/shopflow/app/deploy`
- [ ] Workflow atualizado mergeado em `develop` / `staging`

---

## Referências

- [RUNBOOK-001-vps-setup-deploy.md](./RUNBOOK-001-vps-setup-deploy.md)
- [DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md](./DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md)
- [ADR-002-deploy-docker-compose-vps.md](./ADR-002-deploy-docker-compose-vps.md)
