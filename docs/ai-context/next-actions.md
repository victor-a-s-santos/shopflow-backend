# Shopflow — Próximas ações

> Ordem recomendada com base no estado real do repositório (junho/2026).

---

## Concluído recentemente

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
| 5 | **Frontend guest order status + Account** | Token backend pronto (`SEC-006`); wiring UI Pix |
| 5b | **Frontend Admin Orders** | Backend `/api/admin/orders` pronto; listagem + detalhe no painel |
| 5c | **Frontend Customer Orders** | Backend `/api/customer/orders` pronto; “Meus pedidos” |
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
