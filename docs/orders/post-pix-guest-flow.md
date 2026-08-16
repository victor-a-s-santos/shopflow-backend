# Fluxo pós-Pix (compra guest)

## Princípio

A compra **termina** quando o Pix é aprovado e o pedido fica `Paid`.  
Criar conta ou fazer login é **opcional** e não bloqueia:

- existência do pedido;
- pagamento;
- acompanhamento como convidado;
- visualização no Admin.

```
Guest checkout → Order → Pix Paid
  → acompanhar com GuestOrderAccessToken
  → (opcional) create-account OU login + claim
```

## Acompanhar sem conta

**Preferido:** `GET /api/orders/public/{orderNumber}`  
Header `X-ORDER-ACCESS-TOKEN` (ou query `t` / `token` — ver risco e contrato em [`EMAIL-002-guest-order-link-validation.md`](../features/EMAIL-002-guest-order-link-validation.md)).

**Legado:** `GET /api/orders/guest/{orderId}/status` + mesmo header.

Ambos cobrem tela de sucesso / acompanhamento com `orderNumber`, `customerStatus`, totais, status de pagamento (`method: Pix`), e-mail mascarado, itens e flags `canCreateAccount` / `accountExistsForEmail`.

**Não retorna:** token, hash, nome técnico do provider, provider IDs, PII completa. `expiresAt` só enquanto o Pix estiver `Pending`.


## Conta opcional

| Situação | Endpoint | Resultado |
|----------|----------|-----------|
| Sem conta | `POST .../create-account` | Cria customer, vincula, sign-in cookie, `ACCOUNT_CREATED_AND_ORDER_LINKED` |
| E-mail já cadastrado | `POST .../create-account` | **409** `ACCOUNT_ALREADY_EXISTS` + `redirectTo: /login` |
| Já logado (mesmo e-mail) | `POST .../claim` | Vincula, `ORDER_LINKED` |

Detalhes: [`guest-order-claim.md`](./guest-order-claim.md).

## OrderNumber

- `Order.Id` = Guid interno
- `Order.OrderNumber` = `bigint` único (sequence Postgres a partir de 10000)
- Exposto como string nos DTOs guest / customer / admin / create order

## Claim / create-account e Paid

MVP: create-account e claim são permitidos com token válido **mesmo se o pedido ainda estiver PendingPayment**.  
Não alteram status de pagamento. A UI deve incentivar a ação após Paid.

## Sem e-mail transacional (histórico)

O acompanhamento guest **também** usa o link do e-mail OrderCreated (`/pedido/{orderNumber}?t=`). Ver [`EMAIL-002-guest-order-link-validation.md`](../features/EMAIL-002-guest-order-link-validation.md). Recuperação depois que o token some do e-mail/browser continua dívida (sem reemissão).
