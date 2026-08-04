# RUNBOOK-002 — Deploy do frontend no Cloudflare Pages

Runbook operacional para publicar o frontend Shopflow (`apps/web`) nos ambientes **teste** e **homologação**.

**Decisões de arquitetura:** [ADR-001-ambientes-teste-homologacao.md](./ADR-001-ambientes-teste-homologacao.md)

**Backend (API na VPS):** [RUNBOOK-001-vps-setup-deploy.md](./RUNBOOK-001-vps-setup-deploy.md)

---

## Visão geral

| Ambiente | Domínio frontend | Branch Git | API |
|----------|------------------|------------|-----|
| Teste | https://teste.vipassessoriadigital.com.br | `develop` | https://api-teste.vipassessoriadigital.com.br |
| Homologação | https://hml.vipassessoriadigital.com.br | `staging` | https://api-hml.vipassessoriadigital.com.br |

O frontend é uma SPA (React + Vite). O arquivo `apps/web/public/_redirects` garante que rotas client-side (ex.: `/admin`, `/produto/:slug`) retornem `index.html` com status 200.

As chamadas à API usam `VITE_API_BASE_URL`, injetada em **build time** pelo Vite. Não há URL de localhost em builds de produção — se a variável não estiver definida, o build falha.

---

## Pré-requisitos

