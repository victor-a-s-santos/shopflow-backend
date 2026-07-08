# RUNBOOK-001 — Setup, deploy e validação na VPS

Runbook operacional para ambientes **teste** e **homologação** do Shopflow.

**Decisões de arquitetura:** [ADR-001-ambientes-teste-homologacao.md](./ADR-001-ambientes-teste-homologacao.md), [ADR-002-deploy-docker-compose-vps.md](./ADR-002-deploy-docker-compose-vps.md)

**Deploy (Compose, scripts, variáveis):** [deploy/README.md](../../deploy/README.md)

---

## Ambientes

| Ambiente | URL da API | Banco | `ASPNETCORE_ENVIRONMENT` |
|----------|------------|-------|--------------------------|
| Teste | https://api-teste.vipassessoriadigital.com.br | `shopflow_test` | `Testing` |
| Homologação | https://api-hml.vipassessoriadigital.com.br | `shopflow_hml` | `Staging` |

Rotas de negócio usam o prefixo `/api`. O health check fica em **`GET /health`** (sem prefixo).

---

## Health check

Endpoint público, sem autenticação, para validar que a API está de pé.

| Item | Valor |
|------|--------|
| Método / path | `GET /health` |
| Auth | Não exigida |
| Sucesso | HTTP **200** |
| Corpo | `{ "status": "ok", "environment": "<ASPNETCORE_ENVIRONMENT>" }` |

Não expõe connection strings, secrets nem detalhes internos. Não verifica banco de dados — apenas confirma que o processo da API responde.

### Validar na VPS (produção de teste/hml)

```bash
curl -sS https://api-teste.vipassessoriadigital.com.br/health
curl -sS https://api-hml.vipassessoriadigital.com.br/health
```

Resposta esperada (exemplo):

```json
{"status":"ok","environment":"Testing"}
```

```json
{"status":"ok","environment":"Staging"}
```

Com código HTTP:

```bash
curl -sS -o /dev/null -w "%{http_code}\n" https://api-teste.vipassessoriadigital.com.br/health
curl -sS -o /dev/null -w "%{http_code}\n" https://api-hml.vipassessoriadigital.com.br/health
```

Ambos devem retornar `200`.

### Validar localmente (monorepo dev)

Com `docker compose up` na raiz do monorepo (API em `localhost:5127`):

```bash
curl -sS http://localhost:5127/health
```

Resposta esperada em Development:

```json
{"status":"ok","environment":"Development"}
```

### Validar localmente (stack `deploy/`)

Com a stack de teste/hml no diretório `deploy/`:

```bash
cd deploy
docker compose up -d --build

# Dentro do container (porta interna 8080)
docker compose exec api-test wget -qO- http://localhost:8080/health
docker compose exec api-hml wget -qO- http://localhost:8080/health
```

Com `/etc/hosts` apontando os hostnames para `127.0.0.1` e Caddy com TLS:

```bash
curl -sk https://api-teste.vipassessoriadigital.com.br/health
curl -sk https://api-hml.vipassessoriadigital.com.br/health
```

---

## CORS (preflight)

A API lê origens permitidas de `AllowedOrigins` (env: `AllowedOrigins__0`, `AllowedOrigins__1`, …).  
Cada ambiente deve listar **apenas** o frontend correspondente:

| Ambiente | Variável | Valor |
|----------|----------|-------|
| Teste | `AllowedOrigins__0` | `https://teste.vipassessoriadigital.com.br` |
| Homologação | `AllowedOrigins__0` | `https://hml.vipassessoriadigital.com.br` |

Após alterar `.env.test` ou `.env.hml`, rebuild e restart do serviço:

```bash
cd deploy
./scripts/deploy-test.sh   # ou deploy-hml.sh
```

### Teste manual de preflight (OPTIONS)

Simula o que o navegador envia antes de um `fetch` cross-origin.

**Teste:**

```bash
curl -sS -D - -o /dev/null -X OPTIONS \
  'https://api-teste.vipassessoriadigital.com.br/api/catalog/products' \
  -H 'Origin: https://teste.vipassessoriadigital.com.br' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Access-Control-Request-Headers: content-type'
```

**Homologação:**

```bash
curl -sS -D - -o /dev/null -X OPTIONS \
  'https://api-hml.vipassessoriadigital.com.br/api/catalog/products' \
  -H 'Origin: https://hml.vipassessoriadigital.com.br' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Access-Control-Request-Headers: content-type'
```

Resposta esperada:

- HTTP **204** (ou **200**)
- Header `Access-Control-Allow-Origin: <origem do frontend>`
- Header `Access-Control-Allow-Credentials: true` (auth por cookie)

Confirme que a origem **não** autorizada é rejeitada:

```bash
curl -sS -D - -o /dev/null -X OPTIONS \
  'https://api-teste.vipassessoriadigital.com.br/api/catalog/products' \
  -H 'Origin: https://evil.example' \
  -H 'Access-Control-Request-Method: GET'
```

Não deve retornar `Access-Control-Allow-Origin: https://evil.example`.

---

## Admin seed

Em **Testing** e **Staging**, `SHOPFLOW_ADMIN_EMAIL` e `SHOPFLOW_ADMIN_PASSWORD` são **obrigatórios**. A API falha no startup se ausentes.

