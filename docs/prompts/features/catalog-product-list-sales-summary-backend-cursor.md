Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, CQRS, EF Core, PostgreSQL, Catalog, Product/SKU read models, performance de listagem e contratos de API para e-commerce.

Objetivo:
Adicionar um resumo comercial de sales rules na listagem de produtos do catálogo, para que o frontend consiga exibir badges/preço por lote/valor unitário no ProductCard sem precisar chamar by-slug para cada produto.

Problema atual:
A home/listagem de produtos ainda não retorna salesRule/salesRuleDisplay. Para exibir corretamente:
- Mín. X peças
- Múltiplos de X
- Lote com X peças
- Lote sortido com X peças
- preço por lote
- valor unitário equivalente
- Opções por unidade e lote

o frontend está fazendo hidratação por `by-slug` em cada ProductCard.

Isso funciona no MVP, mas cria risco de N+1:
1 request de listagem + N requests by-slug.

Escopo:
- Backend Catalog.
- Adicionar `salesSummary` compacto no DTO de listagem pública de produtos.
- Não alterar by-slug.
- Não alterar checkout.
- Não alterar inventory.
- Não alterar orders.
- Não alterar frontend neste prompt.
- Não criar migrations, salvo se for absolutamente necessário, o que não deve ser.
- Não implementar filtro novo.
- Não implementar preço por faixa, B2B, mínimo global ou pacote composto.

Base obrigatória:
- docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md
- docs/catalog/sales-rules-contract.md
- docs/catalog/catalog.md ou docs/catalog.md, conforme existir
- docs/ai-context/shopflow-current-state.md

==================================================
1. CONTEXTO
==================================================

Sales rules já foram implementadas no backend:

SalesMode:
- Unit
- MinimumQuantity
- MultipleQuantity
- FixedPackage
- AssortedPackage

Regra de negócio:
- salesRule fica no SKU.
- pacote/lote é SKU próprio.
- quantity sempre representa unidades do SKU vendido.
- packageSize é apenas exibição.
- preço do SKU pacote/lote é preço por lote/pacote.
- valor unitário equivalente = preço efetivo / packageSize.

O frontend da vitrine já usa salesRule/salesRuleDisplay na PDP e carrinho.

Agora precisamos evitar que ProductCard precise chamar by-slug para cada produto da listagem.

==================================================
2. ENDPOINTS ALVO
==================================================

Identificar os endpoints públicos de listagem de produtos usados pela home/vitrine.

Exemplos possíveis:
- GET /api/catalog/products
- GET /api/catalog/products?category=...
- GET /api/catalog/products/search
- qualquer query handler/read model usado pela home.

Adicionar `salesSummary` nos DTOs de listagem pública.

Não precisa incluir em endpoints admin se não forem usados por ProductCard, salvo se a estrutura DTO for compartilhada e fizer sentido.

Não alterar o contrato completo do by-slug, pois ele já retorna salesRule e salesRuleDisplay por SKU.

==================================================
3. DTO PROPOSTO
==================================================

Adicionar DTO compacto:

ProductSalesSummaryDto:
{
  "hasUnit": true,
  "hasMinimumQuantity": false,
  "hasMultipleQuantity": true,
  "hasFixedPackage": true,
  "hasAssortedPackage": false,
  "hasPackage": true,
  "isMixedSalesModes": true,

  "primarySalesMode": "Mixed",
  "primaryBadge": "Opções por unidade e lote",

  "minimumQuantity": 3,
  "quantityStep": 3,

  "packageSize": 3,
  "packageLabel": "Lote com 3 peças",
  "packageDescription": null,
  "quantityUnitLabel": "lote(s)",
  "showTotalPieces": true,

  "packagePrice": 241.00,
  "equivalentUnitPrice": 80.33,

  "fromPrice": 80.33,
  "fromPriceLabel": "A partir de"
}

Ajustar nomes conforme padrão do projeto.

Campos importantes:

Booleanos:
- hasUnit
- hasMinimumQuantity
- hasMultipleQuantity
- hasFixedPackage
- hasAssortedPackage
- hasPackage
- isMixedSalesModes

Resumo principal:
- primarySalesMode
- primaryBadge

Dados de regra:
- minimumQuantity
- quantityStep
- packageSize
- packageLabel
- packageDescription
- quantityUnitLabel
- showTotalPieces

Preço:
- packagePrice
- equivalentUnitPrice
- fromPrice
- fromPriceLabel

Observação:
- Se preferir DTO mais enxuto, manter no mínimo:
  - hasPackage
  - isMixedSalesModes
  - primarySalesMode
  - primaryBadge
  - minimumQuantity
  - quantityStep
  - packageSize
  - packageLabel
  - quantityUnitLabel
  - packagePrice
  - equivalentUnitPrice
  - fromPrice

==================================================
4. REGRAS DE AGREGAÇÃO POR PRODUTO
==================================================

Como a salesRule é por SKU, a listagem precisa resumir as SKUs ativas do produto.

