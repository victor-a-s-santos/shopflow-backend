Você está atuando como engenheiro frontend sênior do Shopflow (React + TypeScript + Vite).

Implemente a hidratação do link guest do e-mail transacional.

Spec: `docs/features/EMAIL-002-guest-order-link-validation.md` (neste repositório backend; copiar o contrato se o FE estiver em outro repo).

==================================================
1. PROBLEMA
==================================================

O e-mail “Recebemos seu pedido #{n}” usa:

```
https://teste.vipassessoriadigital.com.br/pedido/{orderNumber}?t={guestAccessToken}
```

A página `/pedido/:orderNumber` hoje resolve o token só de:

- `location.state.guestAccessToken` (navegação interna pós-checkout)
- `sessionStorage` `shopflow.guestOrderAccess.v2.{orderNumber}`

Não lê `searchParams` `t`. Resultado: o link do Gmail mostra *“Não foi possível abrir este pedido. O link pode ter expirado ou ser inválido.”* mesmo com token válido (reproduzido no pedido 10022).

A API pública já existe e deve continuar sendo usada:

```
GET {VITE_API_BASE_URL}/orders/public/{orderNumber}
Header: X-ORDER-ACCESS-TOKEN: <raw token>
```

401 opaco `"Order access denied."`. Rate limit 429.

Não chamar `GET /api/orders/{guid}` nem endpoints admin/Pix backoffice.

==================================================
2. IMPLEMENTAÇÃO
==================================================

Na página pública de pedido (`data-cy="public-order-page"`):

1. Resolver token nesta ordem:
   - `location.state?.guestAccessToken`
   - query `t` (canônico do e-mail; `decodeURIComponent`)
   - query `token` (alias legado da API)
   - `sessionStorage` v2 pela `orderNumber` da rota
   - fallback v1 por `orderId` se o código atual já tiver isso
2. Se o token veio da query: persistir no mesmo helper do checkout (`shopflow.guestOrderAccess.v2.{orderNumber}`, e o índice `id.{orderId}` quando o status retornar `orderId`).
3. Remover `t` e `token` da URL com `navigate(..., { replace: true, state })` / `history.replaceState` **sem** perder o token em memória. A barra deve ficar `/pedido/{orderNumber}`.
4. Só então `GET /orders/public/{orderNumber}` com o header. `enabled` do React Query continua `!!(orderNumber && token)`.
5. Sem token → UI atual `public-order-access-denied` / missing number. Não dispara fetch.
6. Erros 401/403/404/400 → mensagem opaca atual. 429 → “Muitas consultas…”.
7. Nunca logar o token, nunca mandar para analytics, nunca exibir o raw token na UI.

Não mudar o formato do e-mail (`?t=`). Não inventar endpoint novo.

==================================================
3. TESTES
==================================================

- Cypress (ou equivalente): visitar `/pedido/10582?t=fake` sem sessionStorage → a página tenta a API com o header (pode 401; o importante é **não** cair no early-return de “sem token”).
- Visitar `/pedido/10582` sem token e sem storage → mensagem de link inválido **sem** chamar a API.
- Depois de hidratar, reload em `/pedido/10582` (sem query) usa sessionStorage.

==================================================
4. FORA DE ESCOPO
==================================================

- Reemitir token
- Magic link por e-mail depois de Created
- Backend/VPS
- Confirmar/resetar senha (`?token=` dessas páginas é outro fluxo)
- Reprocessar outbox Skipped

==================================================
5. SAÍDA
==================================================

1. Arquivos alterados
2. Como o token é resolvido
3. Como a URL é limpa
4. Como validar em TESTE com um pedido guest novo (não 10020/10021)
