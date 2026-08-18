Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, CQRS, EF Core, PostgreSQL, Orders, CheckoutSession, Catalog, Inventory e contratos de e-commerce.

Objetivo:
Implementar a Fase 4 backend das sales rules: salvar snapshot da regra de venda no CheckoutSessionItem/OrderItem e expor dados de exibição de lote/pacote nos pedidos Admin, Customer e Guest.

Escopo desta fase:
- Persistir snapshot mínimo de sales rule no momento do checkout/order creation.
- Garantir que pedidos antigos continuem legíveis mesmo se o SKU mudar depois.
- Expor salesDisplay nos DTOs de pedidos.
- Não alterar frontend nesta fase.
- Não alterar regras de checkout já implementadas.
- Não alterar Inventory.
- Não alterar PaymentsPix.
- Não implementar pacote composto, tier pricing, B2B ou mínimo global.

Base obrigatória:
- docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md
- docs/catalog/sales-rules-contract.md
- docs/orders/customer-orders.md
- docs/orders/admin-orders.md
- docs/orders/post-pix-guest-flow.md

==================================================
1. CONTEXTO
==================================================

Fase 1 backend concluída:
- SalesMode + SkuSalesRule no SKU.
- salesRule por SKU.
- salesRuleDisplay no storefront para FixedPackage/AssortedPackage.
- Checkout valida minimumQuantity/quantityStep/pacote.
- Inventory reserva line.Quantity sem multiplicar packageSize.

Fase 2 frontend admin concluída:
- Admin configura salesRule por SKU.

Fase 3 storefront/carrinho concluída:
- PDP e carrinho respeitam salesRule.
- Checkout payload continua:
  { skuId, quantity }
- Para lote:
  quantity = quantidade de lotes/pacotes, não peças.

Pendência:
Hoje OrderItem não possui snapshot da salesRule.
Se o lojista alterar o SKU depois, um pedido antigo pode perder ou mudar a exibição:
- antes: 2 lotes de 3 peças = 6 peças;
- depois: SKU alterado para lote de 6;
- pedido antigo não pode passar a parecer 2 lotes de 6 peças.

==================================================
2. PRINCÍPIO CENTRAL
==================================================

No momento da criação do CheckoutSession/Order, salvar um snapshot da regra comercial usada naquele momento.

O pedido deve conseguir exibir:
- quantity;
- unidade vendida: peça(s), lote(s), pacote(s), kit(s), caixa(s);
- packageSize, se pacote/lote;
- packageLabel;
- packageDescription;
- totalPieces;
- salesMode;
- preço do SKU vendido;
- valor unitário equivalente, se pacote/lote.

Regra:
- OrderItem histórico não deve depender do estado atual do SKU para exibir lote/pacote.

==================================================
3. CAMPOS RECOMENDADOS
==================================================

Adicionar snapshot em CheckoutSessionItem e OrderItem, ou pelo menos garantir que o snapshot chegue ao OrderItem.

Campos sugeridos:

- SalesMode
- PackageSize nullable
- PackageLabel nullable
- PackageDescription nullable
- QuantityUnitLabel nullable
- ShowTotalPieces
- TotalPieces nullable
- EquivalentUnitPrice nullable
- SalesDisplayLabel nullable

Se preferir uma estrutura mais enxuta, mínimo aceitável:

- SalesMode
- PackageSize
- PackageLabel
- QuantityUnitLabel
- TotalPieces
- EquivalentUnitPrice

Avaliar nomes conforme padrão do projeto.

Semântica:
- SalesMode = modo do SKU no momento do checkout.
- PackageSize = peças por unidade vendida, somente display.
- QuantityUnitLabel = "peça(s)", "lote(s)", "pacote(s)" etc.
- TotalPieces = quantity * packageSize para pacote/lote; para Unit/Min/Multiple pode ser quantity ou null, conforme decisão.
- EquivalentUnitPrice = preço efetivo do item / packageSize, arredondado com mesmo padrão backend usado em salesRuleDisplay.
- SalesDisplayLabel opcional:
  exemplo: "2 lotes de 3 peças"
  Se criar, tratar como snapshot textual de conveniência.

Importante:
- Não usar esses campos para cálculo de pagamento.
- Não usar esses campos para estoque.
- Subtotal continua quantity × unitPrice do SKU vendido.

