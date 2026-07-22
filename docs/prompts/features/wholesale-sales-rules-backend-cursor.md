Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, FluentValidation, Catalog, Inventory, CartCheckout, Orders e regras comerciais de e-commerce/atacado.

Objetivo:
Implementar a Fase 1 backend das regras de venda para atacado, pacotes e múltiplos, conforme o design técnico:

docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md

Escopo desta fase:
- Domain Catalog: SalesMode + SkuSalesRule.
- Persistência em catalog.product_skus.
- Contratos admin/storefront para expor e gravar salesRule por SKU.
- Validação de configuração no backend.
- Enforcement no CreateCheckoutSession.
- ProblemDetails/códigos de erro claros.
- Testes backend.
- Documentação de contrato.

Não implementar frontend.
Não alterar Mercado Pago.
Não alterar Worker.
Não alterar PaymentsPix.
Não implementar pacote composto multi-SKU.
Não implementar tier pricing.
Não implementar mínimo global de carrinho.
Não implementar B2B/gating por cliente.
Não implementar snapshot em OrderItem nesta fase, salvo se for estritamente necessário e aprovado pelo design.

==================================================
1. BASE DE DESIGN OBRIGATÓRIA
==================================================

Ler antes de implementar:

docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md

Decisões obrigatórias do design:

1. A regra de venda fica no SKU.
2. Pacote fechado/sortido é SKU próprio.
3. quantity sempre representa unidades do SKU vendido.
4. Para pacote:
   - 1 pacote = quantity 1;
   - packageSize é apenas exibição;
   - Inventory reserva a quantidade de pacotes, não peças internas.
5. Backend é fonte da verdade no checkout.
6. SKUs atuais devem migrar como Unit:
   - salesMode = Unit;
   - minimumQuantity = 1;
   - quantityStep = 1.
7. ClosedGrid/composição multi-SKU fica pós-MVP.

Exemplo obrigatório:

SKU PCT-SORT-6:
- salesMode = AssortedPackage
- packageSize = 6
- quantity = 2 no checkout significa 2 pacotes
- Inventory deve reservar 2, nunca 12

==================================================
2. MODELOS DE VENDA MVP
==================================================

Criar enum no domínio Catalog:

SalesMode:
- Unit = 0
- MinimumQuantity = 1
- MultipleQuantity = 2
- FixedPackage = 3
- AssortedPackage = 4

Não implementar ClosedGrid agora.

Criar Value Object ou estrutura equivalente no domínio:

SkuSalesRule:
- SalesMode
- MinimumQuantity
- QuantityStep
- PackageSize
- PackageLabel
- PackageDescription
- QuantityUnitLabel
- AllowCustomerToChooseVariants
- ShowTotalPieces
- IsWholesaleOnly

Regras padrão:

Unit:
- MinimumQuantity = 1
- QuantityStep = 1
- PackageSize = null
- PackageLabel = null
- PackageDescription = null
- QuantityUnitLabel default: "peça(s)" ou null se o projeto preferir gerar no DTO
- AllowCustomerToChooseVariants = true
- ShowTotalPieces = false
- IsWholesaleOnly = false

MinimumQuantity:
- MinimumQuantity > 1
- QuantityStep = 1
- PackageSize = null

MultipleQuantity:
- QuantityStep > 1
- MinimumQuantity >= 1
- MinimumQuantity % QuantityStep == 0
- PackageSize = null

FixedPackage:
- PackageSize > 1
- PackageLabel obrigatório ou gerado como "Pacote com {PackageSize} peças"
- MinimumQuantity >= 1
- QuantityStep >= 1
- QuantityUnitLabel default: "pacote(s)"
- ShowTotalPieces default true

AssortedPackage:
- mesmas regras de FixedPackage
- AllowCustomerToChooseVariants deve ser false
- PackageDescription recomendada, mas não obrigatória se o design permitir
- QuantityUnitLabel default: "pacote(s)"
- ShowTotalPieces default true

