Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, CQRS, EF Core, PostgreSQL, Catalog, Backoffice/Admin APIs, paginação server-side, filtros, ordenação e contratos para telas administrativas.

Objetivo:
Implementar uma listagem admin de produtos paginada, pesquisável e ordenável, separada da listagem pública da loja.

Contexto:
A listagem pública já está paginada:

GET /api/catalog/products?page=1&pageSize=16&sort=default

Ela retorna apenas produtos ativos com pelo menos 1 SKU ativo e é usada pela vitrine/home.

Também já existem campos comerciais no Product:
- IsFeatured
- DisplayOrder
- CreatedAt

A vitrine pública usa:
- isFeatured DESC
- displayOrder ASC
- createdAt DESC
- id ASC

Problema atual:
O frontend admin ainda está limitado a uma listagem pequena, hoje aproximadamente 48 itens, e não possui uma listagem administrativa robusta para catálogo maior.

Importante:
Não usar a listagem pública como fonte da tela admin, porque o admin precisa ver produtos:
- ativos;
- inativos;
- sem SKU;
- sem imagem;
- incompletos;
- destacados;
- sem ordem manual;
- com problemas de cadastro.

Decisão:
Criar ou ajustar endpoint admin próprio para listagem paginada de produtos.

Não alterar frontend neste prompt.
Não alterar checkout.
Não alterar carrinho.
Não alterar orders.
Não alterar inventory.
Não alterar payments.
Não alterar ProductDetail público/by-slug.
Não alterar semântica de salesRule/salesSummary.
Não alterar a listagem pública, salvo compartilhamento interno seguro de helpers.

==================================================
1. ENDPOINT ADMIN ALVO
==================================================

Criar ou ajustar endpoint admin:

GET /api/admin/catalog/products

ou, se o projeto já tiver padrão diferente, seguir o padrão atual dos endpoints admin do Catalog.

O endpoint deve ser protegido por Backoffice/Admin policy já existente.

Parâmetros desejados:

- page: int >= 1, default 1
- pageSize: int entre 1 e 100, default 20
- q: string opcional
- categorySlug: string opcional
- categoryId: Guid opcional
- status: all | active | inactive
- featured: all | featured | not_featured
- sort:
  - default
  - newest
  - oldest
  - name_asc
  - name_desc
  - display_order
  - featured
  - updated_desc, somente se UpdatedAt existir de forma confiável no domínio
  - price_asc, se já houver preço disponível sem custo alto
  - price_desc, se já houver preço disponível sem custo alto

Se algum sort não fizer sentido com a estrutura atual, implementar apenas os seguros e documentar os demais como pendência.

Sugestão mínima obrigatória:
- default
- newest
- oldest
- name_asc
- name_desc
- display_order
- featured

==================================================
2. RESPOSTA PAGINADA
==================================================

Usar o mesmo padrão paginado já usado na listagem pública, para consistência.

Resposta esperada:

{
  "items": [
    {
      "id": "guid",
      "name": "Produto",
      "slug": "produto",
      "isActive": true,
      "isFeatured": false,
      "displayOrder": null,
      "createdAt": "2026-07-25T...",
      "category": {
        "id": "guid",
        "name": "Calças",
        "slug": "calcas"
      },
      "primaryImageUrl": "...",
      "skuCount": 3,
      "activeSkuCount": 2,
      "minPrice": 80.33,
      "hasPromotionalPrice": true
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
AdminProductListItemDto

Campos recomendados:
- id
- name
- slug
- isActive
- isFeatured
- displayOrder
- createdAt
- categoryId/categoryName/categorySlug ou objeto category compacto
- primaryImageUrl
- skuCount
- activeSkuCount
- minPrice
- hasPromotionalPrice

Não retornar dados pesados:
- atributos completos;
- lista completa de SKUs;
- imagens completas;
- salesRule completo por SKU;
- dados de estoque privados;
- pedidos;
- pagamentos.

A tela de edição/detalhe continua usando endpoint próprio de detalhe.

==================================================
3. DIFERENÇA ENTRE LISTAGEM PÚBLICA E ADMIN
==================================================

Documentar e implementar a diferença:

Listagem pública:
- apenas produtos ativos;
- apenas produtos com SKU ativo;
- usada pela loja;
- inclui salesSummary para ProductCard;
- ordenação comercial da vitrine.

Listagem admin:
- todos os produtos, por padrão;
- permite filtrar ativo/inativo;
- mostra produtos incompletos;
- ajuda gestão do catálogo;
- não precisa salesSummary completa;
- pode mostrar indicadores resumidos.

Critério:
- produto inativo aparece no admin quando status=all ou inactive.
- produto sem SKU aparece no admin.
- produto sem imagem aparece no admin.
- produto ativo com SKU ativo continua aparecendo normalmente.

==================================================
4. FILTROS
==================================================

Implementar filtros server-side.

q:
- busca básica por nome e slug.
- opcionalmente código/SKU se for eficiente.
- busca case-insensitive.

categorySlug/categoryId:
- filtro exato por categoria.
- se slug inexistente, retornar página vazia.
- se categoryId inexistente, retornar página vazia.

status:
- all: todos
- active: somente ativos
- inactive: somente inativos

featured:
- all: todos
- featured: isFeatured = true
- not_featured: isFeatured = false

Critério:
- filtros devem ser aplicados antes de Count e paginação.
- totalItems reflete os filtros.

==================================================
5. SORTS
==================================================

Implementar ordenações determinísticas.

Sugestões:

default:
- createdAt DESC
- id ASC

newest:
- createdAt DESC
- id ASC

oldest:
- createdAt ASC
- id ASC

name_asc:
- name ASC
- id ASC

name_desc:
- name DESC
- id ASC

display_order:
- displayOrder ASC nulls last
- isFeatured DESC
- createdAt DESC
- id ASC

featured:
- isFeatured DESC
- displayOrder ASC nulls last
- createdAt DESC
- id ASC

price_asc:
- minPrice ASC nulls last
- id ASC

price_desc:
- minPrice DESC nulls last
- id ASC

Atenção:
- Não usar UpdatedAt se o domínio não possui UpdatedAt confiável.
- Se price sort for custoso, deixar documentado como pendência.
- Sempre usar id como desempate final.

==================================================
6. PAGINAÇÃO
==================================================

Parâmetros:
- page default 1
- pageSize default 20
- pageSize max 100

Se page < 1:
- seguir padrão do projeto: erro de validação ou normalizar para 1.
- Preferência: validar e retornar erro consistente se já for o padrão.

Se pageSize > 100:
- clamp para 100 ou erro de validação, conforme padrão.
- Preferência: clamp se a listagem pública já faz clamp.

Critério:
- não carregar tudo em memória para depois paginar;
- filtros/sort devem ser aplicados antes de Skip/Take.

==================================================
7. PERFORMANCE
==================================================

Evitar N+1.

A query deve:
- aplicar filtros no banco;
- calcular Count com filtros;
- ordenar no banco;
- paginar no banco;
- projetar apenas campos necessários;
- calcular skuCount/activeSkuCount/minPrice de forma eficiente.

Preferir projection com subqueries ou agregação controlada.

Não carregar:
- todas as SKUs completas;
- todas as imagens completas;
- atributos completos;
- dados de pedidos/pagamentos.

Se necessário, criar índices:
- Product.Name/Slug para busca, se já houver padrão;
- Product.CategoryId;
- Product.IsActive;
- Product.IsFeatured/DisplayOrder;
- CreatedAt.

Não criar índices excessivos sem justificativa.

==================================================
8. AUTORIZAÇÃO E SEGURANÇA
==================================================

Endpoint deve exigir Backoffice/Admin policy.

Garantir:
- usuário não autenticado recebe 401;
- cliente autenticado sem Backoffice recebe 403;
- admin recebe 200.

Não expor esse endpoint publicamente.

Não retornar PII, pedidos, pagamentos ou dados sensíveis.

==================================================
9. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes no módulo Catalog/Admin API:

1. Admin list exige autenticação Backoffice.
2. Admin list retorna paginação padrão page=1/pageSize=20.
3. pageSize acima do máximo é limitado ou validado conforme padrão.
4. Admin list retorna produtos ativos e inativos quando status=all.
5. status=active retorna apenas ativos.
6. status=inactive retorna apenas inativos.
7. Produto sem SKU aparece no admin.
8. Produto sem imagem aparece no admin.
9. q busca por nome.
10. q busca por slug.
11. categorySlug filtra server-side.
12. categorySlug inexistente retorna página vazia.
13. featured=featured retorna apenas destacados.
14. featured=not_featured retorna apenas não destacados.
15. sort newest ordena por createdAt desc.
16. sort oldest ordena por createdAt asc.
17. sort name_asc ordena por nome asc.
18. sort name_desc ordena por nome desc.
19. sort display_order respeita displayOrder nulls last.
20. sort featured respeita isFeatured/displayOrder.
21. totalItems reflete os filtros.
22. hasNextPage funciona.
23. skuCount e activeSkuCount são calculados corretamente.
24. minPrice é calculado corretamente.
25. Não há regressão nos testes existentes de Catalog.

Se houver testes HTTP de policy:
- incluir 401/403/200.

==================================================
10. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/catalog/admin-products-listing.md
- docs/catalog/product-list-pagination-and-ordering.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:
- diferença entre listagem pública e admin;
- endpoint admin;
- parâmetros aceitos;
- filtros;
- sorts;
- paginação;
- segurança;
- por que admin não usa listagem pública;
- pendência frontend para consumir endpoint.

==================================================
11. NÃO FAZER
==================================================

Não implementar:
- frontend;
- admin frontend;
- drag and drop;
- edição inline;
- exportação CSV;
- bulk actions;
- checkout;
- carrinho;
- orders;
- inventory;
- payments;
- mudanças na listagem pública.

Não alterar:
- ProductDetail/by-slug;
- salesSummary pública;
- salesRule por SKU;
- quantity;
- packageSize;
- checkout payload.

==================================================
12. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Endpoint criado/alterado.
2. Arquivos alterados.
3. DTO criado.
4. Parâmetros aceitos.
5. Formato da resposta paginada.
6. Filtros implementados.
7. Sorts implementados.
8. Como produtos sem SKU/inativos aparecem.
9. Como skuCount/activeSkuCount/minPrice são calculados.
10. Como autorização foi aplicada.
11. Testes criados/alterados.
12. Resultado dotnet build.
13. Resultado dotnet test.
14. Pendência para frontend admin consumir endpoint.

Critérios de aceite:
- endpoint admin protegido existe;
- paginação funciona;
- produtos inativos/sem SKU aparecem no admin;
- filtros server-side funcionam;
- sort server-side funciona;
- totalItems/hasNextPage corretos;
- endpoint público não muda;
- build/testes passam.