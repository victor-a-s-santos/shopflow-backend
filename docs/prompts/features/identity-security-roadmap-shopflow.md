# Shopflow — README de Segurança, Identity e Autorização

> **Repo recomendado:** backend/principal  
> **Arquivo sugerido:** `docs/security/identity-security-roadmap.md`  
> **Objetivo:** orientar o Cursor a implementar a segurança do Shopflow por fases, sem SaaS pago, usando recursos próprios do backend .NET/PostgreSQL.  
> **Escopo:** autenticação, autorização, proteção do admin, conta do cliente, pedidos de convidados, cookies, CSRF, rate limit, auditoria, webhook futuro e integração com frontend React.

---

## 0. Contexto resumido

O Shopflow é um e-commerce modular em desenvolvimento.

### Backend

- .NET 10
- ASP.NET Core Minimal APIs
- Clean Architecture
- DDD
- CQRS
- MediatR
- FluentValidation
- EF Core
- PostgreSQL
- Monólito modular
- Schemas por módulo
- Docker Compose
- API local: `http://localhost:5127/api`

### Frontend

- React
- TypeScript
- Vite
- TanStack Query
- shadcn/ui
- Tailwind
- Cypress
- Repositório separado
- Lovable cria features visuais
- Cursor revisa, integra, cria services, types, Cypress e docs

### Infra planejada

- Frontend em Cloudflare Pages
- Backend + PostgreSQL em VPS com Docker Compose
- Proxy/SSL com Caddy
- Ambientes:
  - `teste.seudominio.com.br`
  - `api-teste.seudominio.com.br`
  - `hml.seudominio.com.br`
  - `api-hml.seudominio.com.br`
  - produção futura com domínio principal e `api.dominio.com.br`

### Decisão importante

Não usar SaaS pago de autenticação agora.

Não usar Cloudflare Access como requisito.

A segurança será implementada dentro do próprio Shopflow usando:

- ASP.NET Core Identity
- Cookie Authentication
- HttpOnly Secure Cookies
- Policies/roles/permissions
- CSRF protection
- Rate limiting do ASP.NET Core
- Auditoria própria em PostgreSQL

---

## 1. Decisão arquitetural

### Decisão principal

Implementar um módulo único de segurança chamado:

```txt
IdentityAccess
```

Esse módulo deve conter a base comum de usuários, autenticação, sessões, roles, permissões, tokens de segurança e eventos.

Porém, os fluxos devem ser separados:

```txt
Customer Identity
- login de cliente
- cadastro de cliente
- recuperação de senha
- confirmação de e-mail
- minha conta
- meus pedidos

Admin Identity
- login admin separado
- cookie separado
- permissões administrativas
- sessão mais curta
- MFA futuro
- auditoria obrigatória
```

### Não criar dois bancos/sistemas de usuário separados

Não criar:

```txt
IdentityCustomer separado
IdentityAdmin separado
```

Isso duplicaria regra de senha, token, sessão, lockout, auditoria e recuperação de senha.

Criar uma base única:

```txt
identity.users
identity.roles
identity.permissions
identity.sessions
identity.security_events
```

Mas com separação clara por:

```txt
- isStaff
- roles
- permissions
- authentication schemes
- cookie names
- endpoints
- frontend routes
```

---

## 2. Decisão: cookie HttpOnly, não JWT em localStorage

### Usar

```txt
ASP.NET Core Identity
+ Cookie Authentication
+ HttpOnly Secure Cookies
+ SameSite
+ CSRF token para mutações autenticadas
```

### Não usar

```txt
JWT em localStorage
IdentityServer
OpenIddict
Auth0
Clerk
Supabase Auth
Solução própria de hashing/token de senha
```

### Motivo

O Shopflow tem frontend React separado da API, mas não precisa expor token sensível ao JavaScript.

JWT em `localStorage` aumenta o impacto de XSS.

A API deve autenticar por cookies HttpOnly. O frontend apenas chama a API com:

```ts
credentials: "include"
```

### Cookies separados

Usar dois cookies:

```txt
__Host-shopflow_customer
__Host-shopflow_admin
```

Regras:

```txt
HttpOnly: true
Secure: true em HTTPS
SameSite: Lax preferencialmente
Path: /
Domain: não definir
```

Observação:

O prefixo `__Host-` exige `Secure`, `Path=/` e ausência de `Domain`.

---

## 3. Ordem correta de implementação

Como não haverá Cloudflare Access, a primeira entrega precisa proteger o admin.

### Ordem obrigatória

```txt
Fase 1 — Security Foundation + Admin Identity mínimo
Fase 2 — Proteger endpoints admin reais
Fase 3 — Customer Identity
Fase 4 — Account / Meus pedidos / Pedidos convidados
Fase 5 — Roles e permissions
Fase 6 — Auditoria
Fase 7 — Webhook Mercado Pago seguro
Fase 8 — Hardening produção
```

### Não começar por cliente

Não começar pelo login do cliente antes de proteger admin.

Risco atual mais crítico:

```txt
/admin e endpoints administrativos sem proteção
```

---

## 4. Fase 1 — Security Foundation + Admin Identity mínimo

### Objetivo

Criar a fundação de segurança com custo zero, usando recursos internos do backend.

### Entregas

#### 4.1 Criar módulo `IdentityAccess`

Estrutura sugerida:

```txt
src/Modules/IdentityAccess/
├── Domain/
├── Application/
├── Infrastructure/
├── Presentation/
└── IdentityAccessModule.cs
```

A estrutura exata deve seguir o padrão já usado no Shopflow.

#### 4.2 Integrar ASP.NET Core Identity

Criar usuário customizado:

```csharp
public sealed class ShopflowUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public bool IsStaff { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
```

Criar role customizada:

```csharp
public sealed class ShopflowRole : IdentityRole<Guid>
{
}
```

Usar EF Core/PostgreSQL.

Preferência:

```txt
schema: identity
```

#### 4.3 Configurar password policy inicial

Não exagerar, mas não deixar fraco.

Sugestão:

```txt
RequiredLength: 8
RequireDigit: true
RequireLowercase: true
RequireUppercase: false no MVP
RequireNonAlphanumeric: false no MVP
MaxFailedAccessAttempts: 5
DefaultLockoutTimeSpan: 15 minutos
```

Admin pode ter regra mais forte em fase futura.

#### 4.4 Configurar cookie admin

Cookie:

```txt
__Host-shopflow_admin
```

Propriedades:

```txt
HttpOnly = true
SecurePolicy = Always em ambiente HTTPS
SameSite = Lax
SlidingExpiration = true
ExpireTimeSpan = 8 horas no máximo
```

Em desenvolvimento local HTTP, permitir configuração diferente por ambiente, mas nunca em hml/prod.

#### 4.5 Criar endpoints admin auth

Base route:

```txt
/api/auth/admin
```

Endpoints:

```txt
POST /api/auth/admin/login
POST /api/auth/admin/logout
GET  /api/auth/admin/me
```

`POST /api/auth/admin/login`

Request:

```json
{
  "email": "admin@shopflow.local",
  "password": "..."
}
```

Resposta de sucesso:

```json
{
  "user": {
    "id": "uuid",
    "email": "admin@shopflow.local",
    "fullName": "Admin",
    "roles": ["Owner"],
    "permissions": []
  }
}
```

Não retornar token.

A API deve setar cookie HttpOnly.

`GET /api/auth/admin/me`

Deve retornar 401 se não autenticado.

Deve retornar 403 se autenticado, mas não staff/admin.

#### 4.6 Seed do primeiro admin

Criar seed seguro para ambiente dev/test/hml:

Variáveis:

```txt
SHOPFLOW_ADMIN_EMAIL
SHOPFLOW_ADMIN_PASSWORD
SHOPFLOW_ADMIN_FULLNAME
```

