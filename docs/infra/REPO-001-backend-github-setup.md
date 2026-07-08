# REPO-001 — Preparo do repositório GitHub do backend

Documento de preparação do **Shopflow Backend** para versionamento em um repositório GitHub próprio, separado do frontend.

**Não altere** a arquitetura de deploy teste/HML, Cloudflare, produção ou Mercado Pago ao seguir este guia.

---

## 1. Por que o backend foi separado do frontend

- O frontend (`apps/web`) já tem ciclo de vida, CI e hospedagem próprios (ex.: Cloudflare Pages).
- O backend inclui API, worker, Compose de VPS, scripts e documentação de infra — responsabilidades distintas.
- Separar repositórios reduz risco de vazar secrets de deploy/DB no mesmo histórico do frontend e deixa o escopo de review/CI mais claro.

---

## 2. O que entra no repo backend

| Caminho | Motivo |
|---------|--------|
| `apps/api/` | Solução .NET, Dockerfiles, testes, `seed-assets/catalog-products/` |
| `deploy/` | Compose teste/HML, Caddy, Postgres init, scripts, `*.env.example` |
| `docs/` | Documentação técnica e `docs/infra/` |
| `docker-compose.yml` | Dev local (db + api + worker) |
| `.env.example` | Template local (admin seed) — sem secrets reais |
| `.gitignore` / `.dockerignore` | Segurança e builds limpos |
| `README.md` | Entrada do projeto |
| Collections Postman na raiz | Contratos úteis da API |

---

## 3. O que não entra no repo backend

- `apps/web/` e qualquer artefato exclusivo do frontend
- `node_modules/`, `bin/`, `obj/`, `dist/`, `build/`
- `.env` reais (`deploy/.env`, `.env.test`, `.env.hml`, root `.env`, etc.)
- Cookies, tokens, senhas, connection strings reais
- Secrets de Cloudflare ou de banco
- `dataprotection-keys/`, `uploads/`, dumps (`*.sql` de backup, `*.dump`, `*.bak`)
- Dados de runtime Docker (`postgres-data/`, `caddy-data/`, etc.)

**Exceção:** imagens demo em `apps/api/seed-assets/catalog-products/` **devem** ser versionadas. Migrations do EF **devem** ser versionadas.

---

## 4. Checklist antes do primeiro push

- [ ] `.gitignore` na raiz e `deploy/.gitignore` revisados
- [ ] `.dockerignore` na raiz (e em `apps/api/` se usado no build) revisados
- [ ] Nenhum `.env` real no `git status`
- [ ] `apps/web` ignorado ou ausente
- [ ] `seed-assets/catalog-products` listável pelo Git
- [ ] `dotnet restore` / `dotnet build` / `dotnet test` OK em `apps/api`
- [ ] `cd deploy && docker compose config` OK
- [ ] Nested `.git` em `apps/api/` tratado (ver §9 — pendência comum)
- [ ] README e este documento versionados
- [ ] Remote GitHub criado **manualmente** (sem secrets no histórico)

---

## 5. Como validar que `apps/web` não será commitado

```bash
git check-ignore -v apps/web
git status --short | grep 'apps/web' || echo "apps/web não aparece no status (ok)"
```

Esperado: linha do `.gitignore` ignorando `apps/web/`.

---

## 6. Como validar que `.env` real não será commitado

```bash
git check-ignore -v deploy/.env
git check-ignore -v deploy/.env.test
git check-ignore -v deploy/.env.hml
git check-ignore -v .env

# Examples DEVEM permanecer versionáveis (sem output de ignore, ou apenas se não existirem localmente):
git check-ignore -v deploy/.env.test.example || true
git check-ignore -v deploy/.env.hml.example || true
git check-ignore -v deploy/.env.example || true
git check-ignore -v .env.example || true
```

Esperado:

- `.env`, `.env.test`, `.env.hml` → **ignorados**
- `*.example` → **não** ignorados