==================================================
3. SEMÂNTICA UNIVERSAL DE QUANTITY
==================================================

Implementar e testar a regra central:

quantity sempre representa unidades do SKU vendido.

Unit / Minimum / Multiple:
- quantity = peças do SKU
- ReserveAsync(skuId, quantity)

FixedPackage / AssortedPackage:
- quantity = pacotes do SKU
- packageSize = peças por pacote apenas para exibição
- ReserveAsync(skuId, quantity)
- nunca multiplicar quantity por packageSize na reserva

Exemplo:
- SKU pacote sortido 6
- packageSize = 6
- quantity = 2
- totalPieces display = 12
- Inventory reservation = 2

==================================================
3.1. TERMINOLOGIA DE LOTE / PACOTE
==================================================

A referência visual do cliente usa o conceito de "lote":

Exemplo:
- Produto: CORSLET 1146
- Unidades no lote: 3
- Escolha a quantidade: 2
- Preço por lote: R$ 241,00
- Valor unitário: R$ 80,33

Interpretação obrigatória:
- 1 lote = 1 unidade vendável do SKU pacote/lote;
- packageSize = 3;
- quantity = 2 significa 2 lotes;
- totalPieces = quantity * packageSize = 6 peças;
- regularPrice/promotionalPrice do SKU representa preço por lote;
- valor unitário exibido = preço efetivo do SKU / packageSize;
- Inventory deve reservar 2, não 6.

A palavra "package" no domínio técnico pode continuar, mas os campos de exibição precisam permitir linguagem de negócio:

- "lote(s)"
- "pacote(s)"
- "kit(s)"
- "caixa(s)"
- "peça(s)"

Usar QuantityUnitLabel para isso.

Exemplos:
- FixedPackage com QuantityUnitLabel = "lote(s)"
- PackageLabel = "Lote com 3 peças"
- PackageDescription = null ou texto livre
- ShowTotalPieces = true

Não assumir que todo pacote/lote é sortido.
- FixedPackage = lote fechado ou pacote com quantidade definida;
- AssortedPackage = lote/pacote sortido, quando o cliente não escolhe cor/tamanho/composição.

==================================================
4. FÓRMULA DE QUANTIDADE VÁLIDA
==================================================

A validação de compra deve seguir:

quantity >= MinimumQuantity
AND
(quantity - MinimumQuantity) % QuantityStep == 0

Além disso, para configuração MultipleQuantity:
MinimumQuantity % QuantityStep == 0

Exemplos:
- min=3, step=3 → válidos 3, 6, 9, 12.
- min=6, step=3 → válidos 6, 9, 12.
- min=4, step=3 → configuração inválida.

Para MinimumQuantity:
- min=3, step=1 → válidos 3, 4, 5, 6.

Para Unit:
- min=1, step=1 → válidos 1, 2, 3.

Para pacote:
- normalmente min=1, step=1 → válidos 1, 2, 3 pacotes.

==================================================
5. MIGRATION / EF
==================================================

Adicionar colunas em catalog.product_skus:

- sales_mode
- minimum_quantity
- quantity_step
- package_size nullable
- package_label nullable
- package_description nullable
- quantity_unit_label nullable
- allow_customer_to_choose_variants
- show_total_pieces
- is_wholesale_only

Defaults para SKUs existentes:
- sales_mode = Unit
- minimum_quantity = 1
- quantity_step = 1
- package_size = null
- package_label = null
- package_description = null
- quantity_unit_label = null ou "peça(s)", conforme decisão
- allow_customer_to_choose_variants = true
- show_total_pieces = false
- is_wholesale_only = false

Criar migration segura:
- backfill explícito;
- defaults no banco se fizer sentido;
- não quebrar produtos existentes;
- não quebrar checkout atual;
- não alterar OrderItem nesta fase.