Regras:

- Não commitar senha real.
- Se variável não existir, não criar admin em hml/prod.
- Em dev pode criar admin default documentado, mas preferir user-secrets/env.
- Em hml, senha deve vir apenas de `.env` na VPS ou secret do ambiente.

#### 4.7 Criar policy `Backoffice`

Policy:

```txt
Backoffice
```

Requisitos:

```txt
- usuário autenticado no scheme admin
- IsStaff == true
- IsActive == true
```

---

## 5. Fase 2 — Proteger endpoints admin reais

### Objetivo

Nenhum endpoint administrativo pode ficar público.

### Proteger rotas

Todos os endpoints administrativos devem exigir:

```txt
RequireAuthorization("Backoffice")
```

### Endpoints admin a revisar

Catalog:

```txt
/api/admin/catalog/*
/api/catalog/admin/*
/api/catalog/products admin write endpoints
```

Inventory:

```txt
/api/admin/inventory/*
/api/inventory/admin/*
/api/inventory movements/reservations management endpoints
```

Orders futuro:

```txt
/api/admin/orders/*
```

Payments futuro:

```txt
/api/admin/payments/*
```

Dashboard:

```txt
/api/admin/dashboard
```

### Regra

Endpoints públicos de vitrine continuam públicos.

Endpoints de escrita/gestão sempre protegidos.

### Frontend admin

O frontend deve chamar:

```txt
GET /api/auth/admin/me
```

Antes de renderizar área admin.

Se 401:

```txt
redirect para /admin/login
```

Se 403:

```txt
mostrar "sem permissão"
```

---

## 6. Fase 3 — Customer Identity

### Objetivo

Permitir login/cadastro de cliente sem bloquear checkout convidado.

### Rotas

Base:

```txt
/api/auth/customer
```

Endpoints:

```txt
POST /api/auth/customer/register
POST /api/auth/customer/login
POST /api/auth/customer/logout
GET  /api/auth/customer/me

POST /api/auth/customer/forgot-password
POST /api/auth/customer/reset-password
POST /api/auth/customer/confirm-email
POST /api/auth/customer/resend-confirmation
```

### Cookie customer

```txt
__Host-shopflow_customer
```

Configuração:

```txt
HttpOnly = true
Secure = true em HTTPS
SameSite = Lax
SlidingExpiration = true
ExpireTimeSpan = 7 a 30 dias, conforme decisão do produto
```

### Cadastro

Request:

```json
{
  "fullName": "Cliente Exemplo",
  "email": "cliente@email.com",
  "password": "senha"
}
```

Regras:

- Normalizar e-mail.
- Não permitir e-mail duplicado.
- Criar usuário `IsStaff = false`.
- Criar role `Customer`.
- Enviar confirmação de e-mail se serviço de e-mail estiver configurado.
- Em dev/hml sem e-mail real, registrar link de confirmação em log seguro ou tabela de outbox para teste manual.

### Login

Regras:

- Usar SignInManager.
- Resposta genérica para erro.
- Atualizar `LastLoginAt`.
- Registrar security event.
- Aplicar lockout.

### Recuperação de senha

Resposta sempre genérica:

```txt
Se o e-mail existir, enviaremos instruções.
```

Não revelar se e-mail existe.

### Confirmação de e-mail

Obrigatória para:

```txt
- vincular pedidos convidados
- recuperação de senha confiável
- comunicações sensíveis
```

Não obrigatória para:

```txt
- comprar como convidado
```

---

## 7. Fase 4 — Account, pedidos do cliente e pedidos convidados

### Objetivo

Transformar as telas visual-only de conta em telas reais.

### Endpoints customer account

Base:

```txt
/api/account
```

Endpoints:

```txt
GET   /api/account/me
PATCH /api/account/profile

GET   /api/account/orders
GET   /api/account/orders/{orderId}

GET   /api/account/addresses
POST  /api/account/addresses
PUT   /api/account/addresses/{addressId}
DELETE /api/account/addresses/{addressId}
```

Todos exigem autenticação customer.

### Meus pedidos

Regra obrigatória:

```txt
order.CustomerUserId == currentUser.Id
```

Nunca listar pedido apenas por e-mail.

### Checkout convidado continua permitido

Não alterar a regra de produto:

```txt
cliente pode comprar sem login
```

### Pedido criado como convidado

Order deve guardar:

```txt
GuestName
GuestEmail
GuestPhone
CustomerUserId nullable
```

### Vincular pedido de convidado após cadastro

Regra segura:

```txt
Se usuário confirmou e-mail
e order.GuestEmail normalizado == user.Email normalizado
e order.CustomerUserId é null
então vincular pedido ao usuário
```

Criar serviço:

```txt
LinkGuestOrdersToCustomerService
```

Quando chamar:

```txt
- após confirmação de e-mail
- após login, se e-mail já confirmado
- endpoint manual futuro opcional
```

Registrar evento:

```txt
GuestOrderLinkedToCustomer
```

### Acompanhar pedido sem login

Criar `OrderAccessToken`.

Tabela:

```txt
orders.order_access_tokens
- Id
- OrderId
- TokenHash
- Purpose
- ExpiresAt
- UsedAt nullable
- RevokedAt nullable
- CreatedAt
```

Endpoint público:

```txt
GET /api/public/orders/{orderId}?accessToken=...
```

Retornar apenas dados limitados:

```txt
- número do pedido
- status
- itens
- total
- status pagamento
- previsão/forma de entrega quando existir
```

Não retornar:

```txt
- logs internos
- antifraude
- payload pagamento
- dados completos sensíveis
- informações administrativas
```

---

## 8. Fase 5 — Roles e permissions

### Objetivo

Começar com roles simples, mas deixar estrutura preparada para permissions.

### Roles iniciais

```txt
Customer
Owner
Admin
CatalogManager
InventoryManager
OrderManager
Finance
Support
```

### Permissions iniciais

```txt
catalog.read
catalog.write

inventory.read
inventory.adjust

orders.read
orders.manage

payments.read
payments.manage

customers.read_limited

users.manage
permissions.manage

audit.read
settings.manage
```

### Estratégia

Roles agrupam permissions.

Endpoints admin devem usar policies com permissions.

Exemplo:

```txt
RequirePermission("catalog.write")
RequirePermission("inventory.adjust")
RequirePermission("orders.manage")
```

No MVP, se isso atrasar muito, começar com `Backoffice` + `Owner/Admin`.

Mas não deixar o desenho impedir permissions no futuro.

---

## 9. Fase 6 — Auditoria

### Objetivo

Registrar ações sensíveis do backoffice e eventos de segurança.

### Tabela `identity.security_events`

```txt
Id
UserId nullable
EventType
IpAddress
UserAgent
MetadataJson
CreatedAt
CorrelationId
```

Eventos:

```txt
AdminLoginSucceeded
AdminLoginFailed
CustomerLoginSucceeded
CustomerLoginFailed
PasswordResetRequested
PasswordChanged
EmailConfirmed
SessionRevoked
```

### Tabela `backoffice.audit_logs`

```txt
Id
ActorUserId
ActorEmail
Action
EntityType
EntityId
BeforeJson
AfterJson
IpAddress
UserAgent
CorrelationId
CreatedAt
```

Auditar:

```txt
- alteração de produto
- alteração de preço
- ativação/desativação de produto/SKU
- ajuste de estoque
- cancelamento de pedido
- alteração manual de status de pedido
- alteração manual de pagamento
- criação/alteração de usuário admin
- alteração de permissões
```

### Cuidados

Não logar:

```txt
- senha
- token puro
- cookies
- access token Mercado Pago
- dados completos de cartão
- payload sensível completo
```

