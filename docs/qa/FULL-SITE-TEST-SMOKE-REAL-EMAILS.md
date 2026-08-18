# FULL SITE TEST SMOKE — REAL EMAILS (TESTE)

**Data/hora:** 2026-08-16 22:07–22:09 UTC (≈19:07–19:09 BRT)  
**Ambiente:** TESTE apenas  
- Frontend: `https://teste.vipassessoriadigital.com.br`  
- API: `https://api-teste.vipassessoriadigital.com.br` (`environment=Testing`, health 200)  
**Responsável:** Cursor agent (QA smoke automatizado + evidências VPS)  
**E-mail QA:** `vic***@gmail.com` (aliases `+pending|reject|suspend-*`)  
**HML/PROD:** não tocados  

## Decisão final

**PASS WITH RISKS**

Fluxos críticos de Closed → approve → compra → Pix Paid → fulfillment → remessa → outbox **Sent** com Brevo real passaram. Riscos: Cypress local sem binary; R2 upload multipart / WhatsApp UI / guest legado / click ConfirmEmail **NOT RUN**; MP webhook `signature_mismatch` (pagamento confirmou via reconciliation); 1 Failed histórico no outbox; `Brevo__SandboxMode` ficou `false` após o smoke.

## Configs críticas (mascaradas)

| Key | Valor observado |
|-----|-----------------|
| ASPNETCORE_ENVIRONMENT | Testing |
| StoreAccess__Mode | Closed |
| Checkout__AllowGuest / AllowGuestCheckout | false |
| CustomerAccess__RequireApproval | true |
| Brevo__Enabled | true |
| Brevo__SandboxMode | **false** (ajustado para este smoke; antes true) |
| AdminNotifications__ApprovalRequestsEmail | vic***@gmail.com |
| PublicApp__BaseUrl / AdminBaseUrl | https://teste.vipassessoriadigital.com.br |
| Storage__Provider | CloudflareR2 |
| Storage__R2__PublicBaseUrl | https://assets-teste.vipassessoriadigital.com.br |
| PaymentsPix__Provider | MercadoPago |
| MercadoPago__Environment | Sandbox |
| MercadoPago__Enabled | true |
| SHOPFLOW_ADMIN_RESET_PASSWORD | **false** (ajustado neste smoke; antes true) |
| GuestOrderAccess__Enabled | true |

## Resultado por fluxo

| Fluxo | Resultado | Evidência |
|-------|-----------|-----------|
| Infra/health | PASS | API health 200 Testing; FE 200; containers api-test/worker-test/caddy/postgres up |
| StoreAccess Closed | PASS | Anon catalog HTTP 401 `STORE_ACCESS_REQUIRES_LOGIN` |
| Register Pending | PASS | Register 201; login pending; catalog blocked |
| Login Pending | PASS | Catalog 401/403 approval pending |
| Admin approvals | PASS | list/approve/reject/suspend/reactivate |
| E-mails approval | PASS | Outbox Sent: RegistrationReceived, ApprovalRequestAdmin, ConfirmEmail, Approved, Rejected, Suspended |
| Catalog Approved | PASS | products + PDP após approve |
| Admin Product / slug | PASS | Nome `Camiseta Básica QA…`; slug gerado `camiseta-basica-qa-…` (sem acento); HTTP create OK |
| Admin Product R2 upload | NOT RUN | Multipart não exercitado neste script |
| Inventory | PASS | create/add; remove sem motivo = 400; remove com motivo OK |
| Cart/Checkout | PASS | session + order; sem guestAccessToken; CustomerUserId presente |
| Guest checkout new | PASS | bloqueado (401/403) |
| Postal code | PASS* | Endpoint correto `/api/integrations/postal-code/br/{cep}` = 200 (script usou path errado → NOT RUN no JSON; revalidado) |
| Pix Sandbox | PASS | QR gerado; pedido Paid (reconciliation; webhooks com signature_mismatch) |
| Customer Orders | PASS | list/detail; sem leak de internalNote/token |
| Admin Orders | PASS | detail + internal note |
| Fulfillment | PASS | ship + deliver pedido `#10028` |
| DeliveryBatch | PASS | create/ship/deliver; ship idempotente 200/409; pedidos `#10029` `#10030` |
| Forgot Password | PASS | request OK; outbox `ResetPassword` Sent |
| Confirm Email | PASS (envio) / NOT RUN (click) | ConfirmEmail Sent; link não clicado de propósito |
| Guest Tracking legado | NOT RUN | sem token legado |
| WhatsApp | NOT RUN (UI) | bundle FE contém `wa.me` |
| Security | PASS | admin orders/approvals anon 401/403 |
| Outbox/Brevo | PASS | ver seção abaixo |
| Backend unit tests | PASS (parcial) | Catalog 169, Orders 126, Inventory 43, Notifications 51. IdentityAccess.UnitTests project ausente no path tentado. |
| FE typecheck/unit | PASS | typecheck OK; 291 tests OK |
| Cypress | NOT RUN | binary Cypress não instalado localmente |

