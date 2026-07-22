# WHOLESALE-SALES-RULES — Design técnico (atacado / pacotes / múltiplos)

> Status: **design only** — sem implementação de código, migrations, endpoints ou UI neste documento.  
> Data: 2026-07-17  
> Prompt de origem: `docs/prompts/architecture/wholesale-sales-rules-design-cursor.md`  
> Baseado no estado atual de Catalog, Inventory, CartCheckout, Orders e carrinho frontend (`shopflow.cart.v1`).

---

## 1. Resumo executivo

O Shopflow precisa vender varejo e atacado no mesmo catálogo: unidade, mínimo, múltiplos e pacotes. Hoje **quantity = unidades do SKU** de ponta a ponta (carrinho → checkout → reserva → OrderItem), sem regra comercial.

**Recomendação MVP:**

| Decisão | Escolha |
|---------|---------|
| Onde fica a regra | **No SKU** (fonte da verdade) |
| Pacote fechado/sortido | **SKU próprio** (estoque em pacotes) |
| Significado de `quantity` | Sempre **unidades do SKU vendido** (1 pacote = quantity 1) |
| `packageSize` | Apenas **exibição** (peças informativas), não baixa estoque filho |
| Enforcement | Backend no **CreateCheckoutSession** (fonte da verdade) |
| Frontend | UX (selector, mensagens); nunca autoridade |
| Grade fechada / composição | **Pós-MVP** |
| Compatibilidade | SKUs existentes → `Unit` (min=1, step=1) |

Isso preserva Inventory, reservas e Orders sem redesenhar o fluxo de pagamento.

---

## 2. Requisito de negócio

O e-commerce também será usado para atacado. A regra deve:

1. Ser cadastrada no admin (produto/SKU).
2. Aparecer na vitrine (PDP).
3. Restringir quantidade no carrinho (UX).
4. Ser **obrigatoriamente** validada no checkout backend.
5. Não quebrar produtos unitários atuais.

Separar sempre:

- **Estoque:** quantas unidades do SKU existem (peças ou pacotes, conforme o SKU).
- **Regra de venda:** como o cliente pode comprar esse SKU.

---

## 3. Cenários de venda

| Cenário | Modo | Comportamento | Estoque |
|---------|------|---------------|---------|
| **A** Unitário atual | `Unit` | Compra 1, 2, 3…; escolhe variação | Unidades do SKU |
| **B** Mínimo 3 | `MinimumQuantity` | min=3, step=1 → 3, 4, 5… | Unidades do SKU |
| **C** Múltiplos de 3 | `MultipleQuantity` | min=3, step=3 → 3, 6, 9… | Unidades do SKU |
| **D** Pacote fechado 6 | `FixedPackage` | Compra N pacotes; 1 = 6 peças (display) | Unidades do SKU pacote |
| **E** Pacote sortido 12 | `AssortedPackage` | Compra N pacotes; sem escolha de cor unitária | Unidades do SKU pacote |
| **F** Grade fechada P/M/G | `ClosedGrid` | Composição fixa multi-SKU | **Pós-MVP** |

### Exemplos de mensagem (PT-BR)

- B: “Compra mínima: 3 peças.”
- C: “Este produto é vendido em múltiplos de 3.”
- D: “Pacote com 6 peças.” / “Quantidade de pacotes”
- E: “Pacote sortido com 12 peças.” / “Cores sortidas conforme disponibilidade.”

---

## 4. Decisão MVP

### 4.1 Princípios

1. **Não confundir peças com pacotes no Inventory.** Estoque continua por `SkuId` + `quantity` inteiro.
2. **Pacote = SKU vendável**, não composição automática no MVP.
3. **Backend valida sempre**; frontend só ajuda.
4. **Produtos atuais = Unit** sem mudança perceptível.
5. **Um produto pode misturar SKUs unitários e SKU(s) pacote** (ex.: Rosa M + Pacote sortido 6).

### 4.2 Pacote como SKU (escolhido)

```
Produto: Conjunto Flores
  SKU Rosa M     → MultipleQuantity (min 3, step 3)   estoque: peças
  SKU Azul M     → MultipleQuantity (min 3, step 3)   estoque: peças
  SKU PCT-SORT-6 → AssortedPackage (packageSize 6)    estoque: pacotes
```