Considerar apenas:
- produto ativo;
- SKUs ativos;
- SKUs vendáveis;
- preços válidos conforme regra atual do catálogo.

Ignorar:
- SKUs inativos;
- SKUs protegidos/inativos;
- dados de estoque admin privado;
- provider/payment/order data.

Definir grupos:
- Unit
- MinimumQuantity
- MultipleQuantity
- FixedPackage
- AssortedPackage

Determinar:
- se produto tem apenas Unit;
- se produto tem apenas MinimumQuantity;
- se produto tem apenas MultipleQuantity;
- se produto tem apenas FixedPackage;
- se produto tem apenas AssortedPackage;
- se produto tem mistura de modos.

==================================================
5. PRIMARY BADGE
==================================================

Gerar `primaryBadge` para uso direto no ProductCard.

Regras sugeridas:

1. Apenas Unit:
- primarySalesMode: "Unit"
- primaryBadge: null

2. Apenas MinimumQuantity:
- primarySalesMode: "MinimumQuantity"
- primaryBadge: "Mín. {minimumQuantity} peças"

3. Apenas MultipleQuantity:
- primarySalesMode: "MultipleQuantity"
- primaryBadge: "Múltiplos de {quantityStep}"

4. Apenas FixedPackage:
- primarySalesMode: "FixedPackage"
- primaryBadge: packageLabel ou "Lote com {packageSize} peças"

5. Apenas AssortedPackage:
- primarySalesMode: "AssortedPackage"
- primaryBadge: packageLabel ou "Lote sortido com {packageSize} peças"

6. Produto com Unit + pacote/lote:
- primarySalesMode: "Mixed"
- primaryBadge: "Opções por unidade e lote"

7. Produto com Unit + Minimum/Multiple:
- primarySalesMode: "Mixed"
- primaryBadge: "Opções de compra flexível"

8. Produto com múltiplos modos sem Unit:
- primarySalesMode: "Mixed"
- primaryBadge: "Opções de compra"

Esses textos podem ser retornados pelo backend ou o backend pode retornar apenas flags e o frontend monta o texto.
Recomendação:
- Retornar `primaryBadge` para reduzir lógica duplicada no frontend.
- Documentar que o frontend pode personalizar copy no futuro.

==================================================
6. PREÇO / FROM PRICE
==================================================

A listagem precisa de preço coerente.

Para SKUs Unit/Minimum/Multiple:
- preço comparável = preço efetivo do SKU.
- fromPrice pode ser o menor preço efetivo entre essas SKUs.

Para FixedPackage/AssortedPackage:
- packagePrice = preço efetivo do SKU pacote/lote.
- equivalentUnitPrice = packagePrice / packageSize.
- fromPrice pode considerar equivalentUnitPrice se o objetivo for "a partir de R$/un".
- mas o card também deve conseguir exibir "R$ X por lote".

Decisão recomendada:
- fromPrice = menor preço unitário equivalente de compra entre SKUs ativos:
  - Unit/Min/Multiple: effectivePrice
  - Package: effectivePrice / packageSize
- packagePrice = preço efetivo do pacote escolhido como principal.
- equivalentUnitPrice = packagePrice / packageSize.

Arredondamento:
- 2 casas;
- AwayFromZero;
- mesmo padrão já usado em salesRuleDisplay.

Exemplo:
SKU FixedPackage:
- regularPrice = 241.00
- packageSize = 3
- packagePrice = 241.00
- equivalentUnitPrice = 80.33
- fromPrice = 80.33

Atenção:
- subtotal/carrinho/checkout continuam usando preço do SKU e quantity.
- Esse cálculo é só resumo visual do card.

==================================================
7. ESCOLHA DO SKU PRINCIPAL PARA SUMMARY
==================================================

Quando houver vários SKUs, escolher um SKU principal para campos de package/badge/preço.

Regras sugeridas:

1. Se houver SKU package/lote com menor equivalentUnitPrice, usar como package principal.
2. Se não houver package, usar SKU com menor preço efetivo.
3. Se o produto for mixed, primaryBadge deve comunicar mistura, mas os campos de preço podem usar o menor fromPrice.
4. Não retornar lista completa de SKUs na listagem; by-slug continua sendo fonte completa.

Documentar a regra escolhida.

==================================================
8. CONTRATO DE LISTAGEM
==================================================

Exemplo de ProductListItemDto:

{
  "id": "...",
  "name": "CORSLET 1146",
  "slug": "corslet-1146",
  "imageUrl": "...",
  "regularPrice": 241.00,
  "promotionalPrice": null,
  "salesSummary": {
    "hasUnit": false,
    "hasMinimumQuantity": false,
    "hasMultipleQuantity": false,
    "hasFixedPackage": true,
    "hasAssortedPackage": false,
    "hasPackage": true,
    "isMixedSalesModes": false,
    "primarySalesMode": "FixedPackage",
    "primaryBadge": "Lote com 3 peças",
    "packageSize": 3,
    "packageLabel": "Lote com 3 peças",
    "quantityUnitLabel": "lote(s)",
    "packagePrice": 241.00,
    "equivalentUnitPrice": 80.33,
    "fromPrice": 80.33,
    "fromPriceLabel": "A partir de"
  }
}