## Pedidos / produto

| Item | Valor |
|------|-------|
| Produto QA | id `5d437a20-…` slug `camiseta-basica-qa-20260816220715` |
| Order primary | **#10028** (`afc2c017-…`) Paid → Shipped → Delivered |
| Batch orders | **#10029**, **#10030** → remessa ship/deliver |

## Outbox / Brevo (evidência)

Contagens globais no momento da coleta: **Sent=74**, Skipped=18, Failed=1 (histórico).

Tipos **Sent** no smoke (destinatário `vic***`):

- Customer: ConfirmEmail, RegistrationReceived, ApprovalRequestAdmin, Approved, Rejected, Suspended  
- Orders: OrderCreated, PaymentConfirmed, OrderShipped, OrderDelivered (individual + remessa)  
- Auth: ResetPassword  

`Brevo__SandboxMode=false` no worker durante o smoke.

**Inbox real:** entrega no Gmail do destinatário controlado deve ser confirmada visualmente pelo responsável (outbox Sent + Provider path OK; leitura de caixa de entrada não automatizada aqui).

## Segurança

- Sem tokens/senhas/ApiKey no relatório.  
- Customer order JSON sem `internalOrderNote` / `guestAccessToken`.  
- Admin endpoints sem cookie → 401/403.  
- Guest checkout novo bloqueado em Closed.  
- `SHOPFLOW_ADMIN_RESET_PASSWORD` setado para `false`.  

## Bugs

Nenhum blocker novo no caminho crítico.

Observações:

1. Mercado Pago webhook em TESTE loga `signature_valid=False` / `signature_mismatch` — Paid veio por reconciliation/sandbox.  
2. Script de smoke usou path CEP incorreto primeiro; endpoint real OK.  
3. Upload R2 multipart não coberto neste run (Storage R2 enabled).  

## Riscos

1. **Brevo SandboxMode=false** permanece em TESTE → e-mails reais a cada fluxo; decidir se volta para `true` após QA.  
2. Cypress E2E não executado (binary ausente).  
3. WhatsApp / guest legado / R2 upload / confirm-email click não validados no browser.  
4. 1 row Failed + 18 Skipped históricas no outbox.  
5. Webhook MP signature mismatch em TESTE.  

## NOT RUN

- Cypress specs listadas no prompt  
- R2 image upload multipart + primary/delete  
- WhatsApp CTAs por página (browser)  
- Guest tracking legado com token  
- Click no link ConfirmEmail / reset (tokens não extraídos)  

## Próximos passos (produção)

1. Confirmar inbox Gmail dos e-mails Sent deste smoke.  
2. Decidir `Brevo__SandboxMode` em TESTE (`true` recomendado no dia a dia; `false` só para smokes controlados).  
3. Corrigir validação de assinatura webhook MP antes de PROD.  
4. Smoke R2 upload + Cypress em CI/Docker.  
5. Validar guest legado se ainda houver pedidos antigos.  
6. Só então checklist go-live PROD (não feito aqui).  

## Scripts usados (não commitam secrets)

- `scripts/qa/full-site-teste-smoke.py`  
- `scripts/qa/run-full-site-teste-smoke.py`  
