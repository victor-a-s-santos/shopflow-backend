# ORD-002 — Guest order status

Endpoint público limitado para acompanhamento de pedido convidado.

Ver detalhes de segurança e contrato em [`docs/security/SEC-006-guest-order-access-token.md`](../security/SEC-006-guest-order-access-token.md).

## Endpoint

`GET /api/orders/guest/{orderId}/status`  
Header: `X-ORDER-ACCESS-TOKEN`

## Status para UI Pix

| Order | Payment | UI sugerida |
|-------|---------|-------------|
| PendingPayment | Pending | Aguardando pagamento |
| Paid | Paid | Pagamento aprovado |
| Expired | * | Pedido expirado |
| Canceled | * | Pedido cancelado |
| PendingPayment | Failed | Pagamento não aprovado |

Order Paid é a fonte principal se houver divergência com Pix; inconsistências são logadas.
