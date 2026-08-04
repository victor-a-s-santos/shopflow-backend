# Backend next actions (alias)

Fonte canônica deste arquivo: [`next-actions.md`](./next-actions.md).

Delivery/Fulfillment Fase 2+3 (backend): [`docs/orders/delivery-fulfillment-phase-2.md`](../orders/delivery-fulfillment-phase-2.md), [`docs/orders/delivery-batch-phase-3.md`](../orders/delivery-batch-phase-3.md). Design: [`docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md`](../architecture/DELIVERY-FULFILLMENT-DESIGN.md) — FE e WhatsApp/chat ainda pendentes.

Postal code lookup: [`docs/integrations/postal-code-lookup.md`](../integrations/postal-code-lookup.md) — `GET /api/integrations/postal-code/br/{cep}` (Shipping + ViaCEP no backend).

Product images R2: [`docs/integrations/cloudflare-r2-product-images.md`](../integrations/cloudflare-r2-product-images.md) — backend S3-compatible; FE só consome URL pública. Backfill TEST: [`docs/qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md`](../qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md).
