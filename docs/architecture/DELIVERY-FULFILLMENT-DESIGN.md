# DELIVERY / FULFILLMENT — Design técnico

> Status: **design only** — não implementar migrations, endpoints ou UI completa neste documento.  
> Criado a partir da validação com cliente (assessoria de compras / lojistas e revendedores).  
> Relacionado: `docs/orders/customer-orders.md`, `docs/orders/admin-orders.md`, `docs/prompts/features/delivery-fulfillment-roadmap-and-p0-fixes-cursor.md`.

---

## 1. Contexto de negócio

Shopflow atende uma **assessoria de compras** cujo cliente típico é **lojista/revendedor**:

- várias compras intercaladas ao longo de dias/semanas;
- desejo frequente de **receber vários pedidos juntos**;
- operação admin precisa controlar **pagamento** (já existe) e **envio/entrega** (ainda não);
- métodos reais de entrega hoje: **transportadora**, **ônibus de excursão**, **Correios** — não necessariamente frete calculado online.

Portanto, Delivery/Fulfillment é uma **dimensão operacional separada** do ciclo Pix/OrderStatus.

---

## 2. Separação de status (regra dura)

| Dimensão | Responsabilidade | Exemplos |
|----------|------------------|----------|
| **Payment / Pix** | Cobrança | `Pending`, `Paid`, `Expired`, `Canceled`, `Failed` |
| **OrderStatus / customerStatus** | Comercial pós-checkout | `PendingPayment`, `Paid`/`Confirmed`, `Canceled`, `Expired` |
| **FulfillmentStatus** | Logística / envio | `AwaitingShipment`, `Shipped`, `Delivered` (+ futuros) |

**Não misturar:**

- não reutilizar `OrderStatus.Paid` para significar “enviado”;
- não inventar `Preparing`/`Shipped` em `customerStatus` até existir domínio real de fulfillment;
- admin e cliente devem ver pagamento e entrega como blocos distintos.

Hoje (`customerStatus`): `AwaitingPayment` | `Confirmed` | `Canceled` | `Expired` — **sem logística**. Isso permanece até a Fase 2.

---

## 3. DeliveryMethod

Códigos estáveis (API). Labels PT no frontend.

| Code | Label |
|------|--------|
| `Transportadora` | Transportadora |
| `OnibusExcursao` | Ônibus de excursão |
| `Correios` | Correios |

Captura sugerida:

- preferência no **checkout** (MVP Fase 2);
- método **efetivo** pode ser ajustado pelo admin no envio (pode diferir da preferência).

Sem integração de API de transportadora/Correios nesta fase.

---

## 4. PreferredDeliveryDate

- Data preferida pelo cliente (não garantia de SLA).
- **Mínimo: 2 dias úteis após a compra** (criação do pedido / pagamento confirmado — decidir na implementação: âncora = `CreatedAt` vs `PaidAt`; recomendação: **PaidAt** quando existir, senão `CreatedAt`).
- MVP: dia útil = **segunda a sexta** (sem feriados nacionais/municipais).
- Validar no backend; UI só guia.
- Feriados = dívida futura.

---

## 5. Observações

| Campo | Quem escreve | Quem vê | Onde nasce |
|-------|--------------|---------|------------|
| `customerOrderNote` | Cliente | Cliente + Admin | Checkout |
| `internalOrderNote` | Admin | Só Admin | Detalhe do pedido |
| `deliveryNote` | Admin | Só Admin (ou cliente se for tracking público futuro) | Bloco Entrega |
| `batchNote` | Admin | Só Admin | Remessa agrupada |

Não usar um único campo “observação” genérico misturando suporte e logística.

---

## 6. FulfillmentStatus (MVP)

### MVP (3 estados)

```text
AwaitingShipment → Shipped → Delivered
```

| Code | PT |
|------|-----|
| `AwaitingShipment` | Aguardando envio |
| `Shipped` | Enviado |
| `Delivered` | Entregue |

Transições:

- pedido só entra em `AwaitingShipment` quando pagamento/comercial estiver **confirmado** (`OrderStatus.Paid` / `customerStatus.Confirmed`);
- `Shipped` exige `shippedAt`; opcional `trackingCode`/`reference`, `deliveryMethod` efetivo;
- `Delivered` exige `deliveredAt`; tipicamente após `Shipped` (permitir atalho admin só com confirmação explícita se produto pedir).

### Futuro (não MVP)

- `Separating` / Em separação  
- `ReadyToShip` / Pronto para envio  
- `DeliveryIssue` / Problema na entrega  
- `Returned` / Devolvido  

---

## 7. Admin actions (Fase 2)

Por pedido:

- marcar como **enviado** (`shippedAt`, método, tracking/referência, observação);
- marcar como **entregue** (`deliveredAt`);
- editar `internalOrderNote` / `deliveryNote`.

Não alterar Pix/OrderStatus nessas ações.

---

## 8. Agrupamento — DeliveryBatch / ShipmentBatch (Fase 3)

### Objetivo

Agrupar pedidos **pagos** e `AwaitingShipment` do **mesmo cliente** para marcar envio/entrega em lote (ônibus, transportadora, Correios).

### Campos sugeridos

| Campo | Notas |
|-------|--------|
| `id` | Guid |
| `customerUserId` | nullable (guest) |
| `customerEmailNormalized` | para guest / fallback |
| `customerPhoneNormalized` | reforço guest |
| `deliveryMethod` | método da remessa |
| `status` | espelha lifecycle do batch (`Open`/`Shipped`/`Delivered`) |
| `orderIds` | N pedidos |
| `shippedAt` / `deliveredAt` | |
| `trackingCode` / `reference` | livre (ex.: nome do ônibus, código Correios) |
| `internalNote` / `batchNote` | |
| `createdAt` / `createdByAdminId` | auditoria |

### Critérios para agrupar

- `OrderStatus = Paid` (ou equivalente confirmado);
- `FulfillmentStatus = AwaitingShipment`;
- mesmo `customerUserId` quando existir;
- guest: **email normalizado + telefone** (nunca só nome);
- **alertar** se endereços de entrega divergirem (não bloquear silenciosamente).

### UX Admin (alvo)

- filtro “Aguardando envio”;
- detalhe: bloco **Entrega** + “Outros pedidos pendentes deste cliente”;
- seleção múltipla → “Criar entrega agrupada” / “Marcar enviados” / “Marcar entregues”.

---

## 9. UX Cliente (alvo)

**Checkout (Fase 2):**

- método de entrega preferido;
- data preferida (≥ 2 dias úteis);
- observação do cliente (`customerOrderNote`).

**Pós-compra:**

- mostrar preferências escolhidas;
- mostrar `FulfillmentStatus` quando existir;
- CTA **“Falar com vendedor pelo WhatsApp”** (antes de chat nativo).

---

## 10. WhatsApp vs chat nativo

| Etapa | Decisão |
|-------|---------|
| **MVP** | Botão WhatsApp com mensagem pré-preenchida (pedido #, nome, trecho do endereço). Número configurável (`WhatsApp__SalesPhone` ou similar). |
| **Futuro** | Chat nativo por pedido/cliente — só após decisão explícita de produto/ops. |

Não implementar chat real agora.

---

## 11. Roadmap por fases

### Fase 1 — Imediato (este prompt)

- bugs P0 estoque / categoria edit / CEP formatado;
- busca CEP via API Shopflow (`docs/integrations/postal-code-lookup.md`); ViaCEP só no backend;
- **este documento**.

### Fase 2 — Campos + status no pedido

- migrations: `DeliveryMethod?`, `PreferredDeliveryDate?`, `CustomerOrderNote?`, `InternalOrderNote?`, `FulfillmentStatus`, `ShippedAt`, `DeliveredAt`, `TrackingCode?`;
- checkout captura preferências;
- admin: marcar enviado/entregue;
- contratos OpenAPI + testes;
- `customerStatus` continua comercial; UI mostra bloco Entrega separado.

### Fase 3 — Agrupamento

- entidade `DeliveryBatch` / `ShipmentBatch`;
- APIs admin de criar batch e marcar lote;
- UX seleção múltipla + alerta de endereço divergente.

### Fase 4 — Contato

- WhatsApp CTA;
- chat nativo (se necessário).

---

## 12. Não fazer agora

- chat real;
- rastreamento automático Correios/transportadora;
- cálculo de frete;
- feriados;
- status avançados de fulfillment;
- remessa composta sem o design de batch acima;
- usar `OrderStatus` para simular entrega.

---

## 13. Impactos e breaking changes (quando implementar)

- novos campos nullable → backfill: pedidos pagos antigos → `AwaitingShipment`; demais null ou N/A;
- DTOs admin/customer/guest ganham bloco `fulfillment` sem remover payment;
- filtros admin `fulfillmentStatus=AwaitingShipment`;
- OpenAPI/Scalar atualizam automaticamente com novos endpoints.

---

## 14. Próximo prompt recomendado

`docs/prompts/features/delivery-fulfillment-phase-2-order-fields-cursor.md` (a criar):

1. migration + enums `DeliveryMethod` / `FulfillmentStatus`;
2. checkout + create order snapshots;
3. admin mark shipped/delivered;
4. testes + docs de contrato;
5. **sem** DeliveryBatch ainda.
