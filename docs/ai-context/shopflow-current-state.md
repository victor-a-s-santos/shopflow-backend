# Shopflow — Estado atual do projeto

> Última atualização: junho/2026. Baseado no código em `apps/api`, `apps/web` e `docs/`.
> Se este arquivo divergir do código, **o código prevalece**.

## Visão geral

Shopflow é um e-commerce modular em monorepo: backend .NET (monólito modular) + frontend React (Vite). O foco atual é operação de catálogo/estoque no admin, vitrine funcional, carrinho local e checkout integrado com sessão real (`CartCheckout`). O módulo **Orders** backend MVP cria pedidos `PendingPayment` a partir de sessões de checkout — **sem integração frontend ainda**.

**Decisão de produto fixa:** checkout como convidado é permitido e prioritário. Login/cadastro são opcionais e preparados apenas visualmente.

---

## Stack

### Backend (`apps/api`)

| Item | Tecnologia |
|------|------------|
| Runtime | .NET 10 |
| API | ASP.NET Core Minimal APIs |
| Arquitetura | Clean Architecture por módulo (Domain / Application / Infrastructure) |
| Padrões | CQRS, MediatR, FluentValidation, Repository + Read Model |
| ORM | EF Core + PostgreSQL |
| Schemas DB | `catalog`, `inventory`, `cartcheckout`, `orders`, `payments_pix`, `identity` |
| OpenAPI | Scalar em Development (`/scalar/v1`) |
| Testes | xUnit — 13 projetos (~75 casos) |

### Frontend (`apps/web`)

| Item | Tecnologia |
|------|------------|
| Framework | React 18 + TypeScript |
| Build | Vite 7 |
| UI | shadcn/ui + Tailwind |
| Estado servidor | TanStack React Query |
| Carrinho | `CartContext` + `localStorage` (`shopflow.cart.v1`) |
| Auth | `AuthContext` visual-only (sem persistência) |
| E2E | Cypress 13.17 (Docker no macOS 26) |

---

## Infraestrutura local

`docker compose up` sobe:

| Serviço | Porta | Função |
|---------|-------|--------|
| `db` | 5432 | PostgreSQL 16 (`shopflow`) |
| `api` | 5127 | HttpApi — migrations + seed Catalog + **demo roupas** (se habilitado) |
| `worker` | — | Expiração de checkout/pedidos/Pix pendentes |
| `web` | 8080 | Vite dev — proxy `/api` → API |

Upload de imagens: filesystem local (`wwwroot/uploads`), não R2/S3.

---

## Módulos — classificação

### Pronto (backend + frontend integrados)

| Módulo | Backend | Frontend | Testes |
|--------|---------|----------|--------|
| **Catalog** | CRUD produtos/SKUs, categorias, atributos, imagens, by-slug; **salesRule** (Unit/min/múltiplos/pacote; update omite = preserva); demo seed; contratos admin — ver `docs/catalog/` | Admin produtos + vitrine + detalhe (salesRule UI Fase 2) | Unit + integration (cobertura parcial) |
| **Inventory** | Estoque, movimentações, reserva/confirm/cancel, constraints atômicos; **batch availability** Backoffice | Admin estoque completo; Product Edit ainda pode usar GET N+1 até wiring | Unit + integration (incl. concorrência) |

### Parcial

| Módulo | O que existe | O que falta |
|--------|--------------|-------------|
| **CartCheckout (backend)** | `POST/GET /api/checkout/sessions`, cancelamento, reserva de estoque na criação, compensação em falha parcial, **worker de expiração** | Confirmar sessão (pagamento real), shipping |
| **CartCheckout (frontend)** | UI 4 etapas, `POST /api/checkout/sessions`, reserva real, cria pedido + Pix Pending | Shipping |
| **Orders (backend)** | create + guest status; `orderNumber`; Admin/Customer orders; **guest claim** create-account/claim com codes oficiais + Identity password errors; Paid via Pix | Frontend pós-Pix (conta opcional), “Meus pedidos”, claim UI |
| **PaymentsPix (backend)** | Fake + **Mercado Pago Orders API**; webhook via **mercadopago-sdk** + oráculo manual; `SendNotificationUrlInOrderCreate` (painel vs payload); reconciliação Worker `GET /v1/orders` (fallback); Paid só `processed`/`accredited` | Frontend QR, e-mail |
| **Orders (frontend)** | Integrado no checkout (`PendingPayment`) | Conta/admin ainda visual/fake |
| **PaymentsPix (frontend)** | Integrado no checkout (intenção Pix Pending) | QR real, pagamento confirmado |
| **Cart (frontend)** | CRUD local por `skuId`, drawer, persistência | Sincronização com backend (não previsto ainda) |
| **IdentityAccess (backend)** | Admin + Customer auth, CSRF, policies `Backoffice`/`Customer`, 30 testes | Frontend customer auth; Account; guest order token |
| **Admin Dashboard** | Contagem real de produtos | Pedidos (156) e atividade recente são **hardcoded/fake** |

