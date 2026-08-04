# PRE-PRODUCTION GO-LIVE CHECKLIST — Shopflow

## Status geral

Ambiente base validado: **TESTE**  
Decisão alvo: **APPROVED** / **APPROVED WITH RISKS** / **BLOCKED**  
Próximo alvo: preparação para produção controlada

Este é o **checklist oficial**. Completar antes de liberar produção.

> Cópia em `docs/prompts/qa/` é apenas contexto de prompt — **não** usar como checklist oficial.

Relatório: [PRE-PRODUCTION-GO-LIVE-RESULT.md](./PRE-PRODUCTION-GO-LIVE-RESULT.md)

---

# 1. Código, branches e versionamento

## Backend

- [ ] Branch backend correta definida.
- [ ] Commit backend final anotado.
- [ ] `dotnet build` HttpApi com sucesso.
- [ ] Testes focados:
  - [ ] Orders
  - [ ] CartCheckout
  - [ ] Shipping
  - [ ] PaymentsPix
  - [ ] Inventory
  - [ ] Catalog
  - [ ] IdentityAccess (integration se unit ausente)
- [ ] Falhas de sandbox/MSBuild documentadas, se ocorrerem.
- [ ] Nenhuma falha funcional pendente em Orders/Checkout/Pix/DeliveryBatch.

## Frontend

- [ ] Branch frontend correta definida.
- [ ] Commit frontend final anotado.
- [ ] `npm run typecheck` com sucesso.
- [ ] `npm run build` com sucesso.
- [ ] Cypress focado executado ou justificativa documentada.
- [ ] Nenhum mock permanente em tela de produção.
- [ ] Nenhum endpoint de teste hardcoded.

---

# 2. Variáveis de ambiente

## Backend API (TESTE / HML / Produção)

Validar por ambiente:

- [ ] `ASPNETCORE_ENVIRONMENT` correto (`Testing` / `Staging` / `Production`).
- [ ] Connection strings Postgres do ambiente.
- [ ] `DataProtection__KeysPath` + volume Docker persistido.
- [ ] CORS `AllowedOrigins__*` = domínio real do frontend do ambiente.
- [ ] Cookies `Secure=true` (HTTPS).
- [ ] CSRF habilitado.
- [ ] Rate limits habilitados (guest / CEP / auth).
- [ ] Logs sem secrets.

### Flags perigosas (obrigatório `false` em todo ambiente real)

- [ ] `MercadoPago__WebhookRawCaptureEnabled=false`
- [ ] `SHOPFLOW_ADMIN_RESET_PASSWORD=false`

Notas:

- Raw capture tem **hard gate** em `IsProduction()` no código; mesmo assim a flag deve ficar `false`.
- Reset de senha admin: usar `true` **somente** durante rotação temporária (RUNBOOK-001); desligar antes de smoke/go-live.

## Mercado Pago / Pix

- [ ] `PaymentsPix__Provider=MercadoPago`
- [ ] `MercadoPago__Enabled=true`
- [ ] Ambiente correto:
  - [ ] Sandbox para TESTE/HML
  - [ ] Production somente no go-live real
- [ ] `MercadoPago__AccessToken` do ambiente.
- [ ] `MercadoPago__WebhookSecret` do ambiente.
- [ ] `MercadoPago__NotificationUrl` / painel Webhooks apontam para API do ambiente.
- [ ] `MercadoPagoReconciliation__Enabled=true` (+ worker up).
- [ ] `MercadoPago__SandboxPayerFirstNameOverride` vazio/ausente em produção.
- [ ] `MercadoPago__WebhookRawCaptureEnabled=false`

## Guest access

- [ ] `GuestOrderAccess__Enabled=true`
- [ ] `GuestOrderAccess__TokenHashSecret` forte.
- [ ] TTL configurado.
- [ ] Rate limit configurado.

## CEP

- [ ] `PostalCodeLookup__Enabled=true`
- [ ] `PostalCodeLookup__Provider=ViaCep`
- [ ] `PostalCodeLookup__BaseUrl` / timeout configurados.
- [ ] API `/api/integrations/postal-code/br/{cep}` OK.
- [ ] Frontend **não** chama ViaCEP direto.

## Frontend / Cloudflare Pages

- [ ] `VITE_API_BASE_URL` do ambiente.
- [ ] `VITE_APP_ENV` informativo.
- [ ] Rebuild após qualquer alteração `VITE_*`.

## WhatsApp

- [ ] `VITE_SUPPORT_WHATSAPP_ENABLED=true` no Pages do ambiente.
- [ ] `VITE_SUPPORT_WHATSAPP_PHONE` com número **real** (só dígitos, internacional).
- [ ] Placeholder `5511999999999` / `55DDDNUMERO` **não** usados em Pages real.
- [ ] Frontend rebuildado após alterar env.
- [ ] Smoke UI: PDP, checkout, pós-pix/pedido, guest tracking/account — CTA `wa.me` sem token/GUID/internalNote.

Exemplo (não-real / docs only):

```env
VITE_SUPPORT_WHATSAPP_ENABLED=false
VITE_SUPPORT_WHATSAPP_PHONE=55DDDNUMERO
```

---

# 3. Migrations / containers / infra

- [ ] Migrations Catalog / Inventory / CartCheckout / Orders / PaymentsPix / Identity aplicadas.
- [ ] Migrations DeliveryBatch + OrderDeliveryFulfillment + CheckoutDeliveryPreference aplicadas.
- [ ] `shopflow-api-*` / `worker-*` / `postgres` / `caddy` Up.
- [ ] DNS + SSL (Caddy) OK.
- [ ] Health público 200.
- [ ] Volumes: dataprotection persistidos; uploads local só se R2 desligado.
- [ ] R2 (produção): `Storage__Provider=CloudflareR2`, secrets, `PublicBaseUrl` HTTPS custom domain; smoke upload → vitrine → delete (`docs/integrations/cloudflare-r2-product-images.md`).
- [ ] `R2ImageBackfill__Enabled=false` em produção (backfill só TEST manual; nunca auto).
- [ ] Reinício da API não invalida cookies de forma inesperada (DataProtection).

