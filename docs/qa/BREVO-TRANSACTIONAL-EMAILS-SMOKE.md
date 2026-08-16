# Smoke — e-mails transacionais Brevo

Ambiente: **TESTE** primeiro (`Brevo__SandboxMode=true` + sender verificado). HML/PROD só depois do sandbox.

Worker precisa estar no ar (`EmailOutboxWorker` + `OrderEmailIntentDispatcherWorker`). A API só enfileira.

Não logar ApiKey, token de reset/confirm, guest token nem HTML completo.

## Preparação

- [ ] Schema `notifications` migrado (API aplica no startup)
- [ ] `Brevo__Enabled=true`, `Brevo__ApiKey` e `Brevo__SenderEmail` só na VPS
- [ ] `PublicApp__BaseUrl` / `PublicApp__AdminBaseUrl` apontam para o frontend do ambiente
- [ ] `AdminNotifications__ApprovalRequestsEmail` preenchido (inbox operacional)
- [ ] Sandbox: header `X-Sib-Sandbox: drop` (e-mail não chega ao destinatário real)

## Aprovação de cadastro (loja Closed)

- [ ] Novo cadastro Pending → outbox admin (`approval-request-admin`) + cliente (`registration-received`)
- [ ] Admin recebe link `/admin/customers/approvals`
- [ ] Cliente recebe “em análise”, sem prazo prometido
- [ ] Aprovar → “Seu acesso foi aprovado” + `/login`
- [ ] Recusar → texto genérico, **sem** motivo interno
- [ ] Suspender → “acesso temporariamente bloqueado”, sem motivo interno
- [ ] Reativar após suspender → segundo e-mail de aprovado
- [ ] Inbox admin vazia → só o e-mail do cliente é enfileirado
- [ ] Falha Brevo não impede o cadastro nem a decisão admin

## Auth (já EMAIL-001)

- [ ] Register → “Confirme seu e-mail” (`/confirm-email?email=&token=`) — independente da aprovação comercial
- [ ] Forgot password → “Redefinição de senha”; API sempre mensagem genérica

## Pedido / pagamento / remessa (já EMAIL-001)

Remessa **não** cria `batch-shipped:{batchId}`. Reusa `order:{id}:shipped` / `order:{id}:delivered` (um e-mail por pedido).

- [ ] Novo pedido → “Recebemos seu pedido #{n}” + link guest `?t=` só no created
- [ ] Paid → “Pagamento confirmado”
- [ ] Ship individual ou remessa → “foi enviado” (tracking se houver; sem batchId interno)
- [ ] Deliver individual ou remessa → “foi entregue”
- [ ] InternalOrderNote não aparece

## Falhas de config

- [ ] `Brevo__Enabled=false` ou ApiKey/Sender ausente → outbox permanece `Pending` (recuperável)
- [ ] HTTP 5xx/429 → retry; 4xx → `Failed` permanente
- [ ] `EmailOutbox__Enabled=false` → rows ficam `Pending` (worker não claima)

## Produção

- [ ] Sender verificado no Brevo
- [ ] `Brevo__SandboxMode=false`
- [ ] Reply-To operacional
- [ ] Notification center / marketing / SMS / WhatsApp **não** fazem parte deste smoke
