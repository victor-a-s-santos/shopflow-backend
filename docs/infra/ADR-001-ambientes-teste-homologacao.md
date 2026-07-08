# ADR-001 — Infraestrutura de Teste e Homologação do Shopflow

## Status

Aprovado para implementação inicial.

## Contexto

O Shopflow precisa de ambientes reais de teste e homologação usando domínio próprio, HTTPS, API publicada, frontend separado e banco persistente, mas sem aumentar complexidade ou custo neste momento.

A prioridade agora é validar o fluxo real do e-commerce com baixo custo, sem Kubernetes, AWS complexa, banco gerenciado caro ou arquitetura de produção prematura.

## Decisão

A infraestrutura inicial será composta por:

- Frontend React/Vite no Cloudflare Pages
- Backend .NET em uma VPS
- Postgres na mesma VPS
- Docker Compose para orquestração
- Caddy como reverse proxy e SSL automático
- Cloudflare para DNS, CDN e SSL
- GitHub Actions via SSH ou deploy manual inicialmente

## Ambientes

### Teste

Frontend:

```text
teste.seudominio.com.br