Se um `.env` já estiver no índice:

```bash
git rm --cached -- .env deploy/.env deploy/.env.test deploy/.env.hml 2>/dev/null || true
```

---

## 7. Como validar que `seed-assets` foi incluído

```bash
git check-ignore -v apps/api/seed-assets/catalog-products/camiseta-basica-branca.png || true
git ls-files apps/api/seed-assets/catalog-products
# ou, antes do primeiro commit:
git add -n apps/api/seed-assets/catalog-products | head
```

Esperado: PNG(s) **não** ignorados e listáveis para commit.

---

## 8. Como criar o repo no GitHub manualmente

1. Em https://github.com/new, crie um repositório **vazio** (sem README/license gerados pelo GitHub, se for fazer o primeiro push a partir desta árvore).
2. Nome sugerido: `shopflow-back` (ou o nome do time).
3. Visibilidade: privada, se houver secrets no histórico local antigo — preferível começar limpo.
4. **Não** faça upload de pastas com `.env` reais pela UI.

Substitua `USUARIO` e `NOME_DO_REPO_BACKEND` nos comandos abaixo.

---

## 9. Comandos sugeridos para inicializar o Git

> Estes comandos estão **documentados apenas**. Execute manualmente após revisar o checklist.

Se a pasta atual **já** é um repositório Git (caso Shopflow local), pule `git init` e use o remote desejado.

```bash
# Somente se ainda não houver repositório na raiz do backend:
git init

git status
git add .
git status
```

### Atenção: `.git` aninhado em `apps/api/`

Se existir `apps/api/.git`, o Git da **raiz** pode tratar `apps/api` como submódulo/gitlink e **não** versionar o código-fonte. Antes do primeiro `git add` na raiz:

1. Confirme que o remote desejado será o da **raiz** (este repo backend completo).
2. Remova apenas o repositório aninhado (arquivos do código permanecem):

```bash
# CUIDADO: remove o histórico/remoto aninhado em apps/api, não o código
rm -rf apps/api/.git
```

3. Em seguida `git add apps/api`.

---

## 10. Comandos sugeridos para adicionar remote

```bash
git branch -M main
git remote add origin git@github.com:USUARIO/NOME_DO_REPO_BACKEND.git
# ou HTTPS:
# git remote add origin https://github.com/USUARIO/NOME_DO_REPO_BACKEND.git
```

Se `origin` já existir e apontar para outro lugar:

```bash
git remote -v
# git remote set-url origin git@github.com:USUARIO/NOME_DO_REPO_BACKEND.git
```

---

## 11. Comandos sugeridos para o primeiro push

Valide de novo:

```bash
git status --short
git check-ignore -v deploy/.env.test
git check-ignore -v deploy/.env.hml
git check-ignore -v apps/web
git ls-files apps/api/seed-assets/catalog-products
```

Commit e push:

```bash
git commit -m "chore: initial backend repository setup"
git push -u origin main
```

---

## 12. Cuidados com secrets

- Nunca cole connection strings reais, senhas de admin da VPS ou tokens Cloudflare no Git.
- Rotacione qualquer credencial que tenha sido commitada por engano (Postgres, admin seed, DataProtection).
- Na VPS, copie só a partir dos `.example`:

```bash
cd deploy
cp .env.example .env
cp .env.test.example .env.test
cp .env.hml.example .env.hml
# edite valores fortes localmente na VPS
```

- Prefira `git status --short` antes de cada commit.
- PaymentsPix permanece Fake/Pending; não há necessidade de secrets de Mercado Pago neste momento.

---

## Referências

- [RUNBOOK-001-vps-setup-deploy.md](./RUNBOOK-001-vps-setup-deploy.md)
- [DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md](./DEPLOY-003-validacao-admin-customer-worker-demo-catalog.md)
- [deploy/README.md](../../deploy/README.md)
- [README.md](../../README.md) (raiz do backend)
