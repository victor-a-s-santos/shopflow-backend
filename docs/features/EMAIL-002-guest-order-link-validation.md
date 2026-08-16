# EMAIL-002 — Validação do link guest do e-mail (`/pedido/{n}?t=`)

O e-mail transacional de pedido criado (EMAIL-001) aponta para:

```
{PublicApp}/pedido/{orderNumber}?t={guestAccessToken}
```

Em TESTE isso chegou na caixa (pedido **10022**), mas a página pública mostrou *“Não foi possível abrir este pedido. O link pode ter expirado ou ser inválido.”*

## Causa

O backend já tem acompanhamento guest:

| Canal | Contrato |
|--------|----------|
| Preferido | `GET /api/orders/public/{orderNumber}` |
| Legado | `GET /api/orders/guest/{orderId}/status` |
| Credencial | header `X-ORDER-ACCESS-TOKEN` (preferencial), query `t` (e-mail) ou `token` (legado) |
| Falha | **401** opaco `"Order access denied."` (token ausente/inválido/expirado/número errado — sem enumerar pedido) |

O frontend de TESTE (`/pedido/:orderNumber`) **não lê `?t=`**. Só aceita token de:

1. `location.state.guestAccessToken` (botão “Acompanhar pedido” no checkout)
2. `sessionStorage` `shopflow.guestOrderAccess.v2.{orderNumber}` (mesmo browser da compra)

Abrir o link do Gmail (outro dispositivo, aba anônima, ou depois que o `sessionStorage` sumiu) falha **antes** de chamar a API, com a mesma mensagem de link inválido.

SEC-006 pediu para não colocar token em query na API. O e-mail **precisa** de query no link da loja; a API continua preferindo header. O FE deve hidratar `t`, gravar em `sessionStorage` e chamar a API com o header — depois remover `t` da URL (`history.replaceState`) para reduzir vazamento via `Referer`.

## Backend (este repositório)

- `GuestOrderAccessTokenLocator`: header → `t` → `token`
- `GET /api/orders/public/{orderNumber}` e `GET /api/orders/guest/{id}/status` usam o locator
- E-mail continua com `?t=` (`TransactionalEmailTemplates.BuildOrderLink`)
- Rate limit `guest-order-status` inalterado
- Nunca logar o token raw

Não reprocessar outbox `Skipped` antigos. Não reemitir `guestAccessToken` em GETs admin.

## Frontend (repositório `apps/web` / Cloudflare Pages — fora deste backend)

Prompt operacional: [`docs/prompts/features/EMAIL-002-guest-order-link-frontend-cursor.md`](../prompts/features/EMAIL-002-guest-order-link-frontend-cursor.md)

Contrato mínimo da página `/pedido/:orderNumber`:

1. Resolver token nesta ordem: `location.state.guestAccessToken` → query `t` → query `token` → `sessionStorage` v2 (e v1 por `orderId` se já existir).
2. Se veio da query, persistir com o helper já usado no checkout (`shopflow.guestOrderAccess.v2.{orderNumber}`) e **tirar `t`/`token` da URL** sem recarregar.
3. Chamar `GET /api/orders/public/{orderNumber}` com `X-ORDER-ACCESS-TOKEN`. **Não** depender da API receber query no browser (o header basta).
4. Sem token após o passo 1 → a mensagem atual de link inválido (não chamar a API).
5. 401/403/404/400 da API → a mesma mensagem opaca. 429 → mensagem de rate limit.
6. Nunca `console.log` / analytics com o token; não mandar `t` em `Referer` de terceiros depois do replace.

## Checklist TESTE

- [ ] Abrir o link do e-mail **OrderCreated** num browser **sem** `sessionStorage` da compra → pedido abre (Pix pendente, total, acompanhar).
- [ ] Recarregar a mesma URL já sem `?t=` (depois do replace) → continua abrindo via `sessionStorage`.
- [ ] URL adulterada (`t` errado ou `orderNumber` de outro pedido) → mensagem opaca, sem vazar existência.
- [ ] Pedido logado (`/account/orders/{orderId}` no e-mail) não usa `?t=`.
- [ ] Network: request vai para `/api/orders/public/{n}` com header; token não aparece em logs da API.

Pedido **10022** é evidência do bug antigo; validar com um pedido **novo** depois do deploy do FE (não reusar 10020/10021).
