# E-mails de pedido

Ver contrato completo em `docs/integrations/brevo-transactional-emails.md` e arquitetura em `docs/features/EMAIL-001-transactional-email-outbox-brevo.md`.

Resumo:

- **Criado** → intent `orders.email_intents` no mesmo `SaveChanges` de `CreateOrderFromCheckoutSession` (payload pode incluir guest token raw)
- **Pago** → intent no mesmo `SaveChanges` de `Order.Paid` (`OrderPaidWriter`; `AlreadyPaid` repara se a intent faltava)
- **Enviado / Entregue** → intent por pedido no ship/deliver individual ou na remessa
- **Estoque confirmado** → intent `OrderStockConfirmed` no `confirm-stock` (só na primeira confirmação)

O Worker (`OrderEmailIntentDispatcherWorker`) copia intents `Pending` para `notifications.email_outbox`. A Brevo só é chamada pelo `EmailOutboxWorker`.

Idempotência: `order:{orderId:D}:created|paid|stock-confirmed|shipped|delivered`.

`confirm-stock` repetido não cria segunda intent. Repair automático de `OrderStockConfirmed` só cobre Paid + AwaitingShipment com `StockConfirmedAt` (não dispara e-mail em pedidos antigos Shipped/Delivered backfilled). Template: assunto `Pedido #{orderNumber} confirmado em estoque`; corpo sem nota interna e sem admin id. Falha de enqueue não quebra a ação admin (`EnsurePending` no mesmo `SaveChanges`; dispatcher/outbox isolados).

Remessa (`DeliveryBatch`) reusa as mesmas keys por pedido de propósito. Não criar `batch-shipped:{batchId}` — duplicaria o e-mail. Templates não incluem batchId, nota interna nem GUID operacional.

Aprovação de cadastro: `docs/customer/customer-approval-emails.md`.
