# Store access no catálogo

Quando `StoreAccess:Mode=Closed` (`PrivateCatalogApprovedOnly`), a vitrine exige cookie customer **Approved**.

Protegidos:

- `GET /api/catalog/products`
- `GET /api/catalog/products/{id}`
- `GET /api/catalog/products/by-slug/{slug}`
- `GET /api/catalog/categories`
- `GET /api/catalog/attributes`
- `GET /api/inventory/skus/{skuId}` (disponibilidade de vitrine)

Staff Backoffice não é bloqueado. Health, auth, CEP permanecem públicos.

Em `Open`, o catálogo público continua anônimo.

| Situação | HTTP | code |
|----------|------|------|
| Closed sem login | 401 | `STORE_ACCESS_REQUIRES_LOGIN` |
| Closed Pending/não aprovado | 403 | `STORE_ACCESS_REQUIRES_APPROVAL` |
| Closed Rejected/Suspended | 403 | `CUSTOMER_ACCESS_REJECTED` / `CUSTOMER_ACCESS_SUSPENDED` |
| Closed Approved | 200 | — |
| Open anônimo | 200 | — |

Contrato anônimo: `GET /api/store/access` (`mode=Open|Closed`).
