# Customer auth — e-mails

Confirm e reset passam por `IIdentityEmailSender` → `OutboxIdentityEmailSender` → outbox Brevo.

| Fluxo | Assunto | Link sugerido |
|-------|---------|---------------|
| Cadastro | Confirme seu cadastro | `/confirm-email?email=&token=` |
| Esqueci senha | Redefinição de senha | `/reset-password?email=&token=` |

Tokens **não** são logados em Information. Falha de enqueue não derruba o registro/forgot (log Error).

Config: `PublicApp__BaseUrl`, `Brevo__*`, `EmailOutbox__*`.  
Detalhes: `docs/integrations/brevo-transactional-emails.md`.
