# SEC-004 — Revisão de exposição de endpoints (Fase 1/2 hardening)

> Data: 2026-06-30  
> Escopo: revisão pós-IdentityAccess — Inventory, Orders, PaymentsPix, Catalog  
> Objetivo: nenhum endpoint sensível de backoffice/estoque/pedido/pagamento exposto publicamente por engano.

---

## 1. Decisões principais

### Inventory — reserva / confirm / cancel

| Decisão | Detalhe |
|---------|---------|
| **Fluxo checkout** | `CartCheckout` chama `IInventoryReservationService` → `IInventoryAtomicOperations` **internamente** (sem HTTP). |
| **Worker expiração** | `ExpirationProcessor` cancela reservas via `IInventoryReservationService` **internamente**. |
| **HTTP público** | Removidos `POST /api/inventory/skus/{id}/reserve`, `confirm`, `cancel`. |
| **HTTP admin/técnico** | Movidos para `/api/admin/inventory/...` com policy `Backoffice`. |
| **Consulta vitrine** | `GET /api/inventory/skus/{id}` retorna `SkuAvailabilityDto` (sem `quantityOnHand` / `quantityReserved`). |
| **Consulta admin** | Mesmo path com cookie admin → `InventoryItemDto` completo. |

### Orders

| Decisão | Detalhe |
|---------|---------|
| **Público** | `POST /api/orders/from-checkout-session` — checkout convidado (retorna pedido na resposta). |
| **Protegido** | `GET /api/orders/{id}` e `GET /api/orders/by-checkout-session/{id}` — contêm PII; exigem `Backoffice` até **guest access token** (Fase 4). |

### PaymentsPix

| Decisão | Detalhe |
|---------|---------|
| **Público** | `POST /api/payments/pix/orders/{orderId}` — MVP checkout (idempotente; retorna Pix na resposta). |
| **Protegido** | `GET /api/payments/pix/{paymentId}` e `GET /by-order/{orderId}` — exigem `Backoffice` até access token. |

### Catalog

Sem alteração estrutural — escrita já protegida com `Backoffice` na Fase 1/2. Leitura permanece pública para vitrine.

---

## 2. Matriz de endpoints

| Endpoint | Método | Módulo | Classificação atual | Classificação recomendada | Ação |
| -------- | ------ | ------ | ------------------- | ------------------------- | ---- |
| `/api/catalog/attributes` | GET | Catalog | Público vitrine | Público vitrine | Mantido |
| `/api/catalog/categories` | GET | Catalog | Público vitrine | Público vitrine | Mantido |
| `/api/catalog/products` | GET | Catalog | Público vitrine | Público vitrine | Mantido |
| `/api/catalog/products/{id}` | GET | Catalog | Público vitrine | Público vitrine | Mantido |
| `/api/catalog/products/by-slug/{slug}` | GET | Catalog | Público vitrine | Público vitrine | Mantido |
| `/api/catalog/products/variant` | POST | Catalog | Admin (`Backoffice`) | Admin | Mantido |
| `/api/catalog/products/{id}` | PUT/DELETE | Catalog | Admin | Admin | Mantido |
| `/api/catalog/products/{id}/activate\|deactivate` | POST | Catalog | Admin | Admin | Mantido |
| `/api/catalog/products/{id}/variants` | POST/PUT/DELETE | Catalog | Admin | Admin | Mantido |
| `/api/catalog/products/{id}/images` | POST | Catalog | Admin | Admin | Mantido |
| `/api/inventory/skus/{id}` | GET | Inventory | Público (safe) / Admin (full) | Público safe + admin autenticado | **Ajustado** |
| `/api/inventory/skus/{id}/movements` | GET | Inventory | Admin | Admin | Mantido |
| `/api/inventory/skus/{id}` | POST | Inventory | Admin | Admin | Mantido |
| `/api/inventory/skus/{id}/add\|remove` | POST | Inventory | Admin | Admin | Mantido |
| `/api/inventory/skus/{id}/reserve` | POST | Inventory | ~~Público~~ | Interno / admin técnico | **Removido** |
| `/api/inventory/reservations/{id}/confirm\|cancel` | POST | Inventory | ~~Público~~ | Interno / admin técnico | **Removido** |
| `/api/admin/inventory/skus/availability` | POST | Inventory | — | Admin (CSRF) | **Criado** — batch read-only |
| `/api/admin/inventory/skus/{id}/reserve` | POST | Inventory | — | Admin técnico | **Criado** |
| `/api/admin/inventory/reservations/{id}/confirm\|cancel` | POST | Inventory | — | Admin técnico | **Criado** |
| `/api/checkout/sessions` | POST | CartCheckout | Público checkout | Público checkout | Mantido |
| `/api/checkout/sessions/{id}` | GET | CartCheckout | Público checkout | Público checkout | Mantido |
| `/api/checkout/sessions/{id}/cancel` | POST | CartCheckout | Público checkout | Público checkout | Mantido |
| `/api/orders/from-checkout-session` | POST | Orders | Público checkout | Público checkout | Mantido |
| `/api/orders/{id}` | GET | Orders | ~~Público (PII)~~ | Admin / token futuro | **Protegido** |
| `/api/orders/by-checkout-session/{id}` | GET | Orders | ~~Público (PII)~~ | Admin / token futuro | **Protegido** |
| `/api/payments/pix/orders/{orderId}` | POST | PaymentsPix | Público checkout | Público checkout | Mantido |
| `/api/payments/pix/{paymentId}` | GET | PaymentsPix | ~~Público~~ | Admin / token futuro | **Protegido** |
| `/api/payments/pix/by-order/{orderId}` | GET | PaymentsPix | ~~Público~~ | Admin / token futuro | **Protegido** |
| `/api/auth/admin/login` | POST | IdentityAccess | Público (rate limited) | Público | Mantido |
| `/api/auth/admin/logout` | POST | IdentityAccess | Admin | Admin | Mantido |
| `/api/auth/admin/me` | GET | IdentityAccess | Autenticado | Admin | Mantido |
| `/api/auth/csrf` | GET | IdentityAccess | Público | Público | Mantido |