---

## 10. Fase 7 — Webhook Mercado Pago seguro

Mesmo que Mercado Pago só venha depois, deixar planejado.

### Endpoint

```txt
POST /api/webhooks/mercadopago
```

### Regras

- Não usa cookie.
- Não usa login.
- Validação por assinatura do provedor.
- Validar timestamp/replay quando aplicável.
- Gravar evento recebido.
- Garantir idempotência.
- Consultar Mercado Pago antes de marcar pedido como pago.
- Não confiar apenas no payload.
- Processar transição Order/PixPayment/Inventory de forma consistente.

### Tabela

```txt
payments.webhook_events
- Id
- Provider
- ExternalEventId
- ExternalPaymentId
- EventType
- PayloadHash
- SignatureValid
- ReceivedAt
- ProcessedAt nullable
- ProcessingStatus
- ErrorMessage nullable
- CorrelationId
```

---

## 11. CORS

### Regra

Nunca usar:

```txt
AllowAnyOrigin + AllowCredentials
```

### Origens permitidas por ambiente

Dev:

```txt
http://localhost:5173
http://localhost:3000
```

Teste:

```txt
https://teste.seudominio.com.br
```

HML:

```txt
https://hml.seudominio.com.br
```

Produção:

```txt
https://dominio.com.br
https://www.dominio.com.br
```

### Permitir credentials

Necessário porque auth usa cookies.

```txt
AllowCredentials = true
```

---

## 12. CSRF

Como o Shopflow usa cookies HttpOnly, precisa proteger mutações autenticadas contra CSRF.

### Estratégia

Criar endpoint:

```txt
GET /api/auth/csrf
```

Retornar token CSRF para o frontend enviar no header:

```txt
X-CSRF-TOKEN
```

Exigir CSRF em:

```txt
POST/PUT/PATCH/DELETE autenticados
```

Especialmente:

```txt
- admin
- account
- logout
- alteração de senha
- alteração de e-mail
- alteração de endereço
```

Não exigir CSRF em:

```txt
- GET público
- webhook Mercado Pago
```

---

## 13. Rate limiting

Criar policies:

```txt
auth-login
auth-password-reset
checkout-public
order-create
pix-create
webhook-provider
admin-sensitive
```

Sugestão inicial:

```txt
auth-login:
- limite por IP + e-mail
- curto e rígido

password-reset:
- limite por IP + e-mail
- resposta genérica

checkout/order/pix:
- limite por IP
- limite por e-mail/telefone quando disponível

webhook:
- limite alto o bastante para não bloquear provedor
- logs de excesso

admin-sensitive:
- limite moderado
```

Não ajustar no escuro para produção sem observar logs.

---

## 14. Data Protection keys

Como cookies e tokens do ASP.NET dependem de Data Protection, em Docker é obrigatório persistir as chaves.

### Docker Compose

Criar volume:

```yaml
volumes:
  shopflow-dataprotection:
```

Montar no container da API:

```yaml
- shopflow-dataprotection:/var/shopflow/dataprotection-keys
```

Configurar no backend:

```txt
PersistKeysToFileSystem("/var/shopflow/dataprotection-keys")
SetApplicationName("Shopflow")
```

Sem isso, restart/redeploy pode invalidar cookies/tokens inesperadamente.

---

## 15. Segurança dos ambientes sem Cloudflare Access

Como não vamos usar Cloudflare Access, a proteção deve vir do próprio sistema.

### Teste/HML

Obrigatório antes de expor:

```txt
- Admin Identity funcionando
- /admin/login real
- /api/auth/admin/me real
- todos endpoints admin protegidos por Backoffice
- Scalar/OpenAPI desativado ou protegido
- CORS fechado
- secrets fora do Git
- HTTPS
- rate limit
```

### Alternativa temporária de custo zero

Se precisar subir hml antes do Admin Identity ficar pronto, usar Caddy Basic Auth apenas temporariamente.

