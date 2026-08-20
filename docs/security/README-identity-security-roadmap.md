# Shopflow — Identity & Security (Fase 1 e 2)

## Variáveis de ambiente

| Variável | Obrigatória | Descrição |
|----------|-------------|-----------|
| `SHOPFLOW_ADMIN_EMAIL` | Sim (hml/prod) | E-mail do primeiro admin |
| `SHOPFLOW_ADMIN_PASSWORD` | Sim (hml/prod) | Senha do primeiro admin (mín. 8 chars, 1 dígito, 1 minúscula) |
| `SHOPFLOW_ADMIN_NAME` | Não | Nome exibido do admin (default: `Shopflow Admin`) |
| `SHOPFLOW_DEMO_USERS_ENABLED` | Não | `true` cria admin+cliente demo em Development/TESTE. **Sempre ignorado em Production.** |
| `SHOPFLOW_DEMO_ADMIN_EMAIL` / `SHOPFLOW_DEMO_ADMIN_PASSWORD` | Não | Defaults: `admin@teste.com.br` / `Admin123` |
| `SHOPFLOW_DEMO_CUSTOMER_EMAIL` / `SHOPFLOW_DEMO_CUSTOMER_PASSWORD` | Não | Defaults: `teste@teste.com.br` / `Teste123` (cliente já **Approved**) |
| `SHOPFLOW_DEMO_USERS_RESET_PASSWORD` | Não | `true` só para regravar senhas demo; depois `false` |
| `DataProtection__KeysPath` | Recomendado (Docker) | Pasta para chaves ASP.NET Data Protection (default: `./dataprotection-keys`) |

Em **Development**, o seed só roda se `SHOPFLOW_ADMIN_EMAIL` e `SHOPFLOW_ADMIN_PASSWORD` estiverem definidos.

Usuários demo (`SHOPFLOW_DEMO_USERS_ENABLED=true`) são criados depois do admin principal: um Owner (`admin@teste.com.br`) e um cliente já Approved (`teste@teste.com.br`). Ligar só em local e TESTE. Em Production o seed recusa mesmo com a flag `true`.

Em **hml/prod**, a API falha na inicialização se essas variáveis não existirem.

## Cookie admin

| Ambiente | Nome do cookie | Secure |
|----------|----------------|--------|
| Development | `shopflow_admin_dev` | SameAsRequest |
| hml/prod | `__Host-shopflow_admin` | Always |

Propriedades: HttpOnly, SameSite=Lax, Path=/, sem Domain.

## Cookie customer

| Ambiente | Nome do cookie | Secure |
|----------|----------------|--------|
| Development | `shopflow_customer_dev` | SameAsRequest |
| hml/prod | `__Host-shopflow_customer` | Always |

Policy `Customer`: role `Customer` + claim `is_customer=true`. Scheme `CustomerCookie`.

Endpoints: `POST/GET /api/auth/customer/register|login|logout|me`, `forgot-password`, `reset-password`, `confirm-email`.

Ver [SEC-005-customer-identity-backend.md](./SEC-005-customer-identity-backend.md).

## CSRF (SPA React)

1. `GET /api/auth/csrf` → `{ "token": "..." }`
2. Enviar header `X-CSRF-TOKEN` em POST/PUT/PATCH/DELETE autenticados
3. Login público e webhooks futuros estão excluídos

## CORS

Configurar em `appsettings` → `Cors:AllowedOrigins`:

```json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:8080",
    "https://teste.seudominio.com.br",
    "https://hml.seudominio.com.br"
  ]
}
```

`AllowCredentials` está habilitado — nunca usar `AllowAnyOrigin` com cookies.

## Testar login (cURL)

```bash
# Login (salva cookie)
curl -c cookies.txt -X POST http://localhost:5127/api/auth/admin/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"SuaSenha123"}'

# Me
curl -b cookies.txt http://localhost:5127/api/auth/admin/me

# CSRF + mutação autenticada
TOKEN=$(curl -s -c cookies.txt -b cookies.txt http://localhost:5127/api/auth/csrf | jq -r .token)
curl -b cookies.txt -X POST http://localhost:5127/api/auth/admin/logout \
  -H "X-CSRF-TOKEN: $TOKEN"
```

## Endpoints protegidos (policy `Backoffice`)

### Catalog (escrita)
- POST `/api/catalog/products/variant`
- PUT `/api/catalog/products/{id}`
- DELETE `/api/catalog/products/{id}`
- POST `/api/catalog/products/{id}/activate|deactivate`
- POST/PUT/DELETE `/api/catalog/products/{id}/variants...`
- POST `/api/catalog/products/{id}/images`

### Inventory (gestão)
- POST `/api/inventory/skus/{skuId}`
- POST `/api/inventory/skus/{skuId}/add|remove`
- GET `/api/inventory/skus/{skuId}/movements`

## Endpoints públicos (checkout/vitrine)

- GET `/api/catalog/*` (leitura)
- GET `/api/inventory/skus/{skuId}` → `SkuAvailabilityDto` (público) ou `InventoryItemDto` (admin autenticado)
- POST/GET `/api/checkout/sessions...`
- POST `/api/orders/from-checkout-session` (GET de pedido exige Backoffice até guest token)
- POST `/api/payments/pix/orders/{orderId}` (GET de Pix exige Backoffice até guest token)
- POST `/api/auth/admin/login`
- POST `/api/auth/customer/register|login|forgot-password|reset-password|confirm-email` (logout/me exigem cookie customer)

Reserva de estoque: **interna** (CartCheckout → Application). Endpoints técnicos admin em `/api/admin/inventory/...`.

Matriz completa: [SEC-004-endpoint-exposure-review.md](./SEC-004-endpoint-exposure-review.md)

## Próximas fases

- ~~Customer Identity (`__Host-shopflow_customer`)~~ — **concluído** (SEC-005)
- Account / meus pedidos
- Guest order access token
- Permissions granulares
- Auditoria (`identity.security_events`)
- Webhook Mercado Pago seguro

Detalhes customer: [SEC-005-customer-identity-backend.md](./SEC-005-customer-identity-backend.md)