Unit:

{
  "salesSummary": {
    "hasUnit": true,
    "hasPackage": false,
    "isMixedSalesModes": false,
    "primarySalesMode": "Unit",
    "primaryBadge": null,
    "fromPrice": 159.90
  }
}

Mixed:

{
  "salesSummary": {
    "hasUnit": true,
    "hasFixedPackage": true,
    "hasPackage": true,
    "isMixedSalesModes": true,
    "primarySalesMode": "Mixed",
    "primaryBadge": "Opções por unidade e lote",
    "fromPrice": 80.33,
    "packagePrice": 241.00,
    "equivalentUnitPrice": 80.33
  }
}

Se o projeto preferir omitir salesSummary quando não há SKUs:
- documentar.
- Mas preferencialmente retornar salesSummary null apenas se produto não tiver SKU ativo.

==================================================
9. PERFORMANCE
==================================================

Objetivo principal:
- evitar N+1 do frontend.

Implementar cálculo de salesSummary de forma eficiente.

Evitar:
- carregar produto por produto com queries separadas;
- acessar banco em loop;
- depender de by-slug.

Preferir:
- projection em query;
- Include/projection de SKUs ativos em uma query;
- read model já existente com agregação;
- cálculo em memória após carregar produtos + SKUs em batch, se a paginação for pequena e eficiente.

Garantir que:
- listagem paginada continua performática;
- não retorna todos os campos de SKU;
- não retorna atributos completos desnecessários;
- não retorna dados privados de estoque.

==================================================
10. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes de Catalog:

1. Product list Unit retorna salesSummary Unit sem badge.
2. Product list MinimumQuantity retorna badge "Mín. 3 peças".
3. Product list MultipleQuantity retorna badge "Múltiplos de 3".
4. Product list FixedPackage retorna:
   - hasPackage true;
   - packageSize 3;
   - packageLabel;
   - packagePrice 241.00;
   - equivalentUnitPrice 80.33.
5. Product list AssortedPackage retorna badge "Lote sortido com 6 peças".
6. Produto mixed Unit + FixedPackage retorna:
   - isMixedSalesModes true;
   - primaryBadge "Opções por unidade e lote".
7. Produto mixed Minimum + Multiple retorna badge "Opções de compra flexível" ou texto definido.
8. SKUs inativos não entram no salesSummary.
9. Produto sem SKU ativo retorna salesSummary null ou fallback documentado.
10. fromPrice usa menor valor unitário equivalente.
11. packagePrice continua preço do SKU pacote, não preço unitário.
12. Arredondamento 241/3 = 80.33.
13. by-slug continua retornando salesRule/salesRuleDisplay como antes.
14. Não há regressão nos testes existentes de Catalog.
15. Não há regressão no checkout.

Se houver teste de performance/query count, adicionar ou pelo menos validar que não há query por produto.

==================================================
11. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/catalog/product-list-sales-summary.md
- docs/catalog/sales-rules-contract.md
- docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md, se necessário apenas para nota de Fase 3/otimização
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md
- docs/README.md, se necessário

Documentar:
- por que salesSummary existe;
- diferença entre salesSummary e salesRule;
- salesSummary é resumo para card/listagem;
- by-slug continua sendo detalhe completo;
- packagePrice = preço do SKU pacote;
- equivalentUnitPrice = packagePrice / packageSize;
- fromPrice = menor preço unitário equivalente;
- não muda checkout;
- não muda estoque;
- remove necessidade de by-slug por card no frontend.

==================================================
12. NÃO FAZER
==================================================

Não implementar:
- frontend;
- alterações de card;
- checkout;
- inventory;
- orders;
- admin;
- migrations;
- filtros novos;
- B2B;
- tier pricing;
- mínimo global;
- pacote composto;
- shipping;
- payments.

Não alterar:
- semântica de quantity;
- salesRule por SKU;
- by-slug completo.

==================================================
13. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Endpoints afetados.
2. Arquivos alterados.
3. DTO `salesSummary` criado.
4. Regra de agregação implementada.
5. Como o SKU principal/resumo é escolhido.
6. Como fromPrice/packagePrice/equivalentUnitPrice são calculados.
7. Como produto mixed é tratado.
8. Como SKUs inativos são tratados.
9. Testes criados/alterados.
10. Resultado dotnet build.
11. Resultado dotnet test.
12. Pendência para frontend remover hidratação by-slug no ProductCard.

Critérios de aceite:
- listagem retorna salesSummary suficiente para ProductCard;
- Unit não mostra badge;
- lote mostra packagePrice/equivalentUnitPrice;
- mixed mostra "Opções por unidade e lote";
- SKUs inativos não influenciam;
- 241/3 arredonda para 80.33;
- by-slug não quebra;
- checkout não muda;
- testes passam.