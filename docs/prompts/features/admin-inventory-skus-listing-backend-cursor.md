Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, CQRS, EF Core, PostgreSQL, Inventory, Catalog, Backoffice/Admin APIs, paginação server-side, filtros, estoque e contratos operacionais.

Objetivo:
Criar um endpoint admin próprio para listar e buscar SKUs com dados operacionais de estoque, sem depender da listagem pública de catálogo nem de getProductById por produto.

Contexto:
O Backoffice já foi auditado para não usar `/api/catalog/products` dentro de `/admin/*`.

Situação atual do frontend Inventory:
- deixou de usar a listagem pública;
- usa índice admin de catálogo como solução intermediária;
- usa getProductById para obter SKUs;
- possui aviso se totalItems > 100;
- pendência documentada: criar endpoint admin de Inventory para SKUs.

Problema:
Inventory Admin precisa operar SKUs/estoque diretamente.
Catálogo Admin lista produtos, mas Inventory deve listar SKUs com:
- produto;
- SKU;
- status;
- preço resumido;
- estoque físico;
- estoque reservado;
- disponível;
- informações de venda;
- paginação;
- filtros;
- busca.

Decisão:
Criar endpoint próprio:

GET /api/admin/inventory/skus

Protegido por Backoffice/Admin policy.

Não alterar frontend neste prompt.
Não alterar loja pública.
Não alterar checkout.
Não alterar orders.
Não alterar payments.
Não alterar salesRule.
Não alterar reserva/confirm/cancel existentes.
Não expor esse endpoint publicamente.

==================================================
1. ENDPOINT
==================================================

Criar endpoint:

GET /api/admin/inventory/skus

Proteção:
- Backoffice policy obrigatória.

Parâmetros:

- page: int >= 1, default 1
- pageSize: int entre 1 e 100, default 20
- q?: string
- productId?: Guid
- categorySlug?: string
- categoryId?: Guid
- status?: all | active | inactive
- stockStatus?: all | in_stock | low_stock | out_of_stock | reserved
- sort?:
  - default
  - product_name_asc
  - product_name_desc
  - sku_code_asc
  - sku_code_desc
  - stock_asc
  - stock_desc
  - available_asc
  - available_desc
  - reserved_desc
  - price_asc
  - price_desc

Se algum sort for caro ou incompatível com o modelo atual, implementar os seguros e documentar os demais como pendência.

Sugestão mínima obrigatória:
- default
- product_name_asc
- product_name_desc
- sku_code_asc
- sku_code_desc
- available_asc
- available_desc
- stock_asc
- stock_desc

==================================================
2. RESPOSTA PAGINADA
==================================================

Usar o mesmo padrão de paginação do projeto.

Resposta esperada:

{
  "items": [
    {
      "skuId": "guid",
      "productId": "guid",
      "productName": "Calça Jeans Masculina Reta",
      "productSlug": "calca-jeans-masculina-reta",
      "productIsActive": true,

      "skuCode": "SKU-123",
      "skuIsActive": true,

      "category": {
        "id": "guid",
        "name": "Calças",
        "slug": "calcas"
      },

      "primaryImageUrl": "...",

      "regularPrice": 241.00,
      "promotionalPrice": null,
      "effectivePrice": 241.00,

      "physicalQuantity": 10,
      "reservedQuantity": 2,
      "availableQuantity": 8,

      "stockStatus": "in_stock",

      "salesMode": "FixedPackage",
      "packageSize": 3,
      "packageLabel": "Lote com 3 peças",
      "quantityUnitLabel": "lote(s)",

      "createdAt": "2026-07-25T..."
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 120,
  "totalPages": 6,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "total": 120
}

Ajustar nomes conforme padrão do projeto.

DTO sugerido:
AdminInventorySkuListItemDto

Campos recomendados:
- skuId
- productId
- productName
- productSlug
- productIsActive
- skuCode
- skuIsActive
- category compacta
- primaryImageUrl
- regularPrice
- promotionalPrice
- effectivePrice
- physicalQuantity
- reservedQuantity
- availableQuantity
- stockStatus
- salesMode
- packageSize
- packageLabel
- quantityUnitLabel
- createdAt

Não retornar:
- atributos completos;
- dados de pedidos;
- dados de pagamento;
- tokens;
- dados privados de cliente;
- histórico completo de movimentações.

Histórico de movimentações deve continuar em endpoint próprio, se existir.

==================================================
3. CÁLCULO DE ESTOQUE
==================================================

Usar a fonte oficial de estoque do módulo Inventory.

Campos:
- physicalQuantity: quantidade física.
- reservedQuantity: quantidade reservada.
- availableQuantity: physicalQuantity - reservedQuantity.

Garantir:
- availableQuantity nunca deve ser exibido como negativo, salvo se o domínio permitir e isso for intencional.
- Se houver divergência/inconsistência, documentar ou retornar valor real conforme regra do domínio.
- Não recalcular reserva manualmente se já houver VO/serviço/read model oficial.

StockStatus sugerido:

out_of_stock:
- availableQuantity <= 0

reserved:
- availableQuantity <= 0 e reservedQuantity > 0
ou, se preferir, usar reserved apenas como indicador adicional.

low_stock:
- availableQuantity > 0 e availableQuantity <= threshold

in_stock:
- availableQuantity > threshold

Threshold:
- usar config se já existir;
- se não existir, usar default 5 e documentar;
- não criar tela/config admin neste prompt.

Se o projeto já tiver status/threshold de estoque, reutilizar.

==================================================
4. FILTROS
==================================================

Implementar filtros server-side antes de Count/paginação.

q:
- buscar por:
  - productName;
  - productSlug;
  - skuCode.
- case-insensitive.

productId:
- filtra SKUs de um produto.

categorySlug/categoryId:
- filtra SKUs de produtos daquela categoria.
- slug inexistente retorna página vazia.

status:
- all: todos os SKUs.
- active: SKU ativo e produto ativo.
- inactive: SKU inativo ou produto inativo.
Se o domínio tratar produto inativo/SKU inativo separadamente, documentar a regra.

stockStatus:
- all: todos.
- in_stock: disponíveis acima do limiar.
- low_stock: disponíveis > 0 e <= limiar.
- out_of_stock: disponíveis <= 0.
- reserved: reservedQuantity > 0.

Critério:
- totalItems reflete filtros.
- hasNextPage reflete filtros.

==================================================
5. SORTS
==================================================

Implementar ordenação determinística.

default:
- productName ASC
- skuCode ASC
- skuId ASC

product_name_asc:
- productName ASC
- skuCode ASC
- skuId ASC

product_name_desc:
- productName DESC
- skuCode ASC
- skuId ASC

sku_code_asc:
- skuCode ASC
- productName ASC
- skuId ASC

sku_code_desc:
- skuCode DESC
- productName ASC
- skuId ASC

stock_asc:
- physicalQuantity ASC
- productName ASC
- skuId ASC

stock_desc:
- physicalQuantity DESC
- productName ASC
- skuId ASC

available_asc:
- availableQuantity ASC
- productName ASC
- skuId ASC

available_desc:
- availableQuantity DESC
- productName ASC
- skuId ASC

reserved_desc:
- reservedQuantity DESC
- productName ASC
- skuId ASC

price_asc:
- effectivePrice ASC nulls last
- productName ASC
- skuId ASC

price_desc:
- effectivePrice DESC nulls last
- productName ASC
- skuId ASC

Sempre usar skuId como desempate final.

==================================================
6. RELAÇÃO COM CATALOG
==================================================

O endpoint pertence ao módulo Inventory/Admin, mas pode precisar projetar dados do Catalog:

- nome do produto;
- slug;
- categoria;
- imagem principal;
- preço;
- sales rule resumida.

Implementar de forma compatível com a arquitetura modular atual.

Não violar fronteiras do projeto:
- se Inventory não puder acessar diretamente entidades de Catalog, usar read model/projection/consulta existente autorizada.
- se já existem read models compartilhados para admin inventory, reutilizar.
- se precisar criar read model específico, documentar.

Atenção:
- não usar endpoint HTTP interno do Catalog.
- não fazer N+1 chamando getProductById.
- query deve ser eficiente.

==================================================
7. PERFORMANCE
==================================================

Evitar N+1.

A query deve:
- aplicar filtros no banco;
- calcular Count com filtros;
- ordenar no banco;
- aplicar Skip/Take;
- projetar apenas campos necessários.

Cuidado especial:
- estoque/reserva pode estar em tabelas do Inventory;
- produto/SKU pode estar em Catalog;
- evitar carregar listas completas de produtos/SKUs em memória.

Se a arquitetura modular exigir duas queries em batch:
- carregar IDs da página;
- buscar dados complementares em lote;
- nunca uma query por SKU/produto.

Adicionar índices se necessário e justificado:
- skuCode;
- productId;
- category;
- stock quantities;
- isActive.

Não criar índices excessivos sem necessidade.

==================================================
8. AUTORIZAÇÃO E SEGURANÇA
==================================================

Endpoint deve exigir Backoffice/Admin policy.

Testar:
- não autenticado: 401;
- customer sem backoffice: 403;
- admin: 200.

Não expor publicamente.

Não retornar PII.

Não retornar dados operacionais sensíveis além dos necessários para inventory admin.

==================================================
9. COMPATIBILIDADE COM OPERAÇÕES EXISTENTES
==================================================

Não alterar endpoints existentes de Inventory:

- GET público safe de SKU, se existir.
- endpoints admin reserve/confirm/cancel.
- ajuste/remoção de estoque.
- histórico de movimentos, se existir.

Esse endpoint é apenas listagem/busca operacional.

==================================================
10. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes no módulo Inventory/Catalog conforme arquitetura:

1. Endpoint exige Backoffice.
2. Não autenticado recebe 401.
3. Customer sem backoffice recebe 403.
4. Admin recebe 200.
5. Retorna paginação default page=1/pageSize=20.
6. pageSize máximo 100.
7. q busca por productName.
8. q busca por productSlug.
9. q busca por skuCode.
10. productId filtra SKUs do produto.
11. categorySlug filtra SKUs da categoria.
12. categorySlug inexistente retorna página vazia.
13. status=active retorna apenas produto+SKU ativos.
14. status=inactive retorna SKU inativo ou produto inativo conforme regra documentada.
15. stockStatus=in_stock funciona.
16. stockStatus=low_stock funciona.
17. stockStatus=out_of_stock funciona.
18. stockStatus=reserved funciona.
19. availableQuantity = physicalQuantity - reservedQuantity.
20. reservedQuantity é calculado corretamente.
21. sort product_name_asc funciona.
22. sort sku_code_asc funciona.
23. sort available_asc funciona.
24. sort available_desc funciona.
25. sort stock_desc funciona.
26. sort price_asc funciona se implementado.
27. totalItems reflete filtros.
28. hasNextPage funciona.
29. Produto sem imagem retorna primaryImageUrl null e não quebra.
30. Produto com lote retorna salesMode/packageSize/packageLabel.
31. Não há regressão nos testes existentes de Inventory.
32. Não há regressão nos testes de Checkout/Reservation.

Se houver dificuldade em montar teste HTTP com cross-module:
- criar testes de handler/query e pelo menos policy tests no endpoint.

==================================================
11. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/inventory/admin-inventory-skus-listing.md
- docs/catalog/admin-products-listing.md, se precisar referenciar diferença
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:
- endpoint `/api/admin/inventory/skus`;
- diferença entre Admin Products e Admin Inventory SKUs;
- filtros;
- sorts;
- paginação;
- cálculo de availableQuantity;
- stockStatus;
- segurança;
- pendência frontend para consumir endpoint;
- que Backoffice não deve usar listagem pública para inventory.

==================================================
12. NÃO FAZER
==================================================

Não implementar:
- frontend;
- tela admin;
- edição inline de estoque;
- bulk actions;
- export CSV;
- importação;
- reorder;
- checkout;
- carrinho;
- orders;
- payments.

Não alterar:
- semântica de reservation;
- endpoints de reserve/confirm/cancel;
- public catalog;
- admin products;
- salesSummary público;
- ProductDetail/by-slug;
- payload de checkout.

Não usar:
- `/api/catalog/products` como fonte;
- HTTP interno para buscar produto por produto;
- query N+1.

==================================================
13. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Endpoint criado.
2. Arquivos alterados.
3. DTO criado.
4. Parâmetros aceitos.
5. Formato da resposta.
6. Como estoque físico/reservado/disponível é calculado.
7. Como stockStatus é calculado.
8. Como filtros funcionam.
9. Como sorts funcionam.
10. Como Catalog e Inventory foram combinados sem N+1.
11. Como autorização foi aplicada.
12. Testes criados/alterados.
13. Resultado dotnet build.
14. Resultado dotnet test.
15. Pendência frontend para Inventory Admin consumir o endpoint.

Critérios de aceite:
- `/api/admin/inventory/skus` existe e é protegido.
- Lista SKUs com produto, preço resumido e estoque.
- Paginação server-side funciona.
- Busca server-side funciona.
- Filtros funcionam.
- Sort funciona.
- availableQuantity correto.
- Não usa endpoint público.
- Não faz N+1.
- Testes passam.