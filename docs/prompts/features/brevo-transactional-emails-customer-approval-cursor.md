Você está atuando como backend engineer sênior do projeto Shopflow.

Objetivo:
Implementar a Fase 3 do Store Access / Customer Approval: e-mails transacionais com Brevo (aprovação de cadastro + pedidos), reusando o outbox EMAIL-001.

Não implementar marketing, newsletter, SMS, WhatsApp Business, chat, frontend ou notification center completo.

Spec:
- `docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md`
- `docs/integrations/brevo-transactional-emails.md`
- `docs/customer/customer-approval-emails.md`
- `docs/prompts/features/brevo-transactional-emails-cursor.md` (EMAIL-001 — já implementado; não reconstruir outbox/worker/pedidos)

Resultado esperado: cadastro Pending enfileira admin+cliente; approve/reject/suspend enfileiram cliente; confirm/reset/order/paid/ship/deliver já existentes continuam; remessa reusa `order:{id}:shipped|delivered`; falha Brevo não quebra register/approve/checkout.
