Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, CQRS, EF Core, PostgreSQL, Catalog, Product/SKU, paginação, filtros server-side e APIs públicas de e-commerce.

Objetivo:
Implementar filtro server-side por categoria na listagem pública de produtos, garantindo compatibilidade com paginação, sort default, salesSummary e busca.

Contexto:
O backend já implementou paginação real e ordenação comercial em:

GET /api/catalog/products?page=1&pageSize=16&sort=default

Resposta:
{
  "items": [...],
  "page": 1,
  "pageSize": 16,
  "totalItems": 52,
  "totalPages": 4,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "total": 52
}

A listagem pública já:
- retorna apenas produtos ativos com pelo menos 1 SKU ativo;
- retorna salesSummary apenas para os itens da página;
- suporta sort default, newest, name_asc, price_asc, price_desc;
- usa ordenação default:
  1. isFeatured DESC
  2. displayOrder ASC, nulls last
  3. createdAt DESC
  4. id ASC

Problema atual:
O frontend ainda filtra categorias client-side. Com paginação, isso fica incorreto, porque o frontend só filtra os itens já carregados da página atual.

Exemplo:
- API retorna página 1 com 16 produtos variados.
- Usuário seleciona categoria “Calças”.
- Frontend filtra apenas esses 16 produtos.
- Pode mostrar poucos ou nenhum produto, mesmo existindo produtos da categoria em páginas futuras.

Decisão:
A API pública deve aceitar filtro por categoria antes da paginação.

Contrato desejado:
GET /api/catalog/products?categorySlug=calcas&page=1&pageSize=16&sort=default

Também aceitar categoryId se o projeto já usa ID em algum fluxo:
GET /api/catalog/products?categoryId={guid}&page=1&pageSize=16&sort=default

Se for necessário escolher apenas um para o MVP:
- implementar categorySlug como principal;
- manter categoryId apenas se já existir no contrato atual.

Não alterar frontend neste prompt.
Não alterar checkout.
Não alterar carrinho.
Não alterar orders.
Não alterar inventory.
Não alterar payments.
Não alterar admin visual.
Não alterar salesRule.
Não alterar salesSummary, salvo para garantir que continue funcionando com filtro.

==================================================
1. ENDPOINT ALVO
==================================================

Endpoint público:

GET /api/catalog/products

Adicionar suporte a parâmetros:

- categorySlug?: string
- categoryId?: Guid, se fizer sentido no projeto
- page
- pageSize
- sort
- q/search, se já existir

Exemplo:
GET /api/catalog/products?categorySlug=calcas&page=1&pageSize=16&sort=default

Critério:
- filtro de categoria deve ser aplicado antes de Count/totalItems;
- filtro de categoria deve ser aplicado antes de paginação;
- hasNextPage deve refletir os produtos daquela categoria;
- salesSummary deve continuar sendo calculado apenas para produtos da página filtrada.

==================================================
2. REGRA DE FILTRO POR CATEGORIA
==================================================

Validar como Product se relaciona com Category no domínio atual.

Implementar filtro para produtos pertencentes à categoria selecionada.

Se o projeto tiver categoria hierárquica:
- verificar se a listagem deve incluir apenas categoria exata ou também subcategorias.
- Para MVP, se não houver regra definida, usar categoria exata.
- Documentar subcategorias como pendência futura.

Se Product tiver múltiplas categorias:
- produto aparece se possuir a categoria filtrada.

Se Product tiver uma categoria principal:
- produto aparece se categorySlug/categoryId bater.

Se categorySlug não existir:
- retornar lista vazia paginada ou erro 404/400 conforme padrão atual.
- Recomendação para e-commerce público: retornar lista vazia com totalItems=0 para não quebrar UX.
- Documentar a decisão.

==================================================
3. COMPATIBILIDADE COM BUSCA
==================================================

Se o endpoint já suporta q/search:

GET /api/catalog/products?q=jeans&categorySlug=calcas&page=1&pageSize=16&sort=default

A regra deve ser AND:

produto precisa:
- estar na categoria;
- bater com a busca;
- estar ativo;
- ter SKU ativo;
- respeitar demais filtros atuais.

Critério:
- totalItems deve refletir categoria + busca.
- paginação deve refletir categoria + busca.

==================================================
4. COMPATIBILIDADE COM SORT
==================================================

O filtro por categoria deve funcionar com todos os sorts já existentes:

- default
- newest
- name_asc
- price_asc
- price_desc

