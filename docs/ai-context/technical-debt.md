# Shopflow — Dívidas técnicas

> Classificação: **Alta** (bloqueia produção ou integridade) · **Média** (impacto operacional/UX) · **Baixa** (melhoria incremental)

---

## Alta

| Dívida | Evidência | Impacto |
|--------|-----------|---------|
| **Admin sem guard no frontend** | Rotas `/admin/*` sem redirect no React; API protege escrita com `Backoffice` | Risco UX em ambiente exposto |
| **Sem CI/CD** | Nenhum pipeline no repo | Regressões manuais |

---

## Média

| Dívida | Evidência | Impacto |
|--------|-----------|---------|
| **N+1 no Admin Inventory** | ~~Backend sem listagem SKU~~ → `GET /api/admin/inventory/skus` pronto; FE ainda pode indexar via catalog/getProductById | Wiring FE Inventory Admin (`docs/inventory/admin-inventory-skus-listing.md`) |
| **Batch Product Edit** | ~~ausente~~ → `POST /api/admin/inventory/skus/availability` | Wiring frontend Product Edit |
| **Lint frontend (erros pré-existentes)** | `npm run lint` — `textarea.tsx`, `api.ts`, `tailwind.config.ts` | CI futuro falhará |
| **Upload local de imagens** | ~~só filesystem~~ → R2 via `Storage__*` (`docs/integrations/cloudflare-r2-product-images.md`); local permanece fallback | Blobs antigos: backfill **TEST** manual (`docs/qa/R2-TEST-PRODUCT-IMAGES-BACKFILL-REPORT.md`); prod TBD |
| **Checkout convidado desligado no cliente atual** | `Checkout:AllowGuest=false`; tracking legado permanece (`docs/orders/guest-order-access.md`) | Novos guest checkouts bloqueados; pedidos antigos ok |
| **Frontend store access / approval** | Backend Fase 1 pronto; UI ainda visual-only | Guards, tela Pending, fila admin, login unificado = Fase 2 |
| **Shipping / frete** | `ShippingAmount = null`; CEP lookup OK; frete calculado pendente | Cotação de frete |
| **Delivery FE** | Batch/fulfillment backend pronto; UI admin/checkout não | Frontend remessa + preferências |
| **Dashboard admin com dados fake** | `AdminDashboard` — pedidos hardcoded | Métricas enganosas |
| **Race webhook vs worker** | Approved após expiração não auto-repara Order/reservas | Log crítico; intervenção manual |

---

## Baixa

| Dívida | Evidência | Impacto |
|--------|-----------|---------|
| **Sales rules — FE pedidos** | Backend Fase 4 snapshot pronto (`salesDisplay` em OrderItem); FE ainda não consome | Pedidos sem copy de lote |
| **ProductCard N+1 by-slug** | Resolvido — FE usa `salesSummary` da listagem | ver `docs/catalog/product-list-sales-summary.md` + `apps/web/docs/product-card.md` |
| **Home sem “Carregar mais” / filtro client-side** | Backend paginação + `categorySlug` pronto; FE deve deixar de filtrar categoria no client | `docs/catalog/product-list-pagination-and-ordering.md` |
| **Admin Products usa listagem pública** | Backend `/api/admin/catalog/products` pronto; FE ainda limitado (~48) | `docs/catalog/admin-products-listing.md` |
| **Admin product form description/isActive wire** | Backend create/update/detail prontos; FE ainda pode strippar `description` no create e não hidratar no edit | `docs/catalog/admin-product-contract.md` |
| **Subcategorias na listagem** | Filtro é match exato de categoria | Árvore/filhos não incluídos |
| **Guest claim / tracking por e-mail** | OrderCreated inclui `?t=`; API public aceita `t`; **FE ainda precisa hidratar query** (EMAIL-002) | `docs/features/EMAIL-002-guest-order-link-validation.md` |
| **FE confirm/reset pages** | Backend envia links `/confirm-email` e `/reset-password` | Wiring UI customer auth |
| **Salvar produto admin multi-endpoint** | Create shell → variants → images separados; sem transação global | Persistência parcial se o FE falhar no meio; ver `docs/catalog/admin-product-contract.md` |
| **Arquivos de imagem órfãos** | Delete agora tenta remover objeto (R2/local); falha só logada | Lixo residual se storage falhar |
| **Sem testes HttpApi** | Nenhum teste E2E na API | Contratos não validados automaticamente |
| **AuthContext `signInVisualOnly` não wired** | Login não chama helper | Preview de UI logada indisponível |
| **Integração tests skip sem Postgres** | `SHOPFLOW_TEST_DB` opcional | CI local inconsistente |
| **Chunk JS > 500kB** | Warning no Vite build | Performance inicial |

---

## Resolvido recentemente

| Item | Nota |
|------|------|
| Checkout frontend desconectado | Integrado com `POST /api/checkout/sessions` |
| Módulo Orders scaffold | MVP + Admin + Customer orders (`CustomerUserId`); falta UI customer + claim guest |
| PaymentsPix scaffold | MVP backend com provider fake (`PixPayment` Pending) |
| Worker expiração checkout | `Vls.Shopflow.Worker` + `ExpirationProcessor` |
| IdentityAccess admin (Fase 1/2) | Cookie admin, CSRF, policy Backoffice |
| Demo catalog seed roupas | 10 produtos, 94 SKUs, imagens em `seed-assets/` |
| Mercado Pago Pix provider + webhook | QR real + Paid (`MP-PIX-002`); `notification_url` opcional; assinatura `mercadopago-sdk` 3.3.0; reconciliação Worker fallback; raw capture temp (`MP-PIX-003`) — remover após diagnóstico |

---

## Reservado / conhecido (não é bug)

| Item | Nota |
|------|------|
| Checkout convidado | Decisão de produto |
| Auth visual-only no frontend | Backend customer pronto; integração UI pendente |
| Cypress bloqueia `/api/payments` no checkout | Correto até frontend integrar PaymentsPix |
| Pedido ≠ pagamento aprovado | `PendingPayment` até webhook Orders `processed`/`accredited` |
| Simulação webhook painel MP | `data.id` tipo `123456` ≠ Order real; não confirma Paid — usar checkout que cria `ORD`/`ORDTST` |
| Atacado / pacotes / múltiplos | Design only — `docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md`; sem sales rules no código ainda; pacote composto e B2B ficam pós-MVP |

---

## Sugestão de ataque

1. Frontend customer auth + CSRF
2. Frontend QR Pix + status Paid
3. Batch inventory endpoint
