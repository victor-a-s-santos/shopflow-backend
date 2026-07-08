# ADR-002: Deploy de teste e homologação com Docker Compose em VPS única

**Status:** Aceito  
**Data:** 2026-06-30  
**Contexto:** Shopflow aguarda confirmação do domínio final no Registro.br. Enquanto isso, usamos `vipassessoriadigital.com.br` como placeholder.

## Decisão

Publicar os ambientes **teste** e **homologação** em uma única VPS usando:

- **Docker Compose** para orquestração local dos serviços
- **Caddy** como reverse proxy com TLS automático (Let's Encrypt)
- **PostgreSQL 16** compartilhado, com bancos isolados por ambiente

Não inclui produção, Kubernetes, AWS, RDS, ECS, Cloudflare ou CI/CD automatizado neste momento.

## Ambientes e domínios (placeholder)

| Ambiente | Frontend (futuro) | API |
|----------|-------------------|-----|
| Teste | `teste.vipassessoriadigital.com.br` | `api-teste.vipassessoriadigital.com.br` |
| Homologação | `hml.vipassessoriadigital.com.br` | `api-hml.vipassessoriadigital.com.br` |

Nesta fase, o Caddy expõe apenas os hosts de API. O frontend será adicionado quando houver build estático ou container web para cada ambiente.

## Arquitetura

```
Internet :80/:443
        │
        ▼
    ┌─────────┐
    │  Caddy  │
    └────┬────┘
         │
    ┌────┴────────────────┐
    ▼                     ▼
api-test:8080       api-hml:8080
    │                     │
    └──────────┬──────────┘
               ▼
         postgres:5432
    shopflow_test │ shopflow_hml
```

## Motivação

1. **Custo e simplicidade** — uma VPS atende teste e homologação sem overhead de orquestrador.
2. **Paridade com desenvolvimento** — o monorepo já usa Docker Compose e Postgres localmente.
3. **TLS sem configuração manual** — Caddy obtém e renova certificados quando o DNS apontar para a VPS.
4. **Isolamento lógico** — APIs e bancos separados no mesmo host, suficiente para pré-produção.
5. **Deploy manual preparado** — scripts em `/deploy/scripts` permitem evoluir para GitHub Actions depois.

## Detalhes técnicos

### Portas

- **Caddy:** 80 e 443 (públicas)
- **API (teste e hml):** 8080 apenas na rede interna do Compose
- **Postgres:** 5432 apenas na rede interna (não publicada)

### Banco de dados

Um único container Postgres com dois databases:

- `shopflow_test`
- `shopflow_hml`

Criados via script de init em `/deploy/postgres/init-databases.sql`.

### Variáveis de ambiente

Três arquivos, cada um com escopo definido:

| Arquivo | Escopo |
|---------|--------|
| `.env` | Credenciais do Postgres — interpolação no `docker-compose.yml` |
| `.env.test` | Connection strings e uploads do `api-test` |
| `.env.hml` | Connection strings e uploads do `api-hml` |

Apenas os arquivos `.example` entram no repositório. Secrets reais ficam em `.env`, `.env.test` e `.env.hml` (listados no `.gitignore`).

`ASPNETCORE_ENVIRONMENT` é definido no `docker-compose.yml`: `Testing` para `api-test`, `Staging` para `api-hml`.

**Separação por `env_file` (não `profiles`):** teste e homologação rodam simultaneamente na mesma VPS. Profiles seriam adequados para ambientes mutuamente exclusivos; aqui cada API recebe seu próprio `env_file`.

### Migrations

A API aplica migrations automaticamente no startup (`Program.cs`). Os scripts `migrate-*.sh` reiniciam o serviço correspondente para forçar a aplicação após atualização de imagem.

### CORS e uploads

- `Uploads__PublicBaseUrl` deve apontar para a URL pública da API de cada ambiente.
- O CORS da API ainda referencia `http://localhost:8080` — ao publicar o frontend nos subdomínios `teste` e `hml`, será necessário ampliar as origens permitidas (fora do escopo desta ADR).

## Alternativas consideradas

| Alternativa | Motivo da rejeição |
|-------------|-------------------|
| VPS separadas por ambiente | Custo dobrado sem ganho proporcional em pré-produção |
| Kubernetes / ECS | Complexidade desnecessária para dois ambientes não produtivos |
| RDS / banco gerenciado | Custo e acoplamento cloud antes da definição de produção |
| Nginx + Certbot | Mais configuração manual que Caddy para o mesmo resultado |
| Cloudflare na frente | Domínio ainda pendente; adiar para fase posterior |

## Consequências

### Positivas

- Infra reproduzível e versionada em `/deploy`
- Subida local possível antes do DNS definitivo
- Caminho claro para automação (GitHub Actions) e para produção futura

### Negativas / riscos

- VPS única: falha de host derruba teste e homologação juntos
- Postgres compartilhado: carga de um ambiente pode afetar o outro
- Deploy manual depende de disciplina operacional até existir pipeline

## Próximos passos (fora desta ADR)

- Confirmar domínio no Registro.br e atualizar DNS
- Publicar frontend em `teste` e `hml`
- Ajustar CORS para origens de produção de teste/hml
- Avaliar Cloudflare (WAF, cache) após DNS estável
- ADR separada para ambiente de produção
