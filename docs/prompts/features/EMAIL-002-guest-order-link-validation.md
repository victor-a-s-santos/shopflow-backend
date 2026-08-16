Você está atuando como engenheiro sênior do Shopflow (backend .NET + frontend React/Vite).

Implemente a feature:

docs/features/EMAIL-002-guest-order-link-validation.md

O e-mail transacional EMAIL-001 já envia:

```
{PublicApp}/pedido/{orderNumber}?t={guestAccessToken}
```

Em TESTE o e-mail chegou (pedido 10022), mas a loja mostrou:

“Não foi possível abrir este pedido. O link pode ter expirado ou ser inválido.”

==================================================
1. CRIAR / MANTER O EMAIL-002
==================================================

Criar (se ainda não existir):

docs/features/EMAIL-002-guest-order-link-validation.md

Documentar:

- link canônico do e-mail (`?t=`)
- API pública `GET /api/orders/public/{orderNumber}`
- header `X-ORDER-ACCESS-TOKEN` preferencial
- query `t` (e-mail) e `token` (legado) na API
- 401 opaco, sem enumerar pedido
- hidratação no FE (`/pedido/:orderNumber`)
- persistir em sessionStorage e remover `t` da URL
- nunca logar o token
- checklist TESTE

Não reprocessar outbox Skipped. Não reemitir guestAccessToken. Não reusar pedidos 10020/10021.

==================================================
2. BACKEND
==================================================

Ler o contrato real em:

- `GET /api/orders/public/{orderNumber}`
- `GET /api/orders/guest/{orderId}/status`
- `GuestOrderAccessGate`
- `TransactionalEmailTemplates.BuildOrderLink` (deve continuar com `?t=`)

Implementar:

- Resolver o token nesta ordem: header `X-ORDER-ACCESS-TOKEN` → query `t` → query `token`
- Usar o mesmo resolver nos dois GETs públicos
- Rate limit inalterado (`guest-order-status`)
- Resposta de falha continua 401 `"Order access denied."`
- Testes unitários do resolver (header vence `t`; `t` vence `token`; vazio → null)

Não criar endpoint novo. Não mudar o HTML do e-mail. Não logar ApiKey nem o token.

==================================================
3. FRONTEND (`apps/web` / Cloudflare Pages)
==================================================

Causa confirmada no bundle de TESTE: `/pedido/:orderNumber` só lê

- `location.state.guestAccessToken`
- `sessionStorage` `shopflow.guestOrderAccess.v2.{orderNumber}`

Não lê `searchParams.t`. Por isso o link do Gmail falha antes da API.

Na página `data-cy="public-order-page"`:

1. Resolver token: state → query `t` → query `token` → sessionStorage v2 → fallback v1 por orderId se já existir.
2. Se veio da query: persistir no helper do checkout e **tirar `t`/`token` da URL** (`replace`), barra = `/pedido/{orderNumber}`.
3. Chamar `GET /orders/public/{orderNumber}` com header `X-ORDER-ACCESS-TOKEN`.
4. Sem token → mensagem opaca atual, sem fetch.
5. 401/403/404/400 → mensagem opaca. 429 → rate limit.
6. Nunca logar/exibir o token raw.

Não chamar `GET /api/orders/{guid}` nem endpoints admin/Pix backoffice.
Não mudar o formato do e-mail.

Este repositório (`shopflow-backend`) **não contém** `apps/web`. Se o FE estiver noutro repo, aplicar a seção 3 lá.

==================================================
4. TESTES
==================================================

Backend:

- locator: header / `t` / `token` / vazio
- e-mail guest continua contendo `t=`

Frontend:

- `/pedido/{n}?t=fake` sem sessionStorage **dispara** a API com o header (401 ok)
- `/pedido/{n}` sem token e sem storage **não** chama a API
- depois de hidratar, reload sem query usa sessionStorage

TESTE (depois do deploy FE):

- abrir o link do e-mail OrderCreated num browser sem sessionStorage da compra
- pedido abre (Pix pendente, total, acompanhar)
- Network: `/api/orders/public/{n}` + header; token não em logs
- pedido novo (não 10020/10021). 10022 é só evidência do bug antigo

==================================================
5. FORA DE ESCOPO
==================================================

- RabbitMQ / MassTransit
- reemissão de token
- magic link pós-perda
- confirm-email / reset-password (`?token=` é outro fluxo)
- HML/PROD deploy
- reprocessar Skipped
- Mercado Pago / fulfillment

==================================================
6. SAÍDA ESPERADA
==================================================

1. Spec em `docs/features/EMAIL-002-guest-order-link-validation.md`
2. Arquivos backend alterados
3. Testes backend executados
4. Contrato FE (e patch FE se o código da loja estiver acessível)
5. Como validar o link do e-mail em TESTE
6. Riscos: token em histórico/Referer até o replace da URL