### Visual-only (frontend preparado, backend parcial)

| Área | Estado |
|------|--------|
| **IdentityCustomer UI** | Rotas `/login`, `/register`, `/forgot-password`, `/account/*`; `authService` stub; backend customer auth **implementado** (cookie HttpOnly) — integração frontend pendente |
| **GuestCheckoutNotice** | Aviso “Já tem uma conta?” no checkout — não bloqueia convidado |
| **AccountAddresses** | CRUD em memória (perde no reload) |

### Scaffold (solução .NET, sem implementação)

| Módulo | Estado |
|--------|--------|
| **Shipping** | Scaffold vazio |

### Pendente

- Gateway Pix real + webhook
- Shipping (frete)
- Frontend integração customer auth (backend pronto)
- Storage externo de imagens (R2/S3)
- CI/CD pipeline
- Guest Order Access Token backend (`SEC-006`) — falta wiring frontend
- Account orders
- Testes HttpApi end-to-end

---

## Endpoints backend reais

Base: `http://localhost:5127/api`

### Catalog (14 endpoints)

```
GET    /catalog/attributes
GET    /catalog/categories
GET    /catalog/products
GET    /catalog/products/{id}
GET    /catalog/products/by-slug/{slug}
POST   /catalog/products/variant
PUT    /catalog/products/{id}
DELETE /catalog/products/{id}
POST   /catalog/products/{id}/activate
POST   /catalog/products/{id}/deactivate
POST   /catalog/products/{productId}/variants
PUT    /catalog/products/{productId}/variants/{skuId}
DELETE /catalog/products/{productId}/variants/{skuId}
POST   /catalog/products/{id}/images
```

### Inventory (8 endpoints)

```
GET    /inventory/skus/{skuId}
GET    /inventory/skus/{skuId}/movements
POST   /inventory/skus/{skuId}
POST   /inventory/skus/{skuId}/add
POST   /inventory/skus/{skuId}/remove
POST   /inventory/skus/{skuId}/reserve
POST   /inventory/reservations/{reservationId}/confirm
POST   /inventory/reservations/{reservationId}/cancel
```

### Checkout (3 endpoints)

```
POST   /checkout/sessions
GET    /checkout/sessions/{id}
POST   /checkout/sessions/{id}/cancel
```

### Orders (3 endpoints)

```
POST   /orders/from-checkout-session
GET    /orders/{orderId}
GET    /orders/by-checkout-session/{checkoutSessionId}
```

### PaymentsPix (4 endpoints)

| Método | Path | Auth |
|--------|------|------|
| POST | `/api/payments/pix/orders/{orderId}` | Público |
| POST | `/api/payments/pix/webhooks/mercado-pago` | Público + `x-signature` |
| GET | `/api/payments/pix/{paymentId}` | Backoffice |
| GET | `/api/payments/pix/by-order/{orderId}` | Backoffice |

```
POST   /payments/pix/orders/{orderId}
GET    /payments/pix/{paymentId}
GET    /payments/pix/by-order/{orderId}
```

### IdentityAccess (10 endpoints)

```
GET    /auth/csrf
POST   /auth/admin/login
POST   /auth/admin/logout
GET    /auth/admin/me
POST   /auth/customer/register
POST   /auth/customer/login
POST   /auth/customer/logout
GET    /auth/customer/me
POST   /auth/customer/forgot-password
POST   /auth/customer/reset-password
POST   /auth/customer/confirm-email
```

Detalhes: `docs/security/SEC-005-customer-identity-backend.md`, `docs/security/README-identity-security-roadmap.md`.

**Não existem:** `/api/shipping`.

---

## Rotas frontend reais

Registradas em `apps/web/src/App.tsx`:

### Loja

| Rota | Página | Integração |
|------|--------|------------|
| `/` | Catalog | API Catalog |
| `/product/:slug` | ProductDetail | Catalog + Inventory (estoque do SKU) |
| `/cart` | Cart | localStorage |
| `/checkout` | Checkout | API CartCheckout (sessão real) |

### Auth / Conta (visual-only)

| Rota | Página |
|------|--------|
| `/login` | Login |
| `/register` | Register |
| `/forgot-password` | ForgotPassword |
| `/account` | AccountOverview |
| `/account/orders` | AccountOrders |
| `/account/orders/:id` | AccountOrderDetail |
| `/account/addresses` | AccountAddresses |
| `/account/profile` | AccountProfile |

