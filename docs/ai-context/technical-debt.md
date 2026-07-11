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
| **N+1 no Admin Inventory** | Listagem Estoque ainda pode usar GET individual | Melhorar listagem Admin Estoque |
| **Batch Product Edit** | ~~ausente~~ → `POST /api/admin/inventory/skus/availability` | Wiring frontend Product Edit |
| **Lint frontend (erros pré-existentes)** | `npm run lint` — `textarea.tsx`, `api.ts`, `tailwind.config.ts` | CI futuro falhará |
| **Upload local de imagens** | `Uploads__RootPath` filesystem | Não escala; sem CDN/R2 |
| **Frontend customer auth desconectado** | Backend `/api/auth/customer/*` pronto; UI visual-only | Login/conta não funcionam na loja |
| **Shipping pendente** | `ShippingAmount = null` | Frete sempre “a calcular” |
| **Dashboard admin com dados fake** | `AdminDashboard` — pedidos hardcoded | Métricas enganosas |
| **Race webhook vs worker** | Approved após expiração não auto-repara Order/reservas | Log crítico; intervenção manual |

---

## Baixa

| Dívida | Evidência | Impacto |
|--------|-----------|---------|
| **Catalog — cobertura de testes incompleta** | Upload/activate sem teste | Regressão possível |
| **Sem testes HttpApi** | Nenhum teste E2E na API | Contratos não validados automaticamente |
| **AuthContext `signInVisualOnly` não wired** | Login não chama helper | Preview de UI logada indisponível |
| **Integração tests skip sem Postgres** | `SHOPFLOW_TEST_DB` opcional | CI local inconsistente |
| **Chunk JS > 500kB** | Warning no Vite build | Performance inicial |

---

## Resolvido recentemente

| Item | Nota |
|------|------|
| Checkout frontend desconectado | Integrado com `POST /api/checkout/sessions` |
| Módulo Orders scaffold | MVP backend implementado (`PendingPayment`) |
| PaymentsPix scaffold | MVP backend com provider fake (`PixPayment` Pending) |
| Worker expiração checkout | `Vls.Shopflow.Worker` + `ExpirationProcessor` |
| IdentityAccess admin (Fase 1/2) | Cookie admin, CSRF, policy Backoffice |
| Demo catalog seed roupas | 10 produtos, 94 SKUs, imagens em `seed-assets/` |
| Mercado Pago Pix provider + webhook | QR real + Paid + confirma estoque (`MP-PIX-002`) |

---

## Reservado / conhecido (não é bug)

| Item | Nota |
|------|------|
| Checkout convidado | Decisão de produto |
| Auth visual-only no frontend | Backend customer pronto; integração UI pendente |
| Cypress bloqueia `/api/payments` no checkout | Correto até frontend integrar PaymentsPix |
| Pedido ≠ pagamento aprovado | `PendingPayment` até webhook Orders `processed`/`accredited` |

---

## Sugestão de ataque

1. Frontend customer auth + CSRF
2. Frontend QR Pix + status Paid
3. Batch inventory endpoint