Avaliar check constraints:
- minimum_quantity >= 1
- quantity_step >= 1
- package_size IS NULL OR package_size > 1

Se constraints por sales_mode ficarem complexas demais, manter no domínio/FluentValidation nesta fase e documentar.

==================================================
6. CONTRATOS ADMIN / CATALOG
==================================================

Estender payloads e DTOs relacionados a SKU/variant.

Admin create/update product e add/update variant devem aceitar:

salesRule:
{
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

Pacote:
{
  "salesMode": "AssortedPackage",
  "minimumQuantity": 1,
  "quantityStep": 1,
  "packageSize": 6,
  "packageLabel": "Pacote sortido com 6 peças",
  "packageDescription": "Cores sortidas conforme disponibilidade.",
  "quantityUnitLabel": "pacote(s)",
  "allowCustomerToChooseVariants": false,
  "showTotalPieces": true,
  "isWholesaleOnly": false
}

Regras:
- salesRule ausente deve defaultar para Unit.
- DTO de leitura deve sempre retornar salesRule normalizada.
- Storefront by-slug deve retornar salesRule por SKU.
- Admin detail/list deve retornar salesRule por SKU.
- Preservar compatibilidade de chamadas antigas sem salesRule.

Não remover campos existentes.
Não quebrar contrato atual de atributos, imagens, preços, active/isActive.

==================================================
6.1. DADOS PARA EXIBIÇÃO DE LOTE NO STOREFRONT
==================================================

O endpoint de storefront by-slug deve retornar dados suficientes para a UI exibir a experiência de lote, sem precisar inferir regra de negócio escondida.

Para SKUs com FixedPackage ou AssortedPackage, a UI precisa conseguir montar:

- "Unidades no lote: 3"
- "Quantidade de lotes: 1, 2, 3..."
- "Preço por lote: R$ 241,00"
- "Valor unitário: R$ 80,33"
- "2 lotes = 6 peças"

Como o preço do SKU já representa a unidade vendável, o frontend pode calcular:
- equivalentUnitPrice = effectiveSkuPrice / packageSize
- totalPieces = quantity * packageSize

Mas, para evitar divergência de arredondamento, avaliar expor campos computados no DTO de leitura, pelo menos no storefront:

salesRuleDisplay ou pricingDisplay:
{
  "sellingUnitLabel": "lote(s)",
  "packageSize": 3,
  "packageSizeLabel": "Unidades no lote",
  "packagePriceLabel": "Preço por lote",
  "equivalentUnitPriceLabel": "Valor unitário",
  "showEquivalentUnitPrice": true,
  "equivalentRegularUnitPrice": 80.33,
  "equivalentPromotionalUnitPrice": 70.00
}

Se preferir não adicionar pricingDisplay nesta fase:
- documentar explicitamente que o frontend deve calcular equivalentUnitPrice usando regularPrice/promotionalPrice + packageSize.
- garantir que by-slug retorna packageSize, quantityUnitLabel, packageLabel e showTotalPieces.

Critério de aceite adicional:
- Um SKU FixedPackage com regularPrice=241.00 e packageSize=3 deve permitir ao frontend exibir:
  - Preço por lote: R$ 241,00
  - Valor unitário: R$ 80,33

==================================================
7. VALIDADORES BACKEND
==================================================

Adicionar validação nos comandos de create/update SKU/variant.

Validações esperadas:

Unit:
- força ou valida min=1, step=1;
- packageSize/packageLabel/packageDescription devem ser null ou ignorados/normalizados.

MinimumQuantity:
- minimumQuantity > 1;
- quantityStep = 1;
- packageSize null.

MultipleQuantity:
- quantityStep > 1;
- minimumQuantity >= 1;
- minimumQuantity % quantityStep == 0;
- packageSize null.

FixedPackage:
- packageSize > 1;
- packageLabel obrigatório ou gerado;
- minimumQuantity >= 1;
- quantityStep >= 1;
- quantityUnitLabel default "pacote(s)";
- showTotalPieces default true.

AssortedPackage:
- packageSize > 1;
- packageLabel obrigatório ou gerado;
- allowCustomerToChooseVariants = false;
- quantityUnitLabel default "pacote(s)";
- showTotalPieces default true.

Validação deve gerar ProblemDetails/ValidationProblemDetails por campo, por exemplo:
- salesRule.minimumQuantity
- salesRule.quantityStep
- salesRule.packageSize
- salesRule.packageLabel
- salesRule.salesMode

==================================================
8. CHECKOUT ENFORCEMENT
==================================================

No CreateCheckoutSession:

Para cada item consolidado por skuId:

1. Resolver SKU ativo e Product ativo.
2. Carregar SkuSalesRule.
3. Validar quantity > 0.
4. Validar:
   - quantity >= minimumQuantity
   - (quantity - minimumQuantity) % quantityStep == 0
5. Para pacote, garantir configuração válida.
6. Precificar com preço atual do SKU.
7. Reservar estoque com a mesma quantity do item.
8. Nunca multiplicar por packageSize.

Adicionar error codes claros:

- SALES_MIN_QUANTITY
- SALES_QUANTITY_STEP
- SALES_RULE_INVALID_CONFIGURATION
- SALES_PACKAGE_INVALID

Mensagens PT-BR:
- "Quantidade mínima deste produto é 3."
- "Este produto é vendido em múltiplos de 3."
- "Este pacote está configurado incorretamente. Entre em contato com o suporte."
- "Pacote inválido para este produto."

HTTP:
- 400 para quantidade/regra inválida.
- 409 permanece para estoque insuficiente, se esse já for o padrão atual.

O erro precisa ser parseável pelo frontend via ProblemDetails.

==================================================
9. INVENTORY NÃO DEVE MUDAR
==================================================

Não alterar regra do Inventory para multiplicar packageSize.

Obrigatório testar:

AssortedPackage:
- packageSize = 6
- checkout quantity = 2
- chamada de reserva deve usar quantity = 2

Não reservar 12.

==================================================
10. ORDERS / SNAPSHOT
==================================================

Não implementar snapshot em OrderItem nesta fase, a menos que o código atual já tenha ponto simples e seguro.

Se não implementar:
- documentar como pendência Fase 4.
- não expor salesDisplay em orders ainda, ou expor derivado apenas se seguro.

Não quebrar Admin Orders, Customer Orders ou Guest status.

Se Order DTOs tiverem SKU data suficiente, não adicionar mudanças desnecessárias agora.

==================================================
11. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes de Catalog:

1. SKU sem salesRule vira Unit default.
2. Unit válido.
3. MinimumQuantity válido.
4. MinimumQuantity com min <= 1 falha.
5. MultipleQuantity válido min=3 step=3.
6. MultipleQuantity inválido min=4 step=3 falha.
7. MultipleQuantity com step <= 1 falha.
8. FixedPackage válido com packageSize=6.
9. FixedPackage sem packageLabel gera default ou falha conforme decisão documentada.
10. AssortedPackage força allowCustomerToChooseVariants=false.
11. Pacote com packageSize <= 1 falha.
12. DTO admin retorna salesRule normalizada.
13. Storefront by-slug retorna salesRule por SKU.
14. Produtos existentes sem salesRule permanecem Unit.

Criar/ajustar testes de CartCheckout:

15. Unit aceita quantity 1.
16. MinimumQuantity min=3 rejeita quantity 2.
17. MinimumQuantity min=3 aceita quantity 3 e 4.
18. MultipleQuantity min=3 step=3 aceita 3, 6, 9.
19. MultipleQuantity min=3 step=3 rejeita 4 e 5.
20. FixedPackage packageSize=6 quantity=2 passa.
21. AssortedPackage packageSize=12 quantity=2 passa.
22. Pacote quantity=2 reserva exatamente 2 unidades do SKU, não 12.
23. Regra inválida de pacote retorna SALES_RULE_INVALID_CONFIGURATION.
24. Erro de quantidade retorna ProblemDetails com code SALES_MIN_QUANTITY ou SALES_QUANTITY_STEP.
25. Estoque insuficiente continua comportamento atual.
26. Checkout de produto antigo Unit não sofre regressão.

Testes adicionais baseados na referência de lote:

27. FixedPackage com packageSize=3, quantityUnitLabel="lote(s)" e regularPrice=241.00 retorna salesRule normalizada.
28. Storefront by-slug retorna packageSize=3, packageLabel/quantityUnitLabel/showTotalPieces para o SKU de lote.
29. Se pricingDisplay/equivalentUnitPrice for implementado, regularPrice=241.00 e packageSize=3 retorna equivalentRegularUnitPrice=80.33.
30. Checkout com SKU lote packageSize=3 e quantity=2 passa.
31. Checkout com SKU lote packageSize=3 e quantity=2 reserva exatamente 2 unidades do SKU, não 6.
32. FixedPackage não deve ser tratado automaticamente como sortido.
33. AssortedPackage deve forçar allowCustomerToChooseVariants=false.
34. FixedPackage pode permitir allowCustomerToChooseVariants=true ou false conforme cadastro.

Se houver testes de integração com Postgres, criar/ajustar conforme padrão do projeto.
Não chamar Mercado Pago real.

==================================================
12. DOCUMENTAÇÃO
==================================================

Criar:

docs/catalog/sales-rules-contract.md

Conteúdo mínimo:
- objetivo;
- sales modes;
- significado de quantity;
- pacote como SKU;
- campos de salesRule;
- exemplos de payload admin;
- exemplos de retorno storefront;
- validações;
- checkout enforcement;
- error codes;
- o que não está no MVP;
- fases futuras.

Atualizar:
- docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md, somente se alguma decisão mudar;
- docs/catalog/admin-product-contract.md;
- docs/catalog/product-attributes-contract.md, se necessário apenas por referência;
- docs/checkout ou docs/cart-checkout, se existir;
- docs/ai-context/shopflow-current-state.md;
- docs/ai-context/backend-next-actions.md;
- docs/ai-context/technical-debt.md;
- docs/README.md, se necessário.

==================================================
13. NÃO FAZER
==================================================

Não implementar:
- frontend admin;
- frontend PDP;
- frontend cart;
- snapshot OrderItem fase 4, salvo se explicitamente necessário;
- ClosedGrid;
- pacote composto baixando vários SKUs;
- tier pricing;
- mínimo por produto/cross-SKU;
- mínimo global de carrinho;
- B2B por cliente;
- visibilidade atacado;
- mudanças em Pix/Mercado Pago;
- mudanças no worker.

==================================================
14. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos criados/alterados.
2. Migration criada.
3. Modelo de domínio criado.
4. Campos adicionados em product_skus.
5. Contratos DTO/payload atualizados.
6. Como salesRule ausente vira Unit.
7. Como CreateCheckoutSession valida min/step/pacote.
8. Garantia de que Inventory reserva quantity sem multiplicar packageSize.
9. Error codes implementados.
10. Testes criados/alterados.
11. Resultado dotnet build.
12. Resultado dotnet test.
13. Pendências para Fase 2 frontend admin.
14. Pendências para Fase 3 storefront/cart.
15. Pendências para Fase 4 OrderItem snapshot.

Critérios de aceite:
- SKUs existentes funcionam como Unit;
- admin/backend aceita persistir salesRule por SKU;
- by-slug retorna salesRule;
- checkout bloqueia quantity inválida;
- pacote quantity=2 reserva 2 pacotes, não 12 peças;
- ProblemDetails retorna codes claros;
- testes passam;
- nenhuma mudança em Pix/worker.