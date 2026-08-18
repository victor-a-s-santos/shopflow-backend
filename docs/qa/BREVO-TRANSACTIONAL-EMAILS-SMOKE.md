# Smoke — e-mails transacionais Brevo

Ambiente: **TESTE** primeiro (`Brevo__SandboxMode=true` + sender verificado). HML/PROD só depois do sandbox.

Worker precisa estar no ar (`EmailOutboxWorker` + `OrderEmailIntentDispatcherWorker`). A API só enfileira.

Não logar ApiKey, token de reset/confirm, guest token nem HTML completo.

## Preparação

- [x] Schema `notifications` migrado (API aplica no startup)
- [x] `Brevo__Enabled=true`, `Brevo__ApiKey` e `Brevo__SenderEmail` só na VPS
- [x] `PublicApp__BaseUrl` / `PublicApp__AdminBaseUrl` apontam para o frontend do ambiente
- [x] `AdminNotifications__ApprovalRequestsEmail` preenchido (inbox operacional)
- [x] Sandbox: header `X-Sib-Sandbox: drop` (e-mail não chega ao destinatário real)

## Aprovação de cadastro (loja Closed)

- [x] Novo cadastro Pending → outbox admin (`approval-request-admin`) + cliente (`registration-received`)
- [x] Admin recebe link `/admin/customers/approvals`
- [x] Cliente recebe “em análise”, sem prazo prometido
- [x] Aprovar → “Seu acesso foi aprovado” + `/login`
- [x] Recusar → texto genérico, **sem** motivo interno
- [x] Suspender → “acesso temporariamente bloqueado”, sem motivo interno
- [x] Reativar após suspender → segundo e-mail de aprovado
- [ ] Inbox admin vazia → só o e-mail do cliente é enfileirado *(NOT RUN nesta rodada)*
- [x] Falha Brevo não impede o cadastro nem a decisão admin *(fluxo principal OK; falha histórica HTTP 401 já existia em rows antigas)*

## Auth (já EMAIL-001)

- [x] Register → “Confirme seu e-mail” (`/confirm-email?email=&token=`) — independente da aprovação comercial
- [x] Forgot password → “Redefinição de senha”; API sempre mensagem genérica

## Pedido / pagamento / remessa (já EMAIL-001)

Remessa **não** cria `batch-shipped:{batchId}`. Reusa `order:{id}:shipped` / `order:{id}:delivered` (um e-mail por pedido).

- [x] Novo pedido → “Recebemos seu pedido #{n}” + link guest `?t=` só no created
- [x] Paid → “Pagamento confirmado”
- [x] Ship individual ou remessa → “foi enviado” (tracking se houver; sem batchId interno)
- [x] Deliver individual ou remessa → “foi entregue”
- [x] InternalOrderNote não aparece

## Falhas de config

- [ ] `Brevo__Enabled=false` ou ApiKey/Sender ausente → outbox permanece `Pending` (recuperável) *(NOT RUN)*
- [ ] HTTP 5xx/429 → retry; 4xx → `Failed` permanente *(NOT RUN controlado; há Failed histórico Brevo HTTP 401)*
- [ ] `EmailOutbox__Enabled=false` → rows ficam `Pending` (worker não claima) *(NOT RUN)*

## Produção

- [ ] Sender verificado no Brevo
- [ ] `Brevo__SandboxMode=false`
- [ ] Reply-To operacional
- [ ] Notification center / marketing / SMS / WhatsApp **não** fazem parte deste smoke

---

## Execução em TESTE

### 1. Data/hora

**2026-08-16 18:20–18:26 -03**

### 2. Ambiente

| Papel | Alvo |
|-------|------|
| API | `https://api-teste.vipassessoriadigital.com.br` (`Testing`) |
| Web | `https://teste.vipassessoriadigital.com.br` |
| Containers | `shopflow-api-test`, `shopflow-worker-test`, `shopflow-postgres` |
| Loja | `StoreAccess__Mode=Closed`, `CustomerAccess__RequireApproval=true`, guest checkout off |
| Smoke id | `e4cc207a` |

### 3. Configs mascaradas (após ajuste pré-smoke)

| Chave | Valor |
|-------|-------|
| `Brevo__Enabled` | `true` |
| `Brevo__SandboxMode` | `true` *(restaurado; estava `false` por validação anterior Test B)* |
| `Brevo__SenderEmail` | `no-reply@vipassessoriadigital.com.br` |
| `Brevo__ReplyToName` | `Atendimento` |
| `Brevo__ApiKey` | **SET** |
| `EmailOutbox__Enabled` | `true` |
| `OrderEmailIntentDispatcher__Enabled` | `true` |
| `PublicApp__BaseUrl` | `https://teste.vipassessoriadigital.com.br` |
| `PublicApp__AdminBaseUrl` | `https://teste.vipassessoriadigital.com.br` |
| `AdminNotifications__ApprovalRequestsEmail` | **SET** (inbox operacional) |

