# E-mails de pedido

Ver contrato completo em `docs/integrations/brevo-transactional-emails.md`.

Resumo:

- **Criado** → após `CreateOrderFromCheckoutSession` (inclui link guest com token se emitido)
- **Pago** → após transição Paid (webhook/reconciliação Mercado Pago)
- **Enviado / Entregue** → ship/deliver individual **ou** por pedido dentro de DeliveryBatch (sem vazar batch interno)

Idempotência: `order:{orderId}:created|paid|shipped|delivered`.