**Vantagens para MVP**

- Compatível com Inventory atual (reserve/confirm/cancel por SKU).
- Checkout não precisa expandir linhas.
- OrderItem continua simples (`Quantity` = pacotes ou peças do SKU vendido).
- Risco baixo de regressão em Pix/worker/reservas.

**Desvantagens aceitas no MVP**

- Lojista controla estoque do pacote operacionalmente (não há baixa automática das cores).
- Não há garantia física de composição sortida no sistema.

### 4.3 Alternativa avançada (não MVP)

Pacote composto: 1 pacote → baixa N unidades de SKUs filhos (ex.: 2P + 2M + 2G). Exige grafo de composição, reserva multi-SKU atômica, falha parcial e UX de grade. **Pós-MVP (`ClosedGrid` / CompositePackage).**

---

## 5. O que fica pós-MVP

| Item | Motivo |
|------|--------|
| `ClosedGrid` / pacote composto multi-SKU | Complexidade de reserva e estoque |
| Tabela de preço por faixa (3→R$50, 12→R$42) | Pricing engine novo |
| Regras por cliente / aprovação B2B | Identity + políticas comerciais |
| Pedido mínimo global (R$ 500) | Regra de carrinho/checkout agregada |
| Produtos só-atacado (visibilidade) | Catálogo filtrado por papel |
| Frete por volume/peso | Shipping ainda scaffold |
| Mistura de variações para atingir mínimo (1 rosa+1 azul+1 preta) | Agregação cross-SKU — decisão de negócio aberta |
| Override hierárquico Product→SKU com entidade `SalesRule` versionada | Overengineering cedo |

---

## 6. Modelo de domínio recomendado

### 6.1 Onde fica a regra?

| Opção | Veredito |
|-------|----------|
| Só Product | Rejeitado para MVP — impede SKU pacote + SKU unitário no mesmo produto |
| Entidade `SalesRule` separada | Desnecessário no MVP; adiciona join sem ganho |
| **SKU (owned VO / colunas)** | **Escolhido** — alinhado a preço, estoque e checkout por `skuId` |
| Product default + SKU override | UX admin desejável; default pode ser “template” de aplicação, não autoridade |

**Autoridade:** `Sku.SalesRule`.  
**Admin UX:** seção “Configuração de venda” no formulário do produto, com:

- “Aplicar a todas as variantes” (escreve nos SKUs unitários).
- SKU marcado como pacote edita a própria regra (não herdada às cegas).

### 6.2 Sales modes (MVP)

```csharp
enum SalesMode
{
    Unit = 0,
    MinimumQuantity = 1,
    MultipleQuantity = 2,
    FixedPackage = 3,
    AssortedPackage = 4
    // ClosedGrid = 5  // pós-MVP
}
```

### 6.3 Value object proposto: `SkuSalesRule`

Campos canônicos:

| Campo | Tipo | Papel |
|-------|------|-------|
| `SalesMode` | enum | Modo |
| `MinimumQuantity` | int | Mínimo comprável (`>= 1`) |
| `QuantityStep` | int | Incremento (`>= 1`) |
| `PackageSize` | int? | Peças por unidade de venda (null se não pacote) |
| `PackageLabel` | string? | Ex.: “Lote com 3 peças” (ou pacote/kit/caixa) |
| `PackageDescription` | string? | Ex.: “Cores sortidas conforme disponibilidade.” |
| `QuantityUnitLabel` | string? | Ex.: “lote(s)” / “pacote(s)” / “kit(s)” / “peça(s)” — default por modo |
| `AllowCustomerToChooseVariants` | bool | Se false, PDP não exige seletor de variação “unitária” para aquele SKU |
| `ShowTotalPieces` | bool | Exibir “2 pacotes = 12 peças” |
| `IsWholesaleOnly` | bool | Reservado; **ignorado na vitrine MVP** (sem gating B2B ainda) |

Defaults por modo (aplicação + validação admin):

