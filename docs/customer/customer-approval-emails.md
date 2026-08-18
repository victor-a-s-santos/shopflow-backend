# E-mails de aprovação de cadastro (Fase 3)

Fase 3 do Store Access / Customer Approval. Reusa o outbox EMAIL-001 (`notifications.email_outbox` + `EmailOutboxWorker` + Brevo). **Não** usa `orders.email_intents`.

Frontend, marketing, SMS, WhatsApp Business e notification center **não** entram nesta fase.

Contrato de acesso da loja: `docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md`.  
Provider/outbox: `docs/integrations/brevo-transactional-emails.md`.

## Eventos

| Evento | Destino | Assunto | IdempotencyKey |
|--------|---------|---------|----------------|
| Cadastro pendente (admin) | `AdminNotifications:ApprovalRequestsEmail` | Novo cadastro aguardando aprovação | `customer:{id}:approval-request-admin` |
| Cadastro recebido (cliente) | e-mail do cliente | Recebemos sua solicitação de cadastro | `customer:{id}:registration-received` |
| Aprovado | cliente | Seu acesso foi aprovado | `customer:{id}:approved:{utcTicks}` |
| Recusado | cliente | Atualização sobre seu cadastro | `customer:{id}:rejected:{utcTicks}` |
| Suspenso | cliente | Atualização sobre seu acesso | `customer:{id}:suspended:{utcTicks}` |

`utcTicks` é o `AccessDecidedAt` da decisão. Assim, reativar depois de suspender envia um segundo e-mail de aprovado, sem colidir com o primeiro.

Cadastro Open (`RequireApproval=false`) **não** dispara os e-mails de pendência. Confirmação de e-mail (técnica) continua independente da aprovação comercial.

Inbox admin vazia: o e-mail operacional é ignorado (log Warning); o cliente ainda recebe “cadastro recebido”.

## Conteúdo (PT-BR)

Templates HTML no código (`TransactionalEmailTemplates`). Sem TemplateId do painel Brevo nesta fase.

- Admin: nome, e-mail, telefone, data da solicitação, CTA `/admin/customers/approvals`.
- Cliente pendente: “em análise”, **sem** SLA prometido, CTA `/account/pending-approval`.
- Aprovado: CTA `/login`.
- Recusado / suspenso: texto genérico. **Nunca** inclui `AccessDecisionReason`.

## Disparo

`ICustomerAccessNotifier` (`OutboxCustomerAccessNotifier` quando o módulo Notifications está registrado; stub de log caso contrário).

- Register Closed → `NotifyRegisteredPendingAsync` (admin + cliente), pós-commit, `try/catch`.
- Approve / reject / suspend / reactivate → e-mail correspondente só se o status **mudou**.
- Falha de enqueue **não** quebra cadastro nem decisão admin.

## Configuração

```
AdminNotifications__ApprovalRequestsEmail=ops@seudominio.com.br
PublicApp__BaseUrl=https://loja.exemplo.com.br
PublicApp__AdminBaseUrl=https://admin.exemplo.com.br
PublicApp__StoreName=Vip Assessoria
Brevo__Enabled=true
Brevo__ApiKey=...
Brevo__SenderEmail=no-reply@seudominio.com.br
Brevo__SenderName=VIP Assessoria Digital
Brevo__ReplyToEmail=atendimento@seudominio.com.br
Brevo__SandboxMode=true
```

Aliases: `AppUrls:StorefrontBaseUrl` / `AppUrls:AdminBaseUrl`.

## Fora de escopo

- Marketing, newsletter, SMS, WhatsApp Business, chat
- UI de logs de e-mail / notification center
- Alterar policy de StoreAccess, Pix, DeliveryBatch ou R2
- Frontend (Fase 2)
