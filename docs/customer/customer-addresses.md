# Customer addresses

Endereços persistidos do cliente autenticado (`CustomerCookie`). Fonte da verdade para o checkout — não usar localStorage como endereço padrão.

## Entidade

`identity.customer_addresses`

| Campo | Notas |
|-------|--------|
| CustomerUserId | Obrigatório; isolamento por customer |
| Label | Opcional (`Casa`, `Loja`) |
| RecipientName | Obrigatório |
| PostalCode | 8 dígitos; resposta formatada `00000-000` |
| Street, Number, Neighborhood, City, State | Obrigatórios; UF 2 letras; número aceita `S/N` |
| Complement | Opcional |
| Country | `BR` |
| IsDefault | Apenas um default por customer |

O primeiro endereço vira default automaticamente. Marcar outro como default desmarca os demais. Pedidos antigos não mudam: o endereço do pedido é snapshot.

## Endpoints

Todos exigem `AuthPolicies.Customer` + CSRF nas mutations.

| Método | Path |
|--------|------|
| GET | `/api/customer/addresses` |
| GET | `/api/customer/addresses/default` |
| POST | `/api/customer/addresses` |
| PUT | `/api/customer/addresses/{id}` |
| DELETE | `/api/customer/addresses/{id}` |
| POST | `/api/customer/addresses/{id}/default` |

Acesso a endereço de outro customer retorna 404.

CEP continua em `GET /api/integrations/postal-code/br/{cep}`.
