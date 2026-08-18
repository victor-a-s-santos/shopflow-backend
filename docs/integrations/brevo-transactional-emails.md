# Brevo — e-mails transacionais

> 2026-08-16 — EMAIL-001 (pedido/auth) + Fase 3 (aprovação de cadastro). Templates HTML no código + outbox/worker.

## Escopo

Envia e-mails **transacionais** via Brevo Transactional Email API (`POST /v3/smtp/email`).

| Evento | Quando | IdempotencyKey |
|--------|--------|----------------|
| Confirmar e-mail | register customer | `customer:confirm-email:{hash}` |
| Redefinir senha | forgot password | `customer:reset-password:{hash}` |
| Cadastro pendente (admin) | register Closed/Pending | `customer:{id}:approval-request-admin` |
| Cadastro recebido | register Closed/Pending | `customer:{id}:registration-received` |
| Acesso aprovado | approve / reactivate | `customer:{id}:approved:{utcTicks}` |
| Cadastro recusado | reject | `customer:{id}:rejected:{utcTicks}` |
| Acesso suspenso | suspend | `customer:{id}:suspended:{utcTicks}` |
| Novo pedido | order created | `order:{orderId}:created` |
| Pagamento confirmado | order Paid | `order:{orderId}:paid` |
| Pedido enviado | fulfillment Shipped (individual **ou** remessa) | `order:{orderId}:shipped` |
| Pedido entregue | fulfillment Delivered (individual **ou** remessa) | `order:{orderId}:delivered` |

Remessa (`DeliveryBatch`) **não** gera `batch-shipped:{batchId}`. O dispatcher já cria **uma intent por pedido** nas mesmas keys `shipped`/`delivered` — um e-mail extra por remessa duplicaria envio.

Aprovação: `docs/customer/customer-approval-emails.md`. Pedidos: `docs/orders/order-emails.md`. Smoke: `docs/qa/BREVO-TRANSACTIONAL-EMAILS-SMOKE.md`.

Não inclui: marketing, newsletter, SMS, WhatsApp Business, chat, notification center, frontend.

## Arquitetura

- Pedidos: fato de negócio + `orders.email_intents` no mesmo `OrdersDbContext.SaveChanges` (ver `docs/features/EMAIL-001-transactional-email-outbox-brevo.md`)
- Worker `OrderEmailIntentDispatcherWorker` → `IEmailNotificationService` → `notifications.email_outbox`
- `ITransactionalEmailSender` → `BrevoTransactionalEmailSender` (HttpClient)
- Worker `EmailOutboxWorker` processa outbox com `SKIP LOCKED`, retry/backoff e reclaim de `Processing` órfão
- Auth: `OutboxIdentityEmailSender` (pós-commit/best-effort; **não** usa `email_intents`)
- Aprovação: `OutboxCustomerAccessNotifier` → outbox (pós-commit/best-effort; **não** usa `email_intents`)
- `IOrderEmailNotifier` permanece registrado por compatibilidade; o ciclo de pedido **não** depende mais de enqueue pós-commit

Falha Brevo **não** quebra checkout/pedido/auth (intent + outbox + retries no worker).

Não há `TransactionScope` nem transação compartilhada entre DbContexts. RabbitMQ/MassTransit estão adiados.

## Configuração

```
PublicApp__BaseUrl=https://sua-loja.com.br
PublicApp__AdminBaseUrl=https://admin.sua-loja.com.br
PublicApp__StoreName=Vip Assessoria

AdminNotifications__ApprovalRequestsEmail=ops@seudominio.com.br

Brevo__Enabled=true
Brevo__ApiKey=...
Brevo__SenderEmail=no-reply@seudominio.com.br
Brevo__SenderName=VIP Assessoria Digital
Brevo__ReplyToEmail=atendimento@seudominio.com.br
Brevo__ReplyToName=Atendimento
Brevo__SandboxMode=true
Brevo__TimeoutSeconds=10

EmailOutbox__Enabled=true
EmailOutbox__IntervalSeconds=15
EmailOutbox__BatchSize=20
EmailOutbox__MaxAttempts=8
EmailOutbox__ProcessingTimeoutSeconds=120

OrderEmailIntentDispatcher__Enabled=true
OrderEmailIntentDispatcher__IntervalSeconds=15
OrderEmailIntentDispatcher__BatchSize=20
```

Com `Brevo__Enabled=false` (ou sem ApiKey/Sender), o worker **não** marca `Skipped`. A mensagem volta para `Pending` com `LastError` de configuração, `Attempts` inalterado e `NextAttemptAt` adiado (~`EmailOutbox__IntervalSeconds`) para retry quando a config existir. `Skipped` fica reservado para um “não enviar” consciente, não para falha de setup.

`EmailOutbox__Enabled=false` nem chega a claimar: as rows permanecem `Pending`. HTTP 4xx da Brevo (ex.: 401 de chave inválida) continua `Failed` permanente — isso não é “config ausente”.

## Templates

MVP: HTML simples em `TransactionalEmailTemplates` (PT-BR).  
TemplateId no painel Brevo: não usado ainda (campo reservado para evolução).

## Links seguros

| Contexto | Link |
|----------|------|
| Customer logado | `{PublicApp}/account/orders/{orderId}` |
| Guest | `{PublicApp}/pedido/{orderNumber}?t={guestAccessToken}` |

Token guest só no e-mail do **novo pedido** (momento em que o raw token existe). E-mails posteriores de guest podem omitir `?t=` se o token não estiver disponível.

O FE **precisa** hidratar `?t=` na rota `/pedido/:orderNumber` e chamar `GET /api/orders/public/{orderNumber}` com header `X-ORDER-ACCESS-TOKEN`. Contrato: `docs/features/EMAIL-002-guest-order-link-validation.md`.

Páginas auth sugeridas: `/confirm-email`, `/reset-password` (query `email` + `token`).

## Segurança

- Nunca logar ApiKey, tokens raw, nem nota interna.
- Outbox não persiste InternalOrderNote / dados de remessa interna.
- Idempotency única no banco evita duplicata.

## Checklist TESTE / produção

1. Migrar schemas `orders` (`email_intents`) e `notifications` (`ProcessingStartedAt`) — a API aplica no startup
2. Configurar sender verificado no Brevo
3. `Brevo__Enabled=true` + ApiKey **somente na VPS** (sandbox primeiro)
4. Worker rodando (`OrderEmailIntentDispatcherWorker` + `EmailOutboxWorker`)
5. Registrar cliente Closed → e-mails admin + “cadastro recebido”; Open → só confirmação de e-mail
6. Forgot password → e-mail reset (API sempre mensagem genérica)
7. Aprovar / recusar / suspender → e-mail do cliente **sem** motivo interno
8. Criar pedido Pix → intent created → e-mail “Recebemos seu pedido”
9. Webhook Paid → intent paid → e-mail pagamento (AlreadyPaid também repara intent)
10. Ship/Deliver (pedido ou remessa) → e-mails por pedido (`order:{id}:shipped|delivered`, sem key de batch)
11. Desligar Brevo / ApiKey ausente → outbox permanece `Pending` com erro de configuração (recuperável); API segue OK
12. Não logar ApiKey, guest token, reset token ou HTML completo

Produção exige sender verificado no Brevo e `Brevo__SandboxMode=false`.