Exemplos:
GET /api/catalog/products?categorySlug=calcas&sort=default&page=1&pageSize=16
GET /api/catalog/products?categorySlug=calcas&sort=price_asc&page=1&pageSize=16

Critério:
- ordenação acontece depois do filtro;
- paginação acontece depois da ordenação;
- resultado é determinístico.

==================================================
5. PERFORMANCE
==================================================

Evitar N+1.

A query deve:
- aplicar filtro de categoria no banco;
- aplicar filtro de produto ativo/SKU ativo no banco;
- aplicar count com filtros;
- aplicar sort;
- aplicar Skip/Take;
- calcular salesSummary apenas para os produtos da página ou de forma eficiente.

Se necessário, revisar índices existentes.

Se já houver índice em ProductCategory/CategorySlug, documentar.
Se não houver e fizer sentido:
- criar índice para category slug ou relacionamento, conforme modelagem atual.

Não criar índice excessivo sem necessidade.

==================================================
6. CONTRATO DE RESPOSTA
==================================================

A resposta continua a mesma:

{
  "items": [...],
  "page": 1,
  "pageSize": 16,
  "totalItems": 7,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "total": 7
}

Quando não houver produtos:
{
  "items": [],
  "page": 1,
  "pageSize": 16,
  "totalItems": 0,
  "totalPages": 0 ou 1 conforme padrão já usado,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "total": 0
}

Manter padrão já implementado no backend.

==================================================
7. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes no módulo Catalog:

1. GET /api/catalog/products sem categorySlug continua funcionando.
2. GET /api/catalog/products?categorySlug=calcas retorna apenas produtos da categoria.
3. totalItems reflete apenas produtos da categoria.
4. hasNextPage reflete apenas produtos da categoria.
5. categorySlug inexistente retorna lista vazia paginada ou erro conforme decisão documentada.
6. categorySlug + page/pageSize funciona.
7. categorySlug + sort=default mantém ordenação comercial dentro da categoria.
8. categorySlug + sort=price_asc funciona.
9. categorySlug + search/q funciona com regra AND.
10. Produto ativo em outra categoria não aparece.
11. Produto inativo não aparece.
12. Produto sem SKU ativo não aparece.
13. salesSummary continua vindo para produtos filtrados.
14. salesSummary não é calculado para produtos fora da página.
15. by-slug continua inalterado.
16. Testes existentes de paginação e salesSummary continuam passando.
17. Dotnet test Catalog continua passando.

Se houver testes de integração HTTP:
- adicionar cobertura para endpoint público com categorySlug.

==================================================
8. DOCUMENTAÇÃO
==================================================

Atualizar:

- docs/catalog/product-list-pagination-and-ordering.md
- docs/catalog/product-list-sales-summary.md, se necessário
- docs/catalog/catalog.md ou equivalente
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:
- categorySlug é filtro server-side;
- filtros são aplicados antes da paginação;
- totalItems/hasNextPage refletem categoria filtrada;
- busca + categoria usam regra AND;
- sort funciona dentro da categoria;
- frontend não deve mais filtrar categoria client-side;
- by-slug continua somente detalhe completo.

==================================================
9. NÃO FAZER
==================================================

Não implementar:
- frontend;
- admin visual;
- filtros avançados;
- subcategorias, salvo se já existir regra clara;
- collections;
- banners;
- busca avançada;
- checkout;
- carrinho;
- orders;
- inventory;
- payments.

Não alterar:
- salesRule por SKU;
- salesSummary como contrato de card;
- ProductDetail/by-slug;
- semântica de quantity;
- packageSize;
- payload de checkout.

==================================================
10. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Endpoint afetado.
2. Parâmetros adicionados.
3. Arquivos alterados.
4. Como categorySlug foi aplicado.
5. Como categorySlug interage com paginação.
6. Como categorySlug interage com busca.
7. Como categorySlug interage com sort.
8. O que acontece com categorySlug inexistente.
9. Como salesSummary continua funcionando.
10. Testes criados/alterados.
11. Resultado dotnet build.
12. Resultado dotnet test.
13. Pendências para frontend.

Critérios de aceite:
- GET /api/catalog/products?categorySlug=... filtra server-side.
- filtro acontece antes de count/paginação.
- hasNextPage fica correto por categoria.
- busca + categoria funciona.
- sort + categoria funciona.
- salesSummary continua vindo.
- by-slug não muda.
- testes passam.