# HML Smoke Test Checklist

> Uso: marcar na VPS / browser durante go-live HML.  
> Runbook completo: [HML-GO-LIVE-RUNBOOK.md](./HML-GO-LIVE-RUNBOOK.md)  
> Não commitar secrets. Não marcar Paid no banco sem evidência MP.

**Data:** __________  
**Executor:** __________  
**API:** `https://api-hml.vipassessoriadigital.com.br`  
**Web:** `https://hml.vipassessoriadigital.com.br`  
**Resultado final:** ☐ HML READY · ☐ HML READY WITH RISKS · ☐ HML NOT READY

---

## A. Pré-voo env / deploy

- [ ] `.env.hml` real: `PaymentsPix__Provider=MercadoPago`
- [ ] `MercadoPago__Enabled=true`, `Environment=Sandbox`
- [ ] AccessToken / WebhookSecret / ApplicationId / UserId preenchidos (mesma app teste)
- [ ] `MercadoPagoReconciliation__Enabled=true`
- [ ] `MercadoPago__WebhookRawCaptureEnabled=false`
- [ ] `SendNotificationUrlInOrderCreate=false`; URL no **painel** MP = webhook HML
- [ ] `GuestOrderAccess__Enabled=true` + `TokenHashSecret` forte
- [ ] `AllowedOrigins` = FE HML; DataProtection volume ok
- [ ] `SHOPFLOW_ADMIN_RESET_PASSWORD=false`
- [ ] Deploy/build API + Worker; API restart (migrations) **antes** do Worker
- [ ] Logs API: provider MercadoPago; sem secrets
- [ ] Logs Worker: `reconciliation worker started` (não `disabled`)
- [ ] `curl -i $API_HML/health` → 200 Staging
- [ ] `curl -i $API_HML/api/admin/orders` → 401
- [ ] `curl -i $API_HML/api/customer/orders` → 401
- [ ] FE Cloudflare com `VITE_API_BASE_URL=.../api` HML

---

## B. Cypress (opcional mas recomendado)

```bash
cd apps/web
export CYPRESS_BASE_URL=https://hml.vipassessoriadigital.com.br
export CYPRESS_API_URL=https://api-hml.vipassessoriadigital.com.br/api
# + CYPRESS_ADMIN_* / CYPRESS_CUSTOMER_* locais
npx cypress run --spec cypress/e2e/admin-orders.cy.ts
npx cypress run --spec cypress/e2e/customer-orders.cy.ts
npx cypress run --spec cypress/e2e/checkout-csrf-pix-flow.cy.ts
```

- [ ] admin-orders: PASS / FAIL / NOT RUN — motivo: __________
- [ ] customer-orders: PASS / FAIL / NOT RUN — motivo: __________
- [ ] checkout-csrf-pix-flow: PASS / FAIL / NOT RUN — motivo: __________

---

## C. Smoke guest

- [ ] Loja sem login; produto demo + imagem OK
- [ ] Carrinho → checkout convidado → pedido criado
- [ ] Pix gerado (QR / copia-e-cola)
- [ ] Pago no sandbox
- [ ] Worker marca Paid (logs) **ou** webhook OK
- [ ] UI Pix → aprovado
- [ ] Admin `/admin/orders` → Paid + detalhe OK
- [ ] Sem QR/tokens/secrets no admin
- [ ] Mesmo e-mail em `/account/orders` **não** lista pedido guest
- [ ] OrderId: __________ · ProviderOrderId: __________

---

## D. Smoke customer

- [ ] Login/register customer OK
- [ ] Checkout logado → Pix → pagar sandbox → Paid
- [ ] Admin vê pedido Paid
- [ ] `/account/orders` lista; detalhe OK
- [ ] Sem provider IDs / QR / copia-e-cola / secrets na UI customer
- [ ] OrderId: __________ · CustomerUserId no banco ≠ null (se conferiu SQL)

---

## E. Smoke admin

- [ ] Login admin
- [ ] Filtro Paid + busca
- [ ] Detalhe: cliente, entrega, itens, totais, payment
- [ ] Ausentes: QR, copia-e-cola, ticketUrl, tokens, secrets

---

## F. Banco (opcional)

- [ ] Índice `IX_orders_CustomerUserId_CreatedAt` existe
- [ ] Pix/Order Status = Paid após smoke
- [ ] Reserva confirmada (não Pending órfã do pedido pago)

---

## G. Decisão

| Critério mínimo | OK? |
|-----------------|-----|
| API + Worker up, health OK, migrate OK | ☐ |
| MercadoPago + Reconciliation ON, RawCapture OFF | ☐ |
| Guest Paid + Admin vê + não lista em Meus pedidos por e-mail | ☐ |
| Customer Paid + aparece em Meus pedidos | ☐ |
| Sem secrets em logs/UI | ☐ |

**NOT READY se:** Fake provider, reconciliation off, Paid não ocorre, login/CORS quebrado, migrate falha, secrets em log.

**Notas / riscos remanescentes:**

```
(ex.: webhook assinatura ainda 401; Paid só via Worker — aceitável em HML)
```
