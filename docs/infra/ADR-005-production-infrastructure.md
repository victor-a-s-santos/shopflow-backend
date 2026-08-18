# ADR-005 — Infraestrutura de Produção

## Status
Aceito. Implementação no repositório (sem deploy).

Arquivos: `deploy/docker-compose.prod.yml`, `deploy/caddy/Caddyfile.prod`, `deploy/.env.prod.example`, `deploy/scripts/deploy-prod.sh`, `.github/workflows/deploy-prod.yml`.

Operação: [RUNBOOK-006-production-vps-setup.md](./RUNBOOK-006-production-vps-setup.md).

## Contexto

O Shopflow está próximo da conclusão do MVP.

Ambientes atuais:
- TESTE e HML executados em VPS via Docker Compose.
- Frontend em repositório separado e publicado no Cloudflare Pages.
- Backend .NET.
- PostgreSQL.
- Caddy como reverse proxy.
- Workers executados separadamente da API.
- Cloudflare R2 para imagens.
- Brevo para e-mails transacionais.
- GitHub Actions para CI/CD.

Produção deve ser isolada dos ambientes TESTE/HML.

## Decisão

### Frontend
- Cloudflare Pages
- Domínio principal:
  - https://vipassessoriadigital.com.br
  - https://www.vipassessoriadigital.com.br

### Backend
VPS Hetzner exclusiva para PROD.

Docker Compose com:
- caddy
- api-prod
- worker-prod
- postgres

Não utilizar Kubernetes/K3s no MVP.

### API
Domínio:

https://api.vipassessoriadigital.com.br

Environment:

Production

### PostgreSQL
Banco exclusivo:

shopflow_prod

Persistência por Docker Volume.

Banco não deve expor porta 5432 publicamente.

### DataProtection
Volume persistente exclusivo para PROD.

Exemplo:

shopflow_dataprotection_prod

### Product Images
Cloudflare R2.

Bucket PROD separado do TESTE.

Sugestão:

shopflow-products-prod

Domínio:

https://assets.vipassessoriadigital.com.br

Storage Provider:

CloudflareR2

Nenhuma imagem PROD deve depender do filesystem efêmero da API.

### E-mail
Brevo API.

Configuração exclusiva PROD:

Brevo__Enabled=true
Brevo__SandboxMode=false

API Key PROD somente em secret/env da infraestrutura.

Nunca versionar API Key.

### Workers
Executar inicialmente em um único container worker-prod.

Inclui os processamentos atuais, conforme arquitetura vigente, incluindo:
- EmailOutboxWorker
- OrderEmailIntentDispatcher
- MercadoPagoPixReconciliationWorker
- PendingCheckoutExpirationWorker

Não introduzir RabbitMQ/MassTransit nesta etapa.

### Reverse Proxy
Caddy.

Responsável por:
- TLS
- reverse proxy da API
- renovação automática dos certificados

### DNS / Edge
Cloudflare.

Registros necessários:
- @
- www
- api
- assets

### Deploy
GitHub Actions.

Branches:

develop → TESTE
staging → HML
main → PROD

Deploy PROD deve usar secrets próprios.

O workflow de PROD não pode reutilizar secrets SSH/env específicos de TESTE/HML.

### Segurança
- VPS PROD exclusiva.
- SSH por chave.
- PasswordAuthentication desabilitado após validação.
- Firewall permitindo somente portas necessárias.
- PostgreSQL não exposto publicamente.
- `.env.prod` fora do Git.
- Secrets nunca impressos nos logs.
- Admin PROD com credenciais próprias.
- SHOPFLOW_ADMIN_RESET_PASSWORD=false normalmente.
- Brevo PROD separado.
- R2 PROD separado.

### Backup
Antes do go-live deve existir estratégia para:
- backup PostgreSQL;
- retenção;
- restauração testada.

R2 não substitui backup do banco.

### Observabilidade inicial
Para o MVP:
- Docker logs
- health endpoint
- logs estruturados da aplicação
- logs do worker
- monitoramento externo do /health

Soluções mais complexas podem ser introduzidas posteriormente.

### Escalabilidade
A arquitetura inicial prioriza simplicidade e baixo custo.

Evolução futura possível:

Cloudflare
    ↓
Load Balancer
    ↓
API x N
    ↓
PostgreSQL gerenciado

Workers também poderão ser separados por responsabilidade.

Kubernetes deverá ser reconsiderado somente quando houver necessidade concreta
de múltiplos nós, autoscaling, maior disponibilidade ou aumento significativo
da quantidade de serviços.

## Arquitetura

Cloudflare
│
├── vipassessoriadigital.com.br
│       ↓
│   Cloudflare Pages
│
├── api.vipassessoriadigital.com.br
│       ↓
│   VPS PROD
│       ├── Caddy
│       ├── api-prod
│       ├── worker-prod
│       └── postgres
│
└── assets.vipassessoriadigital.com.br
        ↓
    Cloudflare R2

External:
- Brevo
- Mercado Pago

## Princípio

Produção deve ser reproduzível, isolada e simples de operar.

Docker Compose permanece como orquestrador do MVP.
Kubernetes está explicitamente fora do escopo inicial.