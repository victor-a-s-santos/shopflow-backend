# Guest Order Claim (pós-Pix)

Vincula um pedido **guest** a uma conta customer usando `GuestOrderAccessToken` como prova de posse.

## Por que não aparece por e-mail

`GET /api/customer/orders` filtra **somente** por `Order.CustomerUserId`.  
Pedidos guest (`CustomerUserId = null`) **não** entram em “Meus pedidos”, mesmo com o mesmo e-mail da conta. Isso evita que alguém com conta no e-mail do checkout veja pedidos sem provar posse (token).

## Guest status vs Customer Orders

| API | Auth | Uso |
|-----|------|-----|
| `GET /api/orders/guest/{orderId}/status` | `X-ORDER-ACCESS-TOKEN` | Acompanhar Pix / status (convidado) |
| `GET /api/customer/orders` | CustomerCookie | “Meus pedidos” (só vinculados) |
| Claim endpoints abaixo | token e/ou cookie | Vincular guest → conta |

## Fluxo pós-Pix aprovado

1. Cliente tem `orderId` + `guestAccessToken` (localStorage / retorno do create order).
2. **Sem conta:** `POST .../create-account` com senha → cria customer com e-mail/nome/telefone do pedido, vincula, faz sign-in (cookie), redireciona para `/account/orders/{orderId}`.
3. **Já tem conta (mesmo e-mail):** create-account retorna **409** `AccountAlreadyExists` → frontend pede login → `POST .../claim` com cookie + token.
4. **Já logado no checkout:** pedido já nasce com `CustomerUserId` — claim desnecessário.

## Endpoints

### Create account

`POST /api/customer/orders/guest/{orderId}/create-account`  
Anon + rate limit (`guest-order-claim`) + CSRF disabled (como register).

```json
{
  "guestAccessToken": "...",
  "password": "...",
  "confirmPassword": "..."
}
```

Sucesso **200**:

```json
{
  "orderId": "...",
  "customerCreated": true,
  "orderLinked": true,
  "redirectTo": "/account/orders/..."
}
```

Conta já existe **409** ProblemDetails:

- `code` / `errorCode`: `AccountAlreadyExists`
- `message`: orientar login + claim

Token inválido/expirado → **401** opaco (`Order access denied.`).

Senha inválida → **400** ValidationProblemDetails (`password`, `confirmPassword`).

### Claim (logado)

`POST /api/customer/orders/guest/{orderId}/claim`  
Auth: `Customer` + rate limit + **CSRF** (`X-CSRF-TOKEN`, como demais mutações autenticadas).

```json
{ "guestAccessToken": "..." }
```

- E-mail do customer deve bater com `Order.CustomerEmail` (case-insensitive).
- Mesmo customer já vinculado → sucesso idempotente.
- Outro customer → **409** `ORDER_ALREADY_LINKED`.
- E-mail diferente → **403** genérico.
- Sem login → **401**.

## Segurança

- Claim **exige** GuestOrderAccessToken válido (não só e-mail).
- Não retorna token, hash, QR, provider IDs.
- Logs: `orderId` + `customerUserId`; sem token bruto / PII excessiva.
- Rate limit por IP (mesma cota configurável de guest status).

## Pendências futuras

- E-mail transacional pós-Pix com link de claim
- Magic link / recuperação sem token no browser
- Frontend wiring (create-account / login+claim / redirect)