Mas a preferência do roadmap é:

```txt
não subir hml público antes do Admin Identity mínimo
```

---

## 16. Integração frontend

### O Lovable pode criar

- `/admin/login`
- Estados visuais de erro/sucesso
- Ajustes visuais em `/login`, `/register`, `/forgot-password`
- Estados de loading
- Estados de acesso negado
- Tela de sessão expirada

### O Cursor deve revisar/integrar

- `authService`
- `adminAuthService`
- `accountService`
- `apiClient` com `credentials: "include"`
- TanStack Query para `/me`
- Guards reais:
  - `AdminRoute`
  - `AccountRoute`
- Tratamento de 401/403
- Cypress

### Regra frontend

Não guardar token em:

```txt
localStorage
sessionStorage
IndexedDB
```

O estado do usuário pode ficar em memória/cache do TanStack Query.

---

## 17. Testes obrigatórios

### Backend

Criar testes para:

```txt
- admin login válido
- admin login inválido
- customer não acessa admin
- admin desativado não acessa admin
- endpoint admin sem cookie retorna 401
- endpoint admin com customer cookie retorna 403
- endpoint admin com admin cookie retorna 200
- customer login válido
- customer login inválido
- account/orders retorna apenas pedidos do usuário
- order access token válido retorna pedido limitado
- order access token inválido não vaza dados
- rate limit de login
- password reset não enumera e-mail
```

### Frontend Cypress

```txt
- /admin redireciona para /admin/login se não autenticado
- login admin bem-sucedido libera /admin
- customer não acessa /admin
- /account redireciona para /login se não autenticado
- customer logado acessa /account
- 401 limpa estado visual
- 403 mostra tela de sem permissão
```

---

## 18. Checklist antes de considerar fase concluída

### Fase 1 concluída quando

```txt
[ ] ASP.NET Core Identity configurado.
[ ] Tabelas Identity criadas no schema identity.
[ ] Cookie admin HttpOnly funcionando.
[ ] Admin seed por env funcionando.
[ ] /api/auth/admin/login funcionando.
[ ] /api/auth/admin/logout funcionando.
[ ] /api/auth/admin/me funcionando.
[ ] Policy Backoffice criada.
[ ] Data Protection keys persistidas.
```

### Fase 2 concluída quando

```txt
[ ] Todos endpoints admin protegidos.
[ ] Nenhum endpoint de escrita admin está público.
[ ] Frontend /admin exige /api/auth/admin/me.
[ ] Cypress cobre admin não autenticado.
```

### Fase 3 concluída quando

```txt
[ ] Customer register/login/logout/me funcionando.
[ ] Cookie customer separado.
[ ] Forgot/reset password planejado ou implementado.
[ ] Confirmação de e-mail planejada ou implementada.
[ ] AuthContext visual-only removido.
```

### Fase 4 concluída quando

```txt
[ ] /account/me real.
[ ] /account/orders real.
[ ] Ownership check em pedidos.
[ ] Guest order access token funcionando.
[ ] Pedido convidado pode ser vinculado após confirmação de e-mail.
```

---

## 19. Prompt principal para Cursor

Use este prompt no Cursor com este MD aberto:

```txt
Você está trabalhando no backend do Shopflow.

Leia integralmente o arquivo docs/security/README-identity-security-roadmap.md e implemente apenas a Fase 1 e Fase 2 neste primeiro ciclo.

Contexto:
- Backend .NET 10
- ASP.NET Core Minimal APIs
- Clean Architecture
- DDD
- CQRS
- MediatR
- FluentValidation
- EF Core
- PostgreSQL
- Monólito modular
- Schemas por módulo
- Docker Compose
- API local em http://localhost:5127/api

Objetivo do ciclo:
1. Criar o módulo IdentityAccess.
2. Integrar ASP.NET Core Identity com usuário Guid customizado.
3. Criar schema identity.
4. Criar cookie auth separado para admin: __Host-shopflow_admin.
5. Criar endpoints:
   - POST /api/auth/admin/login
   - POST /api/auth/admin/logout
   - GET /api/auth/admin/me
6. Criar policy Backoffice.
7. Criar seed seguro do primeiro admin via variáveis de ambiente.
8. Persistir Data Protection keys para funcionar bem em Docker.
9. Proteger todos endpoints administrativos existentes de Catalog e Inventory com RequireAuthorization("Backoffice").
10. Não implementar customer auth ainda.
11. Não implementar roles/permissions granulares ainda, exceto o mínimo necessário para Owner/Admin.
12. Não usar JWT em localStorage.
13. Não retornar token no JSON.
14. Não quebrar checkout convidado.
15. Não alterar regras de Catalog, Inventory, CartCheckout, Orders e PaymentsPix além da proteção dos endpoints admin.

Critérios:
- Código deve seguir os padrões existentes do projeto.
- Criar migrations necessárias.
- Atualizar appsettings/env examples sem secrets reais.
- Criar testes de integração para admin login e proteção dos endpoints.
- Atualizar documentação se necessário.
```

---

## 20. Prompt futuro para frontend/Cursor

Depois que backend Fase 1 e 2 estiverem prontos:

```txt
Você está trabalhando no frontend React/Vite do Shopflow.

Objetivo:
Integrar o login admin real com o backend IdentityAccess.

Contexto:
- Backend expõe:
  - POST /api/auth/admin/login
  - POST /api/auth/admin/logout
  - GET /api/auth/admin/me
- Autenticação usa cookie HttpOnly.
- Não existe token no frontend.
- Todas as chamadas devem usar credentials: "include".

Tarefas:
1. Criar/ajustar adminAuthService.
2. Criar AdminAuthContext ou adaptar AuthContext sem misturar customer/admin.
3. Criar AdminRoute guard real.
4. Proteger /admin, /admin/products, /admin/inventory.
5. Criar /admin/login se necessário.
6. Tratar 401 redirecionando para /admin/login.
7. Tratar 403 com tela de sem permissão.
8. Remover qualquer token fake/localStorage do fluxo admin.
9. Criar testes Cypress:
   - não autenticado não acessa /admin
   - login admin libera /admin
   - logout remove acesso
```

---

## 21. Prompt futuro para Lovable

```txt
Crie ou refine a tela /admin/login do Shopflow.

Diretrizes:
- Visual limpo, profissional e coerente com o painel admin atual.
- Campos: e-mail e senha.
- Estados: loading, erro de credenciais, conta bloqueada/inativa, sucesso.
- CTA: Entrar no painel.
- Não criar lógica real de token.
- Não usar localStorage.
- Preparar a UI para integração com backend via cookie HttpOnly.
- Criar também uma tela simples de acesso negado 403 e sessão expirada.
```

---

## 22. Observações finais

A decisão oficial deste roadmap é:

```txt
Sem SaaS pago.
Sem Cloudflare Access como requisito.
Sem JWT em localStorage.
Com ASP.NET Core Identity.
Com cookies HttpOnly.
Com admin protegido primeiro.
Com checkout convidado preservado.
Com evolução gradual para customer account, permissions, auditoria e webhook seguro.
```

O mais importante é não tentar implementar tudo de uma vez.

O primeiro ciclo deve ser:

```txt
Fase 1 + Fase 2
```

Só depois seguir para cliente.

---

## 23. Referências técnicas oficiais

- ASP.NET Core Identity:
  https://learn.microsoft.com/aspnet/core/security/authentication/identity

- SameSite cookies:
  https://learn.microsoft.com/aspnet/core/security/samesite

- CSRF / Anti-forgery:
  https://learn.microsoft.com/aspnet/core/security/anti-request-forgery

- Rate limiting:
  https://learn.microsoft.com/aspnet/core/performance/rate-limit

- Data Protection:
  https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview
