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

## Endereço salvo

`POST /api/checkout/sessions` aceita:

- `customerAddressId` — copia o endereço do customer autenticado para o snapshot da sessão
- `address` — endereço manual (contrato legado)
- `saveAddress` / `setAsDefault` — persiste em `CustomerAddress` após criar a sessão, só se o customer estiver logado e o endereço for manual

Se `customerAddressId` for de outro customer: `400 ADDRESS_NOT_FOUND`. Guest continua enviando `address` quando o modo permitir.

O pedido (`POST /api/orders/from-checkout-session`) continua copiando o snapshot da sessão. Itens da sessão/pedido agora também podem carregar `productImageUrl` pública (null em pedidos antigos).

Ver também `docs/cart-checkout.md`, `docs/customer/customer-addresses.md` e `docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md`.