Chaves que **faltavam** no `.env.test` e foram adicionadas nesta rodada para o smoke: `AdminNotifications__ApprovalRequestsEmail`, `PublicApp__AdminBaseUrl`, `Brevo__ReplyTo*`, `StoreAccess__Mode=Closed`, `CustomerAccess__RequireApproval=true`, `Checkout__AllowGuest*=false`.

### 4. Resultado por fluxo

| Fluxo | Resultado |
|-------|-----------|
| Cadastro Pending + ConfirmEmail | **PASS** (`Sent`) |
| Admin approval-request + registration-received | **PASS** (`Sent`) |
| Approve | **PASS** (`CustomerApproved` `Sent`) |
| Reject | **PASS** (`CustomerRejected` `Sent`) |
| Suspend | **PASS** (`CustomerSuspended` `Sent`) |
| Reactivate → 2º approved | **PASS** (`Sent`; 2 keys `approved:{ticks}`) |
| Forgot / ResetPassword | **PASS** (`Sent`) |
| OrderCreated `#10024` | **PASS** (`Sent`) |
| PaymentConfirmed (Pix Sandbox → Paid) | **PASS** (`Sent`, key `order:{id}:paid`) |
| Ship / Deliver individual | **PASS** (`Sent`) |
| Remessa 2 pedidos `#10025`+`#10026` ship/deliver | **PASS** (1 e-mail shipped + 1 delivered **por pedido**) |
| Idempotência ship retry | **PASS** (`409`, count outbox shipped inalterado = 1) |
| Logs sem ApiKey / tokens / reason interno / internal notes | **PASS** (amostra worker/api ~25m) |

### 5. Outbox (tipos / status) — sem payload sensível

Customer `5a498d07-…` + pedidos smoke:

| Type | Status | IdempotencyKey (padrão) |
|------|--------|-------------------------|
| ConfirmEmail | Sent | `customer:confirm-email:{hash}` |
| CustomerApprovalRequestAdmin | Sent | `customer:{id}:approval-request-admin` |
| CustomerRegistrationReceived | Sent | `customer:{id}:registration-received` |
| CustomerApproved | Sent ×2 | `customer:{id}:approved:{utcTicks}` |
| CustomerRejected | Sent | `customer:{id}:rejected:{utcTicks}` |
| CustomerSuspended | Sent | `customer:{id}:suspended:{utcTicks}` |
| ResetPassword | Sent | `customer:reset-password:{hash}` |
| OrderCreated | Sent ×3 | `order:{id}:created` |
| PaymentConfirmed | Sent ×3 | `order:{id}:paid` |
| OrderShipped | Sent ×3 | `order:{id}:shipped` |
| OrderDelivered | Sent ×3 | `order:{id}:delivered` |

ProviderMessageId presente nos `Sent` (formato Brevo sandbox). HTML/token **não** inspecionados em log.

### 6. Status Sent / Skipped / Failed

- Rodada smoke: **todos os eventos novos → `Sent`** (sandbox Brevo).
- Histórico pré-smoke no banco: `Skipped` (“Brevo disabled or missing ApiKey/SenderEmail”) e 1× `OrderCreated` `Failed` (`Brevo HTTP 401`) — **não** reprocessados.

### 7. Erros encontrados

1. **Config gap (corrigido na VPS para o smoke):** `.env.test` sem `AdminNotifications__ApprovalRequestsEmail`, `PublicApp__AdminBaseUrl`, `Brevo__ReplyTo*`, flags explícitas de StoreAccess Closed; `Brevo__SandboxMode` estava `false`.
2. Endpoint público de store access é `/api/store/access` (não `/api/store-access`) — falso WARN no harness, não bug de produto.

### 8. Riscos

1. `Brevo__SandboxMode` precisa permanecer `true` em TESTE até aceite explícito de entrega real (Test B / HML).
2. Inbox admin usada no smoke é endereço operacional de TESTE — validar se é a inbox desejada a longo prazo.
3. Rows históricas `Skipped`/`Failed` podem confundir dashboards até limpeza/arquivamento consciente.
4. Produção ainda **fora de escopo** (`SandboxMode=false` + sender verificado).

### 9. Decisão

### **PASS WITH RISKS**

Critérios funcionais do prompt (cadastro/aprovação/auth/pedido/Pix/fulfillment/remessa/idempotência/logs) **PASS** em TESTE com sandbox. Riscos são de configuração operacional (flags que precisaram ser preenchidas; histórico de outbox; sandbox vs entrega real).

### Pedidos / batch desta execução

| Entidade | Id |
|----------|-----|
| Customer A | `5a498d07-b146-4825-b09a-4823e4c5fbbd` |
| Order individual | `#10024` |
| Remessa orders | `#10025`, `#10026` |
| Delivery batch | `831af961-15e1-4d4e-89d8-322d9df33b5c` |
