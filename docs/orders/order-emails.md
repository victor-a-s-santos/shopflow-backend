# E-mails de pedido

Ver contrato completo em `docs/integrations/brevo-transactional-emails.md` e arquitetura em `docs/features/EMAIL-001-transactional-email-outbox-brevo.md`.

Resumo:

- **Criado** → intent `orders.email_intents` no mesmo `SaveChanges` de `CreateOrderFromCheckoutSession` (payload pode incluir guest token raw)
- **Pago** → intent no mesmo `SaveChanges` de `Order.Paid` (`OrderPaidWriter`; `AlreadyPaid` repara se a intent faltava)
- **Enviado / Entregue** → intent por pedido no ship/deliver individual ou na remessa

O Worker (`OrderEmailIntentDispatcherWorker`) copia intents `Pending` para `notifications.email_outbox`. A Brevo só é chamada pelo `EmailOutboxWorker`.

Idempotência: `order:{orderId:D}:created|paid|shipped|delivered`.

Remessa (`DeliveryBatch`) reusa as mesmas keys por pedido de propósito. Não criar `batch-shipped:{batchId}` — duplicaria o e-mail. Templates não incluem batchId, nota interna nem GUID operacional.

Aprovação de cadastro: `docs/customer/customer-approval-emails.md`.
