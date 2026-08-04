# Brevo — e-mails transacionais

> 2026-08-02 — MVP com templates HTML no código + outbox/worker.

## Escopo

Envia e-mails **transacionais** via Brevo Transactional Email API (`POST /v3/smtp/email`).

| Evento | Quando | IdempotencyKey |
|--------|--------|----------------|
| Confirmar cadastro | register customer | `customer:confirm-email:{hash}` |
| Redefinir senha | forgot password | `customer:reset-password:{hash}` |
| Novo pedido | order created | `order:{orderId}:created` |
| Pagamento confirmado | order Paid | `order:{orderId}:paid` |
| Pedido enviado | fulfillment Shipped (individual ou remessa) | `order:{orderId}:shipped` |
| Pedido entregue | fulfillment Delivered | `order:{orderId}:delivered` |

Não inclui: marketing, newsletter, SMS, WhatsApp Business, chat.

## Arquitetura

- `ITransactionalEmailSender` → `BrevoTransactionalEmailSender` (HttpClient)
- `IEmailNotificationService` → render PT-BR + enqueue
- Tabela `notifications.email_outbox`
- Worker `EmailOutboxWorker` processa pending com retry/backoff
- Auth: `OutboxIdentityEmailSender` substitui o stub de log
- Orders: `IOrderEmailNotifier` (Null stub; real em Notifications)

Falha Brevo **não** quebra checkout/pedido/auth (enqueue best-effort + retries no worker).

## Configuração

```
PublicApp__BaseUrl=https://sua-loja.com.br
PublicApp__StoreName=Vip Assessoria

Brevo__Enabled=true
Brevo__ApiKey=...
Brevo__SenderEmail=no-reply@seudominio.com.br
Brevo__SenderName=Vip Assessoria
Brevo__ReplyToEmail=atendimento@seudominio.com.br
Brevo__SandboxMode=true
Brevo__TimeoutSeconds=10

EmailOutbox__Enabled=true
EmailOutbox__IntervalSeconds=15
EmailOutbox__BatchSize=20
EmailOutbox__MaxAttempts=8
```

Com `Brevo__Enabled=false` (ou sem ApiKey/Sender), o worker marca mensagens como `Skipped`.

## Templates

MVP: HTML simples em `TransactionalEmailTemplates` (PT-BR).  
TemplateId no painel Brevo: não usado ainda (campo reservado para evolução).

## Links seguros

| Contexto | Link |
|----------|------|
| Customer logado | `{PublicApp}/account/orders/{orderId}` |
| Guest | `{PublicApp}/pedido/{orderNumber}?t={guestAccessToken}` |

Token guest só no e-mail do **novo pedido** (momento em que o raw token existe). E-mails posteriores de guest podem omitir `?t=` se o token não estiver disponível — FE deve aceitar `?t=` no tracking público (pendência se ainda não lê query).

Páginas auth sugeridas: `/confirm-email`, `/reset-password` (query `email` + `token`).

## Segurança

- Nunca logar ApiKey, tokens raw, nem nota interna.
- Outbox não persiste InternalOrderNote / dados de remessa interna.
- Idempotency única no banco evita duplicata.

## Checklist TESTE / produção

1. Migrar schema `notifications`
2. Configurar sender verificado no Brevo
3. `Brevo__Enabled=true` + ApiKey
4. Worker rodando (`EmailOutboxWorker`)
5. Registrar cliente → e-mail confirmação
6. Forgot password → e-mail reset
7. Criar pedido Pix → e-mail “Recebemos seu pedido”
8. Webhook Paid → e-mail pagamento
9. Ship/Deliver (pedido ou remessa) → e-mails por pedido
10. Desligar Brevo → outbox `Skipped`, API segue OK
