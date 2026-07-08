# Shopflow — Próximas ações

> Ordem recomendada com base no estado real do repositório (junho/2026).

---

## Concluído recentemente

- [x] **Customer Identity backend** — cookie `shopflow_customer_dev`, endpoints register/login/logout/me, forgot/reset/confirm (`docs/security/SEC-005-customer-identity-backend.md`)
- [x] **IdentityAccess Fase 1/2** — admin auth, CSRF, SEC-004 hardening
- [x] Integração frontend Checkout com `POST /api/checkout/sessions`, Orders e PaymentsPix
- [x] Módulo **Orders** backend MVP (`PendingPayment`, snapshot da sessão)
- [x] Módulo **PaymentsPix** backend MVP (provider fake, `PixPayment` Pending)
- [x] **Worker de expiração** — `CheckoutSession`/`Order`/`PixPayment` pendentes + liberação de estoque (`docs/expiration-worker.md`)
- [x] **Demo catalog seed (roupas)** — 10 produtos, 94 SKUs, 20 imagens, estoque inicial (`docs/catalog-demo-seed.md`)

---

## Etapa imediata (prioridade 1)

### Gateway Pix real + webhook

**Objetivo:** cobrança e confirmação reais; marcar `PixPayment`/`Order` `Paid`; confirmar reserva no Inventory.

**Tarefas:**
1. Implementar `IPixPaymentProvider` real (ex.: Mercado Pago).
2. Endpoint de webhook com idempotência.
3. Handler: `Paid` → `ConfirmReservationAsync` no Inventory.
4. Atualizar docs e testes de integração.

---

## Sequência recomendada

| # | Módulo | Motivo da ordem |
|---|--------|-----------------|
| 1 | **Gateway Pix real + webhook** | Cobrança e confirmação reais; marcar Order Paid; confirmar reserva |
| 2 | **Frontend customer auth** | Backend pronto; conectar UI com cookies + CSRF |
| 3 | **Endpoints batch** (Catalog/Inventory) | Reduzir N+1 no Admin Inventory |
| 4 | **Gateway Pix real + webhook** | Cobrança real (pausado até HML/domínio se necessário) |
| 5 | **Guest Order Access Token + Account** | Meus pedidos vinculados |
| 6 | **Shipping** | Frete real; hoje `ShippingAmount` é null |
| 7 | **Notifications** | E-mail real (confirm/reset/pedido) |

---

## IdentityCustomer — status

**Backend concluído** (jun/2026): endpoints `/api/auth/customer/*`, cookie HttpOnly separado, testes de integração.

**Pendente:** integração frontend (`authService`, `AuthContext`, CSRF), Account real, Guest Order Access Token.

Checkout convidado permanece público e prioritário.

---

## Checklist rápido por persona

### Cursor (backend)
→ Guest Order Access Token ou Notifications (e-mail real)

### Cursor (frontend)
→ Integrar customer auth com `/api/auth/customer/*` + CSRF; depois QR Pix quando gateway existir

### Cursor (Cypress)
→ Evoluir specs quando webhook/simulate-paid existir
