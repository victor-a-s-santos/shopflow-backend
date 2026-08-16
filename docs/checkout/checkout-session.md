# Checkout session e store access

`POST /api/checkout/sessions` continua responsável por itens, SalesRule, estoque, CEP, delivery preferences. A policy de acesso roda **antes**.

Fonte da verdade: `IStoreAccessPolicy` (`StoreAccessPolicy`). Frontend não é barreira.

## Regras

| Config | Request | Resultado |
|--------|---------|-----------|
| `Checkout:AllowGuest=false` e sem customer | 401 `GUEST_CHECKOUT_DISABLED` | “O checkout como convidado está desabilitado.” |
| `Closed` + Pending | 403 `CUSTOMER_APPROVAL_PENDING` | “Seu cadastro ainda está em análise.” |
| Rejected | 403 `CUSTOMER_ACCESS_REJECTED` | |
| Suspended | 403 `CUSTOMER_ACCESS_SUSPENDED` | |
| Approved | fluxo normal | |
| `Open` + `AllowGuest=true` | guest permitido | comportamento legado |

`GET /api/checkout/sessions/{id}` e cancelamento não mudam nesta fase.

Ver também `docs/cart-checkout.md` e `docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md`.