---

# 4. Catálogo / estoque

- [ ] Catálogo comprável (produtos ativos).
- [ ] Imagens de produto: URL pública (R2 ou `/uploads` legado) na vitrine/admin.
- [ ] Estoque com `QuantityOnHand` suficiente para smoke.
- [ ] Admin inventory sem auth → 401/403 (não 404/500).
- [ ] Demo seed: `DemoCatalogSeed__Enabled=false` em produção.

---

# 5. Checkout / Pix / Orders

- [ ] CSRF: `GET /api/auth/csrf` 200 + cookie.
- [ ] Checkout session + preferência de entrega.
- [ ] Create order from checkout.
- [ ] Pix create → Pending → Paid (Sandbox ou live conforme ambiente).
- [ ] Guest status com token; sem token → 401.
- [ ] DTO público/guest sem dados internos (batch/internalNote).

---

# 6. Delivery / Fulfillment / Remessas

- [ ] Admin orders list/detail.
- [ ] Fulfillment individual (ship/deliver).
- [ ] Delivery batches list/detail.
- [ ] Create batch from order(s).
- [ ] Ship/deliver remessa.
- [ ] Guest/public não vazam dados internos de remessa.

---

# 7. Segurança (customer / guest / admin)

- [ ] Admin login OK após rotação de senha (se aplicável).
- [ ] `SHOPFLOW_ADMIN_RESET_PASSWORD=false` após rotação.
- [ ] Rotas admin sem cookie → 401/403.
- [ ] Mutation admin sem CSRF rejeitada.
- [ ] Guest token hash secret forte; rate limit.
- [ ] Sem secrets em logs (`AccessToken`, `WebhookSecret`, senha admin, raw capture off).

---

# 8. Cypress (focado)

- [ ] `checkout-delivery-preferences.cy.ts`
- [ ] `checkout-csrf-pix-flow.cy.ts`
- [ ] `customer-orders.cy.ts`
- [ ] `admin-orders-list-polish.cy.ts`
- [ ] `admin-order-fulfillment.cy.ts`
- [ ] `admin-delivery-batches-list.cy.ts`
- [ ] `admin-delivery-batch-detail.cy.ts`
- [ ] `admin-create-delivery-batch-from-order.cy.ts`
- [ ] `whatsapp-contact.cy.ts`

Documentar: passou / falhou / not run / flake.

---

# 9. Smoke manual / rotas mínimas

- [ ] `GET /health` → 200
- [ ] `GET /api/auth/csrf` → 200
- [ ] `GET /api/admin/orders` sem auth → 401/403
- [ ] `GET /api/admin/delivery-batches` sem auth → 401/403
- [ ] `GET /api/integrations/postal-code/br/{cep}` → 200/503 controlado
- [ ] `GET /api/catalog/products` → 200 com itens
- [ ] Compra ponta a ponta (opcional se smoke recente válido)
- [ ] WhatsApp CTA UI com número real

---

# 10. Logs / monitoramento

- [ ] API sem erro crítico pós-recreate.
- [ ] Worker sem erro crítico.
- [ ] Caddy sem erro crítico de SSL/upstream.
- [ ] Sem `MP_WEBHOOK_RAW_CAPTURE` em operação normal.
- [ ] Sem dump de `docker compose config` com secrets em CI.

---

# 11. Rollback

- [ ] Plano documentado (RUNBOOK-004 / DEPLOY-003).
- [ ] FE: republicar deployment Pages anterior.
- [ ] API/Worker: recreate imagem/commit anterior (sem `compose down` wipe).
- [ ] DB: migrations forward-only (sem rollback destrutivo automático).
- [ ] Drill de rollback executado ou risco aceito documentado.

---

# 12. Pendências específicas de produção

- [ ] `deploy/.env.prod` completo (a partir de `.env.prod.example`).
- [ ] DataProtection produção.
- [ ] CORS domínio produção.
- [ ] MP Production/live + NotificationUrl prod.
- [ ] Worker produção.
- [ ] WhatsApp produção (número final).
- [ ] Domínio/SSL produção.
- [ ] Backup banco + volumes.
- [ ] Smoke produção controlado (baixo valor).
- [ ] Rollback drill.

---

# 13. Critérios de decisão

| Decisão | Quando usar |
|---------|-------------|
| **APPROVED** | Todos os itens críticos PASS; sem flags perigosas; smoke e segurança OK; produção só se ambiente prod validado. |
| **APPROVED WITH RISKS** | MVP operacional OK, com riscos documentados (ex.: Cypress not run, HML defasado, MP sandbox only). |
| **BLOCKED** | Qualquer FAIL de segurança/config perigosa, Production não validada para go-live, ou regressão funcional crítica. |

---

# 14. Pendências pós-go-live (não bloqueantes do MVP TESTE)

- [ ] Remover código temporário de raw capture quando diagnóstico encerrado.
- [ ] Atualizar dependências com advisory alto (`System.Security.Cryptography.Xml`).
- [ ] Brevo: `Brevo__Enabled` + sender verificado; worker outbox ativo; smoke confirm/reset/order/paid/ship (`docs/integrations/brevo-transactional-emails.md`).
- [ ] FE: `/confirm-email`, `/reset-password`, tracking `/pedido/:n?t=` hidrata token.
- [ ] Frete real (fora do escopo e-mail).
- [ ] Alinhar docs stale (`shopflow-current-state`, FE context).
