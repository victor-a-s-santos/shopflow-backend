# Customer auth — e-mails

Confirm e reset passam por `IIdentityEmailSender` → `OutboxIdentityEmailSender` → outbox Brevo (pós-commit/best-effort; fora de `orders.email_intents`).

| Fluxo | Assunto | Link sugerido |
|-------|---------|---------------|
| Cadastro (técnico) | Confirme seu e-mail | `/confirm-email?email=&token=` |
| Esqueci senha | Redefinição de senha | `/reset-password?email=&token=` |
| Cadastro pendente / aprovado / recusado / suspenso | ver `docs/customer/customer-approval-emails.md` | `/account/pending-approval`, `/login` |

A confirmação de e-mail é **independente** da aprovação comercial.

Tokens **não** são logados em Information. Falha de enqueue não derruba o registro/forgot (log Error).

Config: `PublicApp__BaseUrl`, `PublicApp__AdminBaseUrl`, `AdminNotifications__ApprovalRequestsEmail`, `Brevo__*`, `EmailOutbox__*`.  
Detalhes: `docs/integrations/brevo-transactional-emails.md`, `docs/features/EMAIL-001-transactional-email-outbox-brevo.md`.