- Repositório Shopflow conectado ao GitHub (ou GitLab) com branches `develop` e `staging`.
- Conta Cloudflare com o domínio `vipassessoriadigital.com.br` gerenciado na zona DNS.
- APIs de teste e hml acessíveis e com CORS permitindo as origens dos frontends (ver seção [CORS](#cors)).
- Node.js **20+** (usado pelo Cloudflare Pages no build).

---

## Variáveis de ambiente

Referência local (não commitar `.env.test` / `.env.hml` com secrets):

| Arquivo exemplo | Uso |
|-----------------|-----|
| [apps/web/.env.test.example](../../apps/web/.env.test.example) | Ambiente teste |
| [apps/web/.env.hml.example](../../apps/web/.env.hml.example) | Ambiente homologação |
| [apps/web/.env.example](../../apps/web/.env.example) | Desenvolvimento local |

### Teste

```env
VITE_API_BASE_URL=https://api-teste.vipassessoriadigital.com.br/api
VITE_APP_ENV=test
```

### Homologação

```env
VITE_API_BASE_URL=https://api-hml.vipassessoriadigital.com.br/api
VITE_APP_ENV=hml
```

`VITE_APP_ENV` é informativa (build-time); a URL da API é definida por `VITE_API_BASE_URL`.

---

## Estratégia: dois projetos no Cloudflare Pages

Recomenda-se **um projeto Pages por ambiente**, cada um com branch de produção distinta e domínio customizado.

| Projeto (sugestão) | Production branch | Domínio customizado |
|--------------------|-------------------|---------------------|
| `shopflow-web-teste` | `develop` | `teste.vipassessoriadigital.com.br` |
| `shopflow-web-hml` | `staging` | `hml.vipassessoriadigital.com.br` |

---

## Passo a passo — projeto Teste

### 1. Criar o projeto

1. Acesse [Cloudflare Dashboard](https://dash.cloudflare.com) → **Workers & Pages** → **Create** → **Pages** → **Connect to Git**.
2. Selecione o repositório **Shopflow**.
3. Nome do projeto: `shopflow-web-teste` (ou equivalente).

### 2. Configuração de build

Na tela de configuração (ou em **Settings → Builds & deployments** após criar):

| Campo | Valor |
|-------|--------|
| **Production branch** | `develop` |
| **Framework preset** | `Vite` (ou None) |
| **Root directory** | `apps/web` |
| **Build command** | `npm ci && npm run build` |
| **Build output directory** | `dist` |

Em **Environment variables** (escopo **Production**):

| Nome | Valor |
|------|--------|
| `VITE_API_BASE_URL` | `https://api-teste.vipassessoriadigital.com.br/api` |
| `VITE_APP_ENV` | `test` |
| `NODE_VERSION` | `20` |
| `VITE_SUPPORT_WHATSAPP_ENABLED` | `true` |
| `VITE_SUPPORT_WHATSAPP_PHONE` | número real só dígitos (ex. `5511…`) — **nunca** `55DDDNUMERO` / `5511999999999` |

`VITE_*` entra no **build**. Qualquer mudança de telefone exige **Retry deployment** / novo build.

Salve e dispare o primeiro deploy (push em `develop` ou **Retry deployment**).

### 3. Domínio customizado

1. No projeto → **Custom domains** → **Set up a custom domain**.
2. Informe `teste.vipassessoriadigital.com.br`.
3. O Cloudflare cria/atualiza o registro DNS (geralmente CNAME para `*.pages.dev`).

### 4. Validar deploy

```bash
# SPA responde
curl -sS -o /dev/null -w "%{http_code}\n" https://teste.vipassessoriadigital.com.br/

# Rota client-side (deve retornar 200 com HTML, não 404)
curl -sS -o /dev/null -w "%{http_code}\n" https://teste.vipassessoriadigital.com.br/admin

# API acessível (health sem prefixo /api)
curl -sS https://api-teste.vipassessoriadigital.com.br/health
```

No navegador: abrir a loja, login admin (se aplicável) e confirmar que requisições vão para `api-teste.vipassessoriadigital.com.br` (aba Network).

---

## Passo a passo — projeto Homologação

Repita o processo com estes valores:

| Campo | Valor |
|-------|--------|
| Nome do projeto | `shopflow-web-hml` |
| **Production branch** | `staging` |
| **Root directory** | `apps/web` |
| **Build command** | `npm ci && npm run build` |
| **Build output directory** | `dist` |

Variáveis (**Production**):

| Nome | Valor |
|------|--------|
| `VITE_API_BASE_URL` | `https://api-hml.vipassessoriadigital.com.br/api` |
| `VITE_APP_ENV` | `hml` |
| `NODE_VERSION` | `20` |
| `VITE_SUPPORT_WHATSAPP_ENABLED` | `true` |
| `VITE_SUPPORT_WHATSAPP_PHONE` | número real só dígitos — não usar placeholder |

Domínio customizado: `hml.vipassessoriadigital.com.br`.

Validação:

```bash
curl -sS -o /dev/null -w "%{http_code}\n" https://hml.vipassessoriadigital.com.br/
curl -sS https://api-hml.vipassessoriadigital.com.br/health
```

---

## Build local (validação antes do deploy)

Na raiz do monorepo ou em `apps/web`:

```bash
cd apps/web
npm ci
VITE_API_BASE_URL=https://api-teste.vipassessoriadigital.com.br/api VITE_APP_ENV=test npm run build
```

Artefatos em `apps/web/dist/`. Preview local:

```bash
npm run preview
```

Build com modo Vite (carrega `.env.test` se existir, copiado do `.env.test.example`):

```bash
cp .env.test.example .env.test   # apenas local, não commitar
npm run build -- --mode test
```

---

## CORS

O frontend e a API estão em subdomínios diferentes. A API deve incluir nas origens CORS permitidas:

- `https://teste.vipassessoriadigital.com.br`
- `https://hml.vipassessoriadigital.com.br`

Requisições usam `credentials: "include"` (cookies de sessão admin). O backend precisa responder com `Access-Control-Allow-Credentials: true` e origem explícita (não `*`).

Configuração da API: fora do escopo deste runbook (ver deploy/backend na VPS).

---

## SPA e `_redirects`

Arquivo versionado: [apps/web/public/_redirects](../../apps/web/public/_redirects)

```text
/* /index.html 200
```

O Vite copia `public/` para `dist/` no build. Sem essa regra, refresh em rotas como `/admin/produtos` retornaria 404 no Pages.

---

## Troubleshooting

| Sintoma | Causa provável | Ação |
|---------|----------------|------|
| Build falha com `VITE_API_BASE_URL must be set` | Variável ausente no Pages | Conferir env vars em Production |
| API calls vão para localhost | Build sem `VITE_API_BASE_URL` | Refazer deploy após corrigir variáveis |
| CORS / cookies não funcionam | Origem não liberada na API | Atualizar CORS no backend |
| 404 ao atualizar página em rota interna | `_redirects` ausente no `dist` | Verificar `public/_redirects` e rebuild |
| Assets 404 | `Root directory` errado | Deve ser `apps/web`, output `dist` |

---

## Deploy contínuo

Cada push na branch de produção do projeto dispara build automático:

- `develop` → projeto teste → `teste.vipassessoriadigital.com.br`
- `staging` → projeto hml → `hml.vipassessoriadigital.com.br`

Não é necessário GitHub Actions para o frontend nesta fase (integração nativa Pages + Git).

---

## Escopo fora deste runbook

- Ambiente de **produção** (domínio final da loja).
- Alterações no backend ou banco.
- Pipelines GitHub Actions para o frontend.