| Mode | Min | Step | PackageSize | AllowChooseVariants | ShowTotalPieces |
|------|-----|------|-------------|---------------------|-----------------|
| Unit | 1 | 1 | null | true | false |
| MinimumQuantity | >1 | 1 | null | true | false |
| MultipleQuantity | ≥ step | >1 | null | true | false |
| FixedPackage | ≥1 | ≥1 | >1 | configurável (default true se houver attrs) | true |
| AssortedPackage | ≥1 | ≥1 | >1 | **false** | true |

### 6.4 Semântica de `quantity` (contrato universal)

> **`quantity` sempre representa unidades do SKU vendido.**

| Modo | quantity=2 significa | Reserva Inventory | Peças físicas (info) |
|------|----------------------|-------------------|----------------------|
| Unit / Min / Multiple | 2 peças daquele SKU | 2 | 2 |
| FixedPackage / AssortedPackage | 2 lotes (unidades do SKU pacote) | 2 | `2 * packageSize` (só display) |

**Não** enviar 12 no checkout para “2 lotes de 6” se o SKU for pacote — isso quebraria estoque.

> **Terminologia (negócio):** a referência visual do cliente usa **lote** (1 lote = 1 unidade vendável do SKU; preço do SKU = preço por lote; unitário display = preço / `packageSize`). Nomes técnicos `Package*` permanecem. Labels de exibição via `QuantityUnitLabel` / `PackageLabel` (lote, pacote, kit, caixa…). `FixedPackage` ≠ sortido; `AssortedPackage` = sortido.

### 6.5 Fórmula de quantidade válida

```
quantity >= MinimumQuantity
AND (quantity - MinimumQuantity) % QuantityStep == 0
```

Equivalente e mais restritiva quando `MinimumQuantity % QuantityStep != 0` é inválida na configuração.

**Regra de configuração (admin):**

- `MultipleQuantity`: `MinimumQuantity % QuantityStep == 0` (ex.: min 3 step 3; min 6 step 3; **não** min 4 step 3).
- Pacotes: tipicamente `MinimumQuantity = 1`, `QuantityStep = 1` (comprar 1, 2, 3 pacotes).

---

## 7. Modelo de dados proposto

Schema `catalog`, tabela `product_skus` (colunas novas):

| Coluna | Tipo | Default migração |
|--------|------|------------------|
| `sales_mode` | smallint / text enum | `0` (Unit) |
| `minimum_quantity` | int | `1` |
| `quantity_step` | int | `1` |
| `package_size` | int NULL | NULL |
| `package_label` | varchar(120) NULL | NULL |
| `package_description` | varchar(500) NULL | NULL |
| `quantity_unit_label` | varchar(40) NULL | NULL |
| `allow_customer_to_choose_variants` | bool | `true` |
| `show_total_pieces` | bool | `false` |
| `is_wholesale_only` | bool | `false` |

Constraints sugeridos (check ou validação de domínio):

- `minimum_quantity >= 1`
- `quantity_step >= 1`
- `package_size IS NULL OR package_size > 1`
- modos pacote exigem `package_size` e `package_label` (label pode ser gerado: `"Lote com {n} peças"`)

**Não criar tabela `sales_rules` no MVP.**

### Order / Checkout snapshots (fase 4)

`checkout_session_items` e `order_items` — campos opcionais:

| Campo | Motivo |
|-------|--------|
| `sales_mode` | Histórico |
| `package_size` | Histórico |
| `package_label` | Exibição estável |
| `quantity_unit_label` | “lote(s)” / “pacote(s)” vs “peça(s)” |
| `total_pieces` | `quantity * package_size` ou `quantity` |

Mínimo aceitável MVP orders: `package_label` + `total_pieces` (ou um único `sale_display_label`).  
Pedidos antigos sem snapshot: UI mostra só `quantity` (comportamento atual).

---

## 8. Contratos de API propostos

### 8.1 Admin — create/update SKU

Estender payloads de `POST/PUT .../variants` e respostas `SkuDto`:

```json
{
  "code": "ROSA-M",
  "regularPrice": 49.9,
  "salesRule": {
    "salesMode": "MultipleQuantity",
    "minimumQuantity": 3,
    "quantityStep": 3,
    "packageSize": null,
    "packageLabel": null,
    "packageDescription": null,
    "quantityUnitLabel": "peça(s)",
    "allowCustomerToChooseVariants": true,
    "showTotalPieces": false,
    "isWholesaleOnly": false
  }
}
```

