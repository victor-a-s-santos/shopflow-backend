# MP-PIX-002 — Webhook confirmation (legado /payments)

> **Obsoleto como fluxo principal.** O Pix Mercado Pago do Shopflow usa Checkout Transparente via **Orders API**.
>
> Documento atualizado: [`MP-PIX-002-orders-provider-and-webhook.md`](./MP-PIX-002-orders-provider-and-webhook.md)

A implementação anterior baseada em `GET /v1/payments/{id}` e evento `payment` foi substituída por:

- Criação: `POST /v1/orders`
- Confirmação: `GET /v1/orders/{id}`
- Evento no painel: **Order**
- Paid somente com `processed` + `accredited`