| Ambiente | Arquivo | E-mail | Senha |
|----------|---------|--------|-------|
| Teste (`Testing`) | `deploy/.env.test` | `admin-teste@vipassessoriadigital.com.br` | Forte, definida na VPS |
| Homologação (`Staging`) | `deploy/.env.hml` | `admin-hml@vipassessoriadigital.com.br` | Forte, definida na VPS |
| Development (local) | `.env` na raiz | Qualquer (ex.: `.env.example`) | Opcional — seed só roda se configurado |

Requisitos da senha: mínimo 8 caracteres, 1 dígito, 1 minúscula.

Após alterar `.env.test` ou `.env.hml`:

```bash
cd deploy
./scripts/deploy-test.sh   # ou deploy-hml.sh
```

### Validar variáveis no container

Confirme que e-mail e senha entraram no ambiente (senha aparece, mas não logue em produção):

```bash
cd deploy

# Teste
docker compose exec api-test printenv SHOPFLOW_ADMIN_EMAIL
docker compose exec api-test printenv SHOPFLOW_ADMIN_PASSWORD

# Homologação
docker compose exec api-hml printenv SHOPFLOW_ADMIN_EMAIL
docker compose exec api-hml printenv SHOPFLOW_ADMIN_PASSWORD
```

Saída esperada: e-mails `admin-teste@...` e `admin-hml@...`; senha não vazia.

Confirme seed nos logs (primeiro startup):

```bash
docker compose logs api-test 2>&1 | rg -i 'Admin user seeded|Admin seed skipped'
docker compose logs api-hml 2>&1 | rg -i 'Admin user seeded|Admin seed skipped'
```

Mensagem esperada na primeira subida: `Admin user seeded for admin-teste@vipassessoriadigital.com.br`.

### Validar login admin

```bash
curl -sS -X POST 'https://api-teste.vipassessoriadigital.com.br/api/auth/admin/login' \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin-teste@vipassessoriadigital.com.br","password":"<SENHA_DA_VPS>"}'
```

Resposta esperada: HTTP **200** com dados do usuário admin.

### Redefinir senha do admin existente

Por padrão, se o usuário admin já existe, o seed **não altera** a senha (`Admin seed skipped: user ... already exists`).

Para redefinir a senha via variável de ambiente (procedimento explícito e temporário):

1. Edite `.env.test` ou `.env.hml`:
   - Atualize `SHOPFLOW_ADMIN_PASSWORD` com a **nova** senha forte
   - Defina `SHOPFLOW_ADMIN_RESET_PASSWORD=true`
2. Reinicie a API:
   ```bash
   cd deploy
   ./scripts/deploy-test.sh   # ou deploy-hml.sh
   ```
3. Confirme nos logs:
   ```bash
   docker compose logs api-test 2>&1 | rg -i 'Admin password reset'
   ```
4. Teste login com a nova senha (comando acima).
5. **Desative o reset** — volte `SHOPFLOW_ADMIN_RESET_PASSWORD=false` (ou remova a linha) e reinicie:
   ```bash
   cd deploy
   ./scripts/deploy-test.sh   # ou deploy-hml.sh
   ```

Mantenha `SHOPFLOW_ADMIN_RESET_PASSWORD=false` no dia a dia. Use `true` apenas durante rotação de senha.

---

## Checklist pós-deploy

1. `docker compose ps` — todos os serviços `running`
2. `GET /health` retorna **200** em teste e hml (comandos acima)
3. `GET /api/catalog/categories` retorna **200** (smoke de rota pública de negócio)
4. Preflight CORS (`OPTIONS`) retorna `Access-Control-Allow-Origin` correto (comandos na seção CORS)
5. `SHOPFLOW_ADMIN_EMAIL` e `SHOPFLOW_ADMIN_PASSWORD` presentes no container (`printenv`)
6. Logs confirmam admin seed ou usuário já existente
7. Logs sem erro de migration: `docker compose logs -f api-test` / `api-hml`

---

## Troubleshooting rápido

| Sintoma | Ação |
|---------|------|
| `/health` timeout ou connection refused | Verificar Caddy, container da API e firewall (80/443) |
| `/health` 502 | API ainda subindo ou crash no startup — ver `docker compose logs api-test` |
| `environment` inesperado | Conferir `ASPNETCORE_ENVIRONMENT` em `deploy/docker-compose.yml` |
| TLS inválido localmente | Usar `curl -k` ou acesso direto ao container na porta 8080 |
| CORS bloqueado no browser | Conferir `AllowedOrigins__0` em `.env.test` / `.env.hml` e refazer deploy da API |
| Preflight sem `Access-Control-Allow-Origin` | Restart `api-test` ou `api-hml` após atualizar `.env.*`; validar com `curl -X OPTIONS` acima |
| API crash: admin seed required | Definir `SHOPFLOW_ADMIN_EMAIL` e `SHOPFLOW_ADMIN_PASSWORD` em `.env.test` / `.env.hml` e redeploy |
| Senha admin não muda após alterar `.env` | Usuário já existe — ativar `SHOPFLOW_ADMIN_RESET_PASSWORD=true` temporariamente (ver seção Admin seed) |

---

## Implementação no código

- Endpoint: `apps/api/ApiGateways/Vls.Shopflow.HttpApi/Endpoints/HealthEndpoints.cs`
- Registro: `Program.cs` → `app.MapHealthEndpoints()`
- CORS: `Program.cs` → `GetSection("AllowedOrigins")`, `UseCors` antes de auth/endpoints
- Admin seed: `IdentityAccessDbContextSeed.cs` → exige `SHOPFLOW_ADMIN_*` fora de Development
- Teste: `EndpointExposureIntegrationTests.Health_WithoutLogin_Returns200WithOkStatus`