Lote / pacote (exemplo sortido):

```json
{
  "code": "PCT-SORT-6",
  "regularPrice": 199.9,
  "salesRule": {
    "salesMode": "AssortedPackage",
    "minimumQuantity": 1,
    "quantityStep": 1,
    "packageSize": 6,
    "packageLabel": "Lote sortido com 6 peças",
    "packageDescription": "Cores sortidas conforme disponibilidade.",
    "quantityUnitLabel": "lote(s)",
    "allowCustomerToChooseVariants": false,
    "showTotalPieces": true,
    "isWholesaleOnly": false
  }
}
```

Preço do SKU em modo lote/pacote = **preço por lote** (unidade vendável). Valor unitário de display = `preçoEfetivo / packageSize`.

### 8.2 Storefront — `GET /catalog/products/by-slug/{slug}`

Incluir em cada `SkuDto`:

- `salesRule` (mesmo shape do admin read);
- `salesRuleDisplay` quando `FixedPackage` / `AssortedPackage` (null caso contrário), com labels e `equivalentRegularUnitPrice` / `equivalentPromotionalUnitPrice` arredondados a 2 casas no backend (`AwayFromZero`), para a UI montar “Unidades no lote”, “Preço por lote”, “Valor unitário” sem divergência de arredondamento.

Ver `docs/catalog/sales-rules-contract.md`.

### 8.3 Checkout — erros (ProblemDetails)

Validação em `CreateCheckoutSession` (após consolidar por `skuId`):

| Campo | Mensagem exemplo | errorCode (sugestão) |
|-------|------------------|----------------------|
| `quantity` | “Quantidade mínima deste produto é 3.” | `SALES_MIN_QUANTITY` |
| `quantity` | “Este produto é vendido em múltiplos de 3.” | `SALES_QUANTITY_STEP` |
| `skuId` | “Este SKU só pode ser comprado como pacote.” | `SALES_PACKAGE_ONLY` |
| `quantity` | “Pacote inválido para este produto.” | `SALES_PACKAGE_INVALID` |

HTTP 400 ValidationProblemDetails com `traceId` (padrão atual HttpApi).

Estoque insuficiente permanece com contrato Inventory já existente (409 / códigos atuais).

### 8.4 Orders — detalhe Admin/Customer/Guest

Se snapshot existir:

```json
{
  "skuId": "...",
  "productName": "Conjunto Flores",
  "skuCode": "PCT-SORT-6",
  "quantity": 2,
  "unitPrice": 199.9,
  "subtotal": 399.8,
  "salesDisplay": {
    "salesMode": "AssortedPackage",
    "packageLabel": "Lote sortido com 6 peças",
    "quantityUnitLabel": "lote(s)",
    "totalPieces": 12
  }
}
```

---

## 9. Impacto no admin

Seção **“Configuração de venda”** no Product Form / Edit (por variante ou apply-all):

1. Select: Unidade | Quantidade mínima | Múltiplos | Lote/pacote fechado | Lote/pacote sortido.
2. Campos condicionais (min, step, packageSize, labels, showTotalPieces, descrição).
3. Validação FE espelhando regras de domínio (UX).
4. Inventory admin: label de unidade = `quantityUnitLabel` (“lotes” / “pacotes” vs “unidades”) quando modo pacote — melhora operacional, não muda API Inventory.

**Não** exigir `IsWholesaleOnly` gating no MVP.

---

## 10. Impacto na vitrine (PDP)

| Modo | UX |
|------|----|
| Unit | Contador atual |
| MinimumQuantity | Contador inicia em min; mensagem de mínimo |
| MultipleQuantity | Step no +/- ; bloqueio de digitação inválida; mensagem de múltiplo |
| FixedPackage | Label pacote; “Qtd de pacotes”; opcional total peças |
| AssortedPackage | SKU pacote selecionável (ou único SKU); **sem** seletor de cor unitária; descrição sortida |