### Admin (sem guard de auth)

| Rota | Página | Integração |
|------|--------|------------|
| `/admin` | AdminDashboard | parcial (pedidos fake) |
| `/admin/products` | AdminProducts | API Catalog |
| `/admin/products/new` | AdminProductForm | API Catalog + Inventory |
| `/admin/products/:id/edit` | AdminProductEdit | API Catalog |
| `/admin/inventory` | AdminInventory | API Catalog + Inventory (N+1) |

---

## Real vs simulado

| Funcionalidade | Real | Simulado / stub / visual |
|----------------|------|--------------------------|
| Listagem e CRUD de produtos | ✓ API | |
| Upload de imagem | ✓ local disk | |
| Estoque e reservas (API) | ✓ API | |
| Sessão de checkout (API) | ✓ API | |
| Finalizar compra na UI | ✓ sessão + pedido + Pix Pending | |
| Pedido (API) | ✓ `PendingPayment` via Orders | |
| Pagamento Pix (API) | ✓ `Pending` fake via PaymentsPix | |
| Expiração automática | ✓ worker (`Vls.Shopflow.Worker`) | |
| Criar pedido na UI | ✓ integrado no checkout | |
| Reserva de estoque no checkout UI | | ✓ via CartCheckout (não Inventory direto) |
| Carrinho | | ✓ localStorage |
| Login / cadastro / conta | Backend customer auth ✓ | Frontend ainda visual-only |
| Pagamento Pix | | ✓ fake Pending na UI; sem QR/gateway real |
| Frete | | ✓ “a calcular” |
| Pedidos (admin e conta) | | ✓ empty/fake states |
| Dashboard pedidos admin | | ✓ número hardcoded |

---

## Cypress

| Spec | Cobertura | Status |
|------|-----------|--------|
| `identity-customer.cy.ts` | Auth visual, guest checkout, sem APIs auth | ✓ 7 testes |
| `shopflow-demo.cy.ts` | Admin Catalog → Inventory → vitrine → checkout sessão | ✓ |
| `checkout-session.cy.ts` | Checkout guest + `POST /api/checkout/sessions` | ✓ |
| `catalog-admin.cy.ts` | Admin produtos | existe |
| `inventory-admin.cy.ts` | Admin estoque | existe |
| `storefront-cart.cy.ts` | Carrinho + localStorage | existe |
| `shopflow-demo-client.cy.ts` | Demo lento para vídeo cliente | existe |

Execução local recomendada: Docker `cypress/included:13.17.0` com `host.docker.internal:8080`.

Interceptors: checkout permite `POST /api/checkout/sessions`; bloqueia `/api/orders`, `/api/payments`; auth visual não chama `/api/auth`, etc.

Detalhes: `docs/testing.md`.

---

## Documentação existente

| Arquivo | Conteúdo |
|---------|----------|
| `docs/architecture.md` | Arquitetura modular, módulos, integrações |
| `docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md` | Design atacado/pacotes/múltiplos (sem código ainda) |
| `docs/catalog.md` | API Catalog |
| `docs/catalog-demo-seed.md` | Carga demo loja de roupas |
| `docs/inventory.md` | API Inventory |
| `docs/cart-checkout.md` | API CartCheckout (backend) |
| `docs/expiration-worker.md` | Worker de expiração checkout/orders/pix |
| `docs/orders.md` | API Orders (MVP backend) |
| `docs/testing.md` | Como rodar testes e Cypress |
| `docs/next-steps.md` | Roadmap (pode estar desatualizado — ver `docs/ai-context/next-actions.md`) |
| `docs/ai-context/*` | Contexto para IA (este arquivo) |
| `docs/security/*` | Identity admin + customer (SEC-004, SEC-005) |
| `docs/prompts/*` | Templates GPT / Lovable / Cursor |

### Design pendente de implementação — wholesale sales rules

Design técnico pronto em `docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md`:

- Regra de venda no **SKU** (`SalesMode`: Unit / MinimumQuantity / MultipleQuantity / FixedPackage / AssortedPackage).
- Pacote MVP = **SKU próprio** (estoque em pacotes); composição multi-SKU = pós-MVP.
- `quantity` sempre = unidades do SKU vendido; enforcement no `CreateCheckoutSession`.
- Nenhuma feature implementada ainda (Fase 0 = docs).

---

## Próximo módulo recomendado

**Frontend customer auth** ou **Gateway Pix real + webhook**

Backend customer identity concluído (SEC-005). Próximo elo crítico de negócio continua sendo pagamento real; paralelamente, integrar frontend com `/api/auth/customer/*`.
