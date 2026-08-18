# Guest Order Claim (pós-Pix)

Vincula um pedido **guest** a uma conta customer usando `GuestOrderAccessToken` como prova de posse.

## Por que não aparece por e-mail

`GET /api/customer/orders` filtra **somente** por `Order.CustomerUserId`.  
Pedidos guest (`CustomerUserId = null`) **não** entram em “Meus pedidos”, mesmo com o mesmo e-mail da conta.

## Fluxo pós-Pix aprovado

1. Cliente tem `orderId` + `guestAccessToken` + vê `orderNumber` no status guest.
2. **Sem conta:** `POST .../create-account` → cria customer, vincula, sign-in, redirect `/account/orders/{orderId}`.
3. **Já tem conta:** create-account → **409** `ACCOUNT_ALREADY_EXISTS` → login → `POST .../claim`.
4. **Já logado no checkout:** pedido já nasce com `CustomerUserId`.

## Endpoints

### Create account

`POST /api/customer/orders/guest/{orderId}/create-account`  
Anon + rate limit (`guest-order-claim`) + CSRF disabled.

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
  "code": "ACCOUNT_CREATED_AND_ORDER_LINKED",
  "orderId": "...",
  "orderNumber": "10582",
  "customerCreated": true,
  "orderLinked": true,
  "redirectTo": "/account/orders/..."
}
```

Conta já existe **409** ProblemDetails:

- `code` / `errorCode`: `ACCOUNT_ALREADY_EXISTS`
- `message`: orientar login + claim
- `redirectTo`: `/login`

Senha inválida (Identity / regras) **400**:

- `code`: `PASSWORD_REQUIREMENTS_NOT_MET`
- `errors.password`: mensagens reais (ex.: “Use pelo menos um número.”)
- **Não** usar “Unable to complete registration.” quando houver erros específicos

Token inválido → **401** `INVALID_GUEST_ORDER_TOKEN`  
Token expirado → **401** `GUEST_ORDER_TOKEN_EXPIRED`

### Claim (logado)

`POST /api/customer/orders/guest/{orderId}/claim`  
Auth: `Customer` + rate limit + **CSRF**.

```json
{ "guestAccessToken": "..." }
```

Sucesso **200**:

```json
{
  "code": "ORDER_LINKED",
  "orderId": "...",
  "orderNumber": "10582",
  "orderLinked": true,
  "redirectTo": "/account/orders/..."
}
```

| Caso | HTTP | `code` |
|------|------|--------|
| E-mail diferente | 403 | `CUSTOMER_EMAIL_DOES_NOT_MATCH_ORDER` |
| Vinculado a outro customer | 409 | `ORDER_LINKED_TO_ANOTHER_CUSTOMER` |
| Mesmo customer | 200 | `ORDER_LINKED` (idempotente) |
| Sem login | 401 | — |

## Códigos oficiais

| Code | HTTP típico |
|------|-------------|
| `ACCOUNT_CREATED_AND_ORDER_LINKED` | 200 |
| `ORDER_LINKED` | 200 |
| `ACCOUNT_ALREADY_EXISTS` | 409 |
| `PASSWORD_REQUIREMENTS_NOT_MET` | 400 |
| `INVALID_GUEST_ORDER_TOKEN` | 401 |
| `GUEST_ORDER_TOKEN_EXPIRED` | 401 |
| `ORDER_NOT_FOUND_OR_ACCESS_DENIED` | 401 |
| `ORDER_LINKED_TO_ANOTHER_CUSTOMER` | 409 |
| `CUSTOMER_EMAIL_DOES_NOT_MATCH_ORDER` | 403 |

## Segurança

- Claim **exige** GuestOrderAccessToken válido (não só e-mail / orderId).
- Não retorna token, hash, QR, provider IDs.
- Rate limit por IP nos endpoints guest claim/status.
- Senha Identity: mín. 8, dígito, minúscula (FluentValidation alinhado).

## Testes

Ver unit tests em `GuestOrderClaimCommandHandlerTests`, `GuestOrderAccessGateTests`, `GetGuestOrderStatusQueryHandlerTests`.