**Pacote sortido como SKU selecionável:** sim — aparece como opção de “variação/SKU” (ex.: atributo “Tipo = Pacote sortido” ou SKU sem attrs de cor). O seletor de cor/tamanho unitário some quando o SKU ativo tem `AllowCustomerToChooseVariants = false`.

Disponível:

- Unit/Multiple: “X disponíveis” (peças).
- Pacote: “X pacotes disponíveis” (estoque do SKU pacote). Opcional secundário: “≈ X×packageSize peças”.

---

## 11. Impacto no carrinho

Estado atual: `shopflow.cart.v1`, item por `skuId` + `quantity` + display fields.

### Adaptação sem quebrar

1. Bump de versão do storage para `shopflow.cart.v2` **ou** campos opcionais em v1 com defaults Unit.
2. Ao adicionar/atualizar, persistir snapshot leve da regra (para UX offline):

```ts
salesRuleSnapshot?: {
  salesMode: string;
  minimumQuantity: number;
  quantityStep: number;
  packageSize?: number | null;
  packageLabel?: string | null;
  quantityUnitLabel?: string | null;
  showTotalPieces?: boolean;
}
```

3. Ao abrir carrinho / antes do checkout: revalidar contra `by-slug` ou endpoint de SKUs (regra pode ter mudado) e estoque.
4. Mensagens: mínimo, múltiplo, pacote, estoque insuficiente.
5. Payload checkout **continua** `{ skuId, quantity }` — backend recalcula preço e revalida regra.

Carrinho stale (regra mudou): ajustar quantity para o próximo válido ou marcar item inválido até o cliente corrigir.

---

## 12. Impacto no checkout

### CreateCheckoutSession (obrigatório)

Para cada linha consolidada:

1. Resolver SKU ativo + product ativo (já existe).
2. Carregar `SkuSalesRule`.
3. Validar fórmula de quantity.
4. Validar pacote configurado se modo pacote.
5. Precificar `UnitPrice` do SKU (já existe).
6. `ReserveAsync(skuId, quantity, …)` — **mesma quantity**.

Nenhuma mudança em PaymentsPix, worker de expiração ou confirmação de reserva por pagamento.

### CheckoutSessionItem

Fase 1: validação sem novos campos.  
Fase 4: copiar snapshot de display para propagar ao OrderItem.

---

## 13. Impacto no estoque

| Modelo | MVP? | Comportamento |
|--------|------|---------------|
| A — unidade/múltiplo | Sim | quantity = peças do SKU |
| B — pacote como SKU | Sim | quantity = pacotes; `packageSize` display |
| C — pacote composto | Não | baixa multi-SKU |

Admin Inventory: preferir label “pacotes” quando `SalesMode` ∈ {FixedPackage, AssortedPackage}. API Inventory permanece agnóstica (`QuantityOnHand` etc.).

---

## 14. Impacto em orders

Hoje `OrderItem`: `SkuId`, `ProductName`, `SkuCode`, `Quantity`, `UnitPrice`, `Subtotal`.

| Necessidade | MVP fase | Ação |
|-------------|----------|------|
| Totais / Pix | — | Sem mudança |
| Exibir “2 pacotes de 6” após mudança de catálogo | Fase 4 | Snapshot mínimo |
| Admin/Customer/Guest order detail | Fase 4 | Expor `salesDisplay` |

Sem snapshot, pedido antigo pode “mudar de aparência” se a UI reler o catálogo atual — **risco HIGH** mitigado na fase 4.

Payments: sem impacto (total monetário inalterado).

---

## 15. Regras de validação

### 15.1 Backend — configuração (admin write)

| Mode | Regras |
|------|--------|
| Unit | Forçar min=1, step=1; package* null |
| MinimumQuantity | min > 1; step = 1; package* null |
| MultipleQuantity | step > 1; min ≥ 1; **min % step == 0**; package* null |
| FixedPackage | packageSize > 1; packageLabel obrigatório ou gerado; min≥1; step≥1 |
| AssortedPackage | igual FixedPackage; `AllowCustomerToChooseVariants = false` |

### 15.2 Backend — compra (checkout)

```
if quantity < MinimumQuantity → SALES_MIN_QUANTITY
else if (quantity - MinimumQuantity) % QuantityStep != 0 → SALES_QUANTITY_STEP
```