---

## 3. Endpoints protegidos por `Backoffice`

### Catalog (escrita)
- POST `/api/catalog/products/variant`
- PUT/DELETE `/api/catalog/products/{id}`
- POST activate/deactivate
- POST/PUT/DELETE variants
- POST images

### Inventory (gestão)
- GET `/api/inventory/skus/{id}/movements`
- POST create/add/remove stock
- POST `/api/admin/inventory/skus/availability` (batch read-only)

### Inventory (técnico — reserva)
- POST `/api/admin/inventory/skus/{id}/reserve`
- POST `/api/admin/inventory/reservations/{id}/confirm`
- POST `/api/admin/inventory/reservations/{id}/cancel`

### Orders (consulta com PII)
- GET `/api/orders/{orderId}`
- GET `/api/orders/by-checkout-session/{checkoutSessionId}`

### PaymentsPix (consulta operacional)
- GET `/api/payments/pix/{paymentId}`
- GET `/api/payments/pix/by-order/{orderId}`

### Auth
- POST `/api/auth/admin/logout`

---

## 4. Endpoints públicos permitidos

### Vitrine
- GET `/api/catalog/*` (somente leitura)
- GET `/api/inventory/skus/{id}` → `SkuAvailabilityDto`

### Checkout convidado
- POST/GET `/api/checkout/sessions...`
- POST `/api/orders/from-checkout-session`
- POST `/api/payments/pix/orders/{orderId}`

### Auth
- POST `/api/auth/admin/login` (rate limit)
- GET `/api/auth/csrf`

---

## 5. Fluxos internos (não HTTP)

| Operação | Chamador | Mecanismo |
|----------|----------|-----------|
| Reservar estoque | `CreateCheckoutSessionCommandHandler` | `IInventoryReservationService.ReserveAsync` |
| Cancelar reserva (checkout) | `CancelCheckoutSessionCommandHandler` | `IInventoryReservationService.CancelReservationAsync` |
| Cancelar reserva (expiração) | `ExpirationProcessor` | `IInventoryReservationService.CancelReservationAsync` |
| Confirmar reserva | — | **Pendente** — será ligado a pagamento aprovado (Fase webhook/MVP pago) via `IInventoryAtomicOperations.ConfirmReservationAsync` |

---

## 6. Pendências críticas / próximas fases

| Prioridade | Item |
|------------|------|
| Alta | **Guest order access token** — consulta limitada de pedido/Pix sem expor PII a UUID adivinhável |
| Alta | **Confirm reservation interno** — ao marcar pedido/pagamento como pago |
| Média | Atualizar `inventoryService` no frontend admin: paths `/api/admin/inventory/...` para reserve/confirm/cancel |
| Média | Frontend vitrine: adaptar tipo `InventoryDto` → `SkuAvailabilityDto` (`isAvailable` + `availableQuantity`) |
| Média | Rate limit em checkout/order/pix públicos |
| Baixa | Permissions granulares além de `Owner` |
| Baixa | Auditoria de operações de estoque |

---

## 7. Referências

- [README Identity Security](./README-identity-security-roadmap.md)
- CartCheckout → Inventory: `InventoryReservationService.cs`
- Expiration → Inventory: `ExpirationProcessor.cs`
