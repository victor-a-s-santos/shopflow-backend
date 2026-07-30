# Shopflow — Próximas ações

> Ordem recomendada com base no estado real do repositório (junho/2026).

---

## Concluído recentemente

- [x] **Product description + isActive contract** — create/update/detail persistem e retornam `description`/`isActive`; migration `description`; (`docs/catalog/admin-product-contract.md`); pendência FE enviar/hidratar no formulário
- [x] **Admin Inventory SKUs listing** — `GET /api/admin/inventory/skus` paginado (q/status/stockStatus/category/sort + estoque); separado de Catalog Admin (`docs/inventory/admin-inventory-skus-listing.md`); pendência FE Inventory Admin
- [x] **Admin products listing** — `GET /api/admin/catalog/products` paginado (status/featured/q/category/sort); separado da vitrine (`docs/catalog/admin-products-listing.md`); pendência FE Admin Products
- [x] **Product list categorySlug filter** — filtro server-side antes de count/paginação + `Category.Slug` + `q` básica (`docs/catalog/product-list-pagination-and-ordering.md`); pendência FE usar `categorySlug` (não filtrar client-side)
- [x] **Product list pagination + display order** — `page`/`pageSize`/`hasNextPage`, sort default featured→displayOrder→createdAt (não UpdatedAt); admin `isFeatured`/`displayOrder` (`docs/catalog/product-list-pagination-and-ordering.md`); pendência FE “Carregar mais” + admin UI
- [x] **Product list `salesSummary`** — listagem pública agrega sales rules para ProductCard sem N+1 by-slug (`docs/catalog/product-list-sales-summary.md`); FE consome no card (`apps/web/docs/product-card.md`)
- [x] **Wholesale sales rules Fase 4 (OrderItem snapshot)** — `salesDisplay` em Admin/Customer/Guest (`docs/orders/order-item-sales-snapshot.md`)
- [x] **Wholesale sales rules Fase 1 (backend)** — SalesMode/SkuSalesRule no SKU, checkout enforcement, migration (`docs/catalog/sales-rules-contract.md`)
- [x] **Wholesale sales rules — design (Fase 0)** — `docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md`
- [x] **Guest order claim pós-Pix** — create-account + claim com GuestOrderAccessToken (`docs/orders/guest-order-claim.md`)
- [x] **Post-Pix guest flow backend** — `orderNumber`, codes oficiais, Identity password errors, guest status flags (`docs/orders/post-pix-guest-flow.md`)
- [x] **Catalog product validation contracts** — ProblemDetails, SKU code, preços, attrs, imagens, proteção SKU, baixa por disponível (`docs/catalog/`, `docs/inventory/stock-movements.md`)
- [x] **Customer Identity backend** — cookie `shopflow_customer_dev`, endpoints register/login/logout/me, forgot/reset/confirm (`docs/security/SEC-005-customer-identity-backend.md`)
- [x] **IdentityAccess Fase 1/2** — admin auth, CSRF, SEC-004 hardening
- [x] Integração frontend Checkout com `POST /api/checkout/sessions`, Orders e PaymentsPix
- [x] Módulo **Orders** backend MVP (`PendingPayment`, snapshot da sessão)
- [x] **Admin Orders backend** — `GET /api/admin/orders` + detalhe (`docs/orders/admin-orders.md`)
- [x] **Customer Orders backend** — `GET /api/customer/orders` + `CustomerUserId` (`docs/orders/customer-orders.md`)
- [x] Módulo **PaymentsPix** backend MVP (provider fake, `PixPayment` Pending)
- [x] **Worker de expiração** — `CheckoutSession`/`Order`/`PixPayment` pendentes + liberação de estoque (`docs/expiration-worker.md`)
- [x] **Demo catalog seed (roupas)** — 10 produtos, 94 SKUs, 20 imagens, estoque inicial (`docs/catalog-demo-seed.md`)

---

## Etapa imediata (prioridade 1)