Pacote com config inválida no catálogo: falha de configuração (não deveria chegar à vitrine ativa) → 400 genérico / log.

### 15.3 Frontend — apenas UX

- Stepper, mensagens, defaults de quantidade.
- Revalidação no carrinho.
- **Nunca** confiar só no FE para checkout.

---

## 16. Estratégia de migration

1. Add nullable/defaulted columns em `product_skus` com defaults Unit (1/1).
2. Backfill explícito: todos os SKUs existentes → Unit.
3. Deploy API que lê defaults se coluna ausente (defesa).
4. Só depois tornar NOT NULL onde aplicável (`sales_mode`, `minimum_quantity`, `quantity_step`, bools).
5. Orders/Checkout snapshots em migration **separada** (fase 4), nullable, sem backfill obrigatório.

**Não quebra:** carrinho antigo (quantity≥1), checkout atual, orders históricos.

Seed demo: permanece Unit até alguém configurar atacado nos produtos demo.

---

## 17. Plano de implementação por fases

### Fase 0 — Design e docs ✅

- Este documento.
- Atualização leve em `docs/ai-context/*`.

### Fase 1 — Backend domínio + validação

- `SkuSalesRule` + enum no Catalog Domain.
- Migration `product_skus`.
- Extender commands/DTOs admin + by-slug.
- FluentValidation admin.
- CartCheckout: enforce na criação de sessão + testes unit/integration.
- ProblemDetails codes.
- **Sem** UI.

### Fase 2 — Frontend admin

- Seção Configuração de venda.
- Apply-all + edição por SKU pacote.
- Labels de estoque opcionais.

### Fase 3 — Storefront + cart

- PDP selector/mensagens.
- Cart v2 / snapshot + revalidação.
- Mensagens de erro alinhadas aos codes.

### Fase 4 — Checkout UX + Orders snapshot

- Mensagens no checkout se 400.
- ~~Snapshot em CheckoutSessionItem → OrderItem.~~ **Feito** — `docs/orders/order-item-sales-snapshot.md`
- ~~Admin/Customer/Guest order display (API).~~ **Feito** (`salesDisplay`); FE pendente.

### Fase 5 — Pós-MVP

- Composição / ClosedGrid.
- Tier pricing.
- B2B gating / pedido mínimo global.
- Regras por cliente.

---

## 18. Riscos

| Risco | Severidade | Mitigação |
|-------|------------|-----------|
| Confundir peças vs pacotes em `quantity` | **BLOCKER** | Contrato documentado: quantity = unidades do SKU; testes explícitos pacote vs múltiplo |
| Checkout aceitar quantity inválida | **BLOCKER** | Enforce só no backend + testes |
| Estoque incorreto (baixar 12 em vez de 2) | **BLOCKER** | Pacote-as-SKU; nunca expandir packageSize na reserva |
| Pedido antigo muda exibição | **HIGH** | Snapshot fase 4 |
| Admin cadastra regra incoerente (min 4 step 3) | **HIGH** | Validação domínio + FE |
| Cliente compra unidade quando deveria ser pacote | **HIGH** | SKU pacote separado; Assorted sem choose variants |
| Carrinho localStorage stale | **MEDIUM** | Revalidação ao abrir/checkout; bump key |
| Pacote sortido “exigir” composição real | **MEDIUM** | Explicitamente fora do MVP; copy operacional |
| Migration quebra produtos existentes | **MEDIUM** | Defaults Unit; backfill; deploy dual-read |
| `IsWholesaleOnly` sem auth | **LOW** | Campo reservado; ignorar na vitrine MVP |
| Expectativa de misturar cores p/ mínimo | **MEDIUM** | Pergunta de negócio; default MVP = por SKU |

---

## 19. Perguntas em aberto para o negócio

1. **Um produto pode ter venda unitária e pacote ao mesmo tempo?**  
   Design MVP permite via SKUs distintos. Confirmar se o lojista quer isso no mesmo produto ou produtos separados.

2. **Pacote sortido: estoque próprio ou consome cores individuais?**  
   MVP assume estoque próprio. Composição real = pós-MVP.