==================================================
4. ONDE CAPTURAR O SNAPSHOT
==================================================

Capturar no fluxo de CreateCheckoutSession, no momento em que o backend:
- resolve SKU;
- calcula preço;
- valida salesRule;
- cria CheckoutSessionItem.

Ou capturar ao criar Order a partir da CheckoutSession, desde que a sessão já possua ou consiga carregar o SKU original com segurança.

Recomendação:
- Salvar snapshot já em CheckoutSessionItem.
- Ao criar Order from CheckoutSession, copiar snapshot para OrderItem.
- Assim a sessão e o pedido ficam consistentes.

Não alterar a regra:
- ReserveAsync continua recebendo line.Quantity.
- Não multiplicar por packageSize.

==================================================
5. MIGRATION
==================================================

Criar migration para adicionar colunas em:

checkout.checkout_session_items, se existir tabela específica;
orders.order_items.

Se a arquitetura só tiver OrderItem e não persistir item de sessão, adaptar conforme estrutura real.

Campos devem ser nullable quando necessário para não quebrar dados existentes.

Backfill:
- pedidos existentes podem ficar null.
- Não tentar reconstruir histórico a partir do catálogo atual, salvo se for seguro e simples.
- UI/DTO deve lidar com null.

Defaults:
- para novos itens Unit:
  - SalesMode = Unit
  - QuantityUnitLabel = "peça(s)" ou null
  - PackageSize = null
  - TotalPieces = quantity ou null, conforme decisão documentada.

==================================================
6. DTO SALES DISPLAY
==================================================

Criar DTO de leitura para pedidos, por exemplo:

OrderItemSalesDisplayDto:
{
  "salesMode": "FixedPackage",
  "packageSize": 3,
  "packageLabel": "Lote com 3 peças",
  "packageDescription": null,
  "quantityUnitLabel": "lote(s)",
  "showTotalPieces": true,
  "totalPieces": 6,
  "equivalentUnitPrice": 80.33,
  "summary": "2 lotes = 6 peças"
}

Adicionar em OrderItem DTOs:

salesDisplay: OrderItemSalesDisplayDto? ou objeto equivalente.

Expor em:

- GET /api/admin/orders
- GET /api/admin/orders/{orderId}
- GET /api/customer/orders
- GET /api/customer/orders/{orderId}
- GET /api/orders/guest/{orderId}/status, se ele retorna itens
- qualquer guest order detail/status que mostre itens

Se algum endpoint de listagem não retorna itens, não precisa incluir ali.
Mas detalhes devem incluir.

==================================================
7. REGRAS DE DISPLAY
==================================================

Para Unit / MinimumQuantity / MultipleQuantity:
salesDisplay pode ser null ou conter:
- salesMode;
- quantityUnitLabel = "peça(s)";
- totalPieces = quantity.

Decidir e documentar.

Recomendação:
- Para não poluir DTO, retornar salesDisplay null para não-pacote, salvo se já houver benefício claro.
- Para FixedPackage/AssortedPackage, retornar salesDisplay preenchido.

Para FixedPackage:
- summary:
  "2 lotes = 6 peças"
  ou
  "2 pacotes = 12 peças"

Para AssortedPackage:
- summary:
  "2 lotes sortidos = 12 peças"
  ou usar packageLabel/description.

Regra de plural:
- Pode usar quantityUnitLabel já contendo "(s)" para simplificar.
- Não gastar muito tempo com pluralização perfeita nesta fase.

==================================================
8. CÁLCULO DO EQUIVALENT UNIT PRICE
==================================================

Para pacote/lote:
- equivalentUnitPrice = effectiveUnitPrice / packageSize

Onde effectiveUnitPrice:
- promotionalPrice se existir no item?
- ou UnitPrice já persistido no OrderItem/CheckoutSessionItem.

Preferir usar o UnitPrice persistido do item, porque ele representa o preço efetivo usado no pedido.

Arredondamento:
- 2 casas;
- AwayFromZero, mesmo padrão usado em salesRuleDisplay.

Exemplo:
- UnitPrice = 241.00
- PackageSize = 3
- EquivalentUnitPrice = 80.33

==================================================
9. CREATE ORDER FROM CHECKOUT SESSION
==================================================

Ao criar Order a partir de CheckoutSession:

- copiar snapshot item a item.
- Não reler salesRule atual do catálogo para compor OrderItem se a CheckoutSession já tiver snapshot.
- Garantir idempotência: se tentativa duplicada retornar Order já criada, não alterar snapshot.

==================================================
10. COMPATIBILIDADE
==================================================

Pedidos antigos:
- campos null;
- DTO continua funcionando;
- frontend antigo não quebra.

Produtos/SKUs Unit:
- comportamento visual atual permanece.
- sem regressão de totals/Pix.

CheckoutSession antiga:
- se existir sessão antiga sem snapshot e for convertida em Order após deploy, tratar fallback:
  - gerar snapshot Unit;
  - ou carregar SKU atual e gerar melhor esforço.
- Documentar decisão.

==================================================
11. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes backend:

CheckoutSession snapshot:
1. Unit cria item com snapshot Unit ou null conforme decisão.
2. FixedPackage packageSize=3 quantity=2 salva snapshot com totalPieces=6.
3. AssortedPackage packageSize=6 quantity=2 salva snapshot com totalPieces=12.
4. equivalentUnitPrice de 241/3 = 80.33.
5. Checkout continua reservando quantity=2, não 6.
6. Snapshot não altera subtotal: 2 × 241 = 482.

Order creation:
7. OrderItem copia snapshot da CheckoutSessionItem.
8. OrderItem de lote preserva packageSize/packageLabel/quantityUnitLabel.
9. OrderItem totalPieces correto.
10. Idempotência: criar order duplicada não altera snapshot existente.

DTOs:
11. Admin order detail retorna salesDisplay para lote.
12. Customer order detail retorna salesDisplay para lote.
13. Guest order status/detail retorna salesDisplay se endpoint retorna itens.
14. Unit não quebra DTO.
15. Pedido antigo com campos null retorna sem erro.

Histórico:
16. Alterar SKU depois do pedido não altera salesDisplay do pedido.
17. Lote antigo packageSize=3 continua mostrando totalPieces=6 mesmo se SKU atual virar packageSize=6.

Regressão:
18. Orders tests atuais continuam passando.
19. CartCheckout tests atuais continuam passando.
20. PaymentsPix/Worker não precisam mudar.

==================================================
12. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/orders/order-item-sales-snapshot.md
- docs/catalog/sales-rules-contract.md
- docs/orders/customer-orders.md
- docs/orders/admin-orders.md
- docs/orders/post-pix-guest-flow.md, se guest expor item
- docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md, marcar Fase 4 implementada parcialmente/concluída
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md
- docs/README.md, se necessário

Documentar:
- por que snapshot existe;
- quais campos são snapshot;
- quantity continua unidade do SKU vendido;
- packageSize só display;
- subtotal não multiplica packageSize;
- pedidos antigos podem ter salesDisplay null;
- frontend ainda precisa consumir salesDisplay em pedidos.

==================================================
13. NÃO FAZER
==================================================

Não implementar:
- frontend;
- alteração de PDP/carrinho;
- admin UI;
- pacote composto multi-SKU;
- tier pricing;
- mínimo global;
- B2B;
- shipping;
- Pix;
- worker;
- reembolso;
- cancelamento;
- nota fiscal.

Não mudar a semântica:
- quantity não vira peça física em pacote.
- packageSize não entra em estoque.
- packageSize não entra em subtotal.

==================================================
14. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos criados/alterados.
2. Migration criada.
3. Campos adicionados em CheckoutSessionItem e/ou OrderItem.
4. Como snapshot é capturado.
5. Como snapshot é copiado para OrderItem.
6. DTO salesDisplay criado.
7. Endpoints que passaram a expor salesDisplay.
8. Como pedidos antigos são tratados.
9. Como equivalentUnitPrice é calculado.
10. Garantia de que subtotal e Inventory não multiplicam packageSize.
11. Testes criados/alterados.
12. Resultado dotnet build.
13. Resultado dotnet test.
14. Pendências para frontend consumir salesDisplay em pedidos.

Critérios de aceite:
- pedido de 2 lotes de 3 peças salva totalPieces=6;
- pedido exibe salesDisplay independente do SKU atual;
- equivalentUnitPrice 241/3 = 80.33;
- subtotal continua 2 × 241;
- Inventory continua reservando 2;
- DTOs admin/customer/guest expõem salesDisplay quando aplicável;
- pedidos antigos não quebram;
- build/test passam.