### Delivery / Fulfillment (próximo módulo operacional)

**Design:** `docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md`  
**Não implementar completo agora.** Separar pagamento × pedido × entrega.

**Fase 1 (feita):** bugs estoque remove + categoria no edit + CEP formatado + busca CEP via API Shopflow (`docs/integrations/postal-code-lookup.md`); documento de design.

**Fase 2 (próximo prompt):** campos no Order (método, data preferida, notes, `FulfillmentStatus`), admin marcar enviado/entregue.

**Fase 3:** `DeliveryBatch` / agrupamento.

**Fase 4:** WhatsApp CTA; chat nativo só com decisão explícita.

### Gateway Pix real + webhook

**Objetivo:** cobrança e confirmação reais; marcar `PixPayment`/`Order` `Paid`; confirmar reserva no Inventory.

**Tarefas:**
1. ~~Implementar `IPixPaymentProvider` real (Mercado Pago Checkout Transparente / Orders)~~ — concluído
2. ~~Webhook Order com assinatura + `GET /v1/orders/{id}`~~ — concluído (`docs/payments/MP-PIX-002-orders-provider-and-webhook.md`)
3. ~~Handler: `processed`/`accredited` → Paid + `ConfirmReservationAsync`~~
4. Integração frontend (QR/copia-e-cola + status Paid via `GET /api/orders/guest/{orderId}/status`).
5. E2E HML com evento Order no painel MP.

---

## Sequência recomendada

| # | Módulo | Motivo da ordem |
|---|--------|-----------------|
| 1 | **Gateway Pix real + webhook** | Cobrança e confirmação reais; marcar Order Paid; confirmar reserva |
| 2 | **Frontend customer auth** | Backend pronto; conectar UI com cookies + CSRF |
| 3 | **Frontend batch Inventory** | Backend `POST /api/admin/inventory/skus/availability` pronto; reduzir N+1 no Product Edit |
| 4 | **Gateway Pix real + webhook** | Cobrança real (pausado até HML/domínio se necessário) |
| 5 | **Frontend guest order status + Account** | Backend: status + `orderNumber` + claim codes; wiring UI pós-Pix (conta opcional) |
| 5b | **Frontend Admin Orders** | Backend `/api/admin/orders` + `orderNumber`; listagem + detalhe no painel |
| 5c | **Frontend Customer Orders** | Backend `/api/customer/orders` + claim/`orderNumber`; “Meus pedidos” |
| 5d | **Frontend ProductCard `salesSummary`** | Feito — card usa listagem; by-slug só PDP (`apps/web/docs/product-card.md`) |
| 5e | **Frontend home “Carregar mais” + categorySlug + admin display** | Backend paginação/`hasNextPage`/`categorySlug`/`q` + `display` no PUT; FE não filtrar categoria no client |
| 5f | **Frontend Admin Products table** | Backend `/api/admin/catalog/products` pronto; substituir listagem limitada pela API admin |
| 5g | **Frontend Admin Inventory SKUs** | Backend `/api/admin/inventory/skus` pronto; listar SKUs/estoque sem Admin Products + getProductById |
| 6 | **Shipping** | Frete real; hoje `ShippingAmount` é null |
| 7 | **Notifications** | E-mail real (confirm/reset/pedido) |

---

## IdentityCustomer — status

**Backend concluído** (jun/2026): endpoints `/api/auth/customer/*`, cookie HttpOnly separado, testes de integração.

**Pendente:** integração frontend (`authService`, `AuthContext`, CSRF), Account real; guest status UI com `X-ORDER-ACCESS-TOKEN`.

Checkout convidado permanece público e prioritário.

---

## Checklist rápido por persona

### Cursor (backend)
→ Guest Order Access Token ou Notifications (e-mail real)

### Cursor (frontend)
→ Integrar customer auth com `/api/auth/customer/*` + CSRF; depois QR Pix quando gateway existir

### Cursor (Cypress)
→ Evoluir specs quando webhook/simulate-paid existir