3. **Preço do pacote é por pacote ou por peça?**  
   MVP: preço do SKU = **por unidade de venda** (pacote). Preço/peça só display (`unitPrice / packageSize`) se desejado.

4. **Cliente atacado pode misturar variações para atingir o mínimo?** (1 rosa + 1 azul + 1 preta = 3)  
   MVP: **não** — mínimo/múltiplo é **por SKU**. Mistura cross-SKU = decisão explícita pós-MVP.

5. **O mínimo é por SKU, por produto ou por carrinho?**  
   MVP: **por SKU**. Pedido mínimo de carrinho = pós-MVP.

6. **Será necessário preço por faixa?**  
   Fora do MVP. Se sim, priorizar após fases 1–4.

7. **Atacado será público ou só clientes aprovados?**  
   MVP: regras públicas na vitrine. Gating B2B = pós-MVP (`IsWholesaleOnly` reservado).

8. **Existem produtos que só aparecem para atacado?**  
   Não no MVP.

9. **Pedido mínimo global (ex.: R$ 500)?**  
   Não no MVP.

10. **Pacote fechado precisa mostrar composição exata?**  
    MVP: label/descrição texto livre. Grade fechada estruturada = pós-MVP.

---

## 20. Critérios de aceite (implementação futura)

### Fase 1 (backend)

- [ ] SKUs existentes migrados como Unit sem regressão de checkout.
- [ ] Admin pode persistir regra por SKU.
- [ ] by-slug retorna `salesRule`.
- [ ] Checkout rejeita quantity inválida com ProblemDetails claros.
- [ ] Reserva Inventory usa a mesma `quantity` enviada (pacote = N pacotes).
- [ ] Testes: Unit, Min, Multiple, FixedPackage, AssortedPackage + casos inválidos.
- [ ] Nenhuma mudança em PaymentsPix / worker necessária.

### Fase 2–3 (UI)

- [ ] Admin edita regra sem inconsistências.
- [ ] PDP e carrinho respeitam min/step/pacote.
- [ ] Carrinho revalida regra stale.

### Fase 4

- [ ] Pedido histórico mostra pacotes corretamente após mudança de catálogo.

### Não aceitar no MVP

- [ ] Baixa automática multi-SKU.
- [ ] Tier pricing.
- [ ] Mínimo cross-SKU / global.
- [ ] Portal B2B aprovado.

---

## 21. Próximo prompt recomendado

Criar e executar um prompt de implementação backend, por exemplo:

`docs/prompts/features/wholesale-sales-rules-backend-cursor.md`

Escopo sugerido do próximo prompt (= **Fase 1**):

1. Domain `SalesMode` + `SkuSalesRule` em Catalog.
2. EF migration + mapping `product_skus`.
3. Extender Add/Update SKU + `SkuDto` + by-slug.
4. Validators FluentValidation.
5. `CreateCheckoutSession` enforcement + error codes.
6. Unit + integration tests.
7. Docs de contrato (`docs/catalog/sales-rules-contract.md`).
8. **Sem** frontend admin/storefront neste prompt.

---

## Apêndice A — Comparativo de opções de modelagem

| Critério | Product-only | SKU-owned (escolhido) | Entidade SalesRule |
|----------|--------------|------------------------|--------------------|
| Unit + pacote no mesmo produto | ❌ | ✅ | ✅ |
| Simplicidade migration | ✅ | ✅ | ⚠️ |
| Alinhamento checkout/estoque | ⚠️ | ✅ | ✅ |
| Overrides futuros | Override depois | Nativo | Nativo |
| Overfetch vitrine | Menor | Por SKU (ok) | Join |

## Apêndice B — Estado atual (baseline)

| Camada | Hoje |
|--------|------|
| Product / Sku | Sem sales fields |
| Cart FE | `shopflow.cart.v1` — `skuId`, `quantity` |
| Checkout | `Quantity > 0` apenas |
| Inventory | Reserve por `skuId` + quantity |
| OrderItem | Snapshot sales display (`SalesMode`, `PackageSize`, `TotalPieces`, …) + DTO `salesDisplay` |

Qualquer implementação deve preservar esse baseline para `SalesMode = Unit`.
