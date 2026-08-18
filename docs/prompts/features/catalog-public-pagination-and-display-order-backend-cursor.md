Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, CQRS, EF Core, PostgreSQL, Catalog, Product/SKU, APIs públicas de e-commerce, paginação, ordenação comercial e performance.

Objetivo:
Implementar paginação real e ordenação comercial determinística na listagem pública de produtos do catálogo, além de preparar o backend para o admin controlar quais produtos têm prioridade na vitrine.

Contexto:
A home/vitrine da loja usa `GET /api/catalog/products`.
Agora o ProductCard consome `salesSummary` diretamente da listagem, sem by-slug por card.

Problema atual:
- A home/listagem ainda não tem uma regra comercial clara de:
  - quais produtos aparecem primeiro;
  - quantos produtos aparecem por vez;
  - como carregar mais produtos;
  - como o admin define prioridade de exibição.
- Não devemos usar `UpdatedAt` como ordenação principal, porque qualquer edição pequena no produto mudaria a ordem da vitrine.
- A loja pode vender unidade, mínimo, múltiplos e lotes conforme cada SKU.
- A ordenação da vitrine não deve depender do tipo de venda, salvo no futuro.

Decisão de produto:
- Backend terá paginação por `page` e `pageSize`.
- Frontend público usará botão “Carregar mais”.
- Admin terá campos para controlar destaque e ordem:
  - `isFeatured`
  - `displayOrder`
- Ordenação padrão:
  1. isFeatured DESC
  2. displayOrder ASC
  3. createdAt DESC
  4. id ASC

Não alterar frontend neste prompt.
Não alterar checkout.
Não alterar inventory.
Não alterar orders.
Não alterar payments.
Não alterar salesSummary, exceto se necessário para compatibilidade com paginação.
Não reintroduzir publish se o domínio atual não usa publish.

==================================================
1. REGRA DE PRODUTOS ELEGÍVEIS PARA LISTAGEM PÚBLICA
==================================================

A listagem pública deve retornar apenas produtos elegíveis para venda/exibição.

Validar a regra atual e manter compatibilidade.

Regra desejada:
- produto ativo;
- com pelo menos 1 SKU ativo/vendável;
- com preço válido conforme regra atual;
- respeitando categoria/filtro/busca já existentes;
- sem expor dados privados/admin.

Se hoje a API já possui filtros equivalentes, preservar.
Se algum item da regra ainda não existir, documentar como pendência, sem quebrar comportamento existente.

Atenção:
- Não usar estoque privado/admin como critério se o catálogo público ainda não trabalha com disponibilidade pública.
- Não alterar contratos de inventory.

==================================================
2. CAMPOS NOVOS NO PRODUTO
==================================================

Adicionar ao domínio Product:

1. IsFeatured
Tipo: bool
Default: false
Uso:
- Define se o produto tem prioridade na vitrine/listagem padrão.

2. DisplayOrder
Tipo: int? ou int
Recomendação:
- nullable int, para diferenciar produtos sem ordem manual.
- Se o projeto preferir não nullable, usar default 1000 ou 9999.

Uso:
- Menor valor aparece antes.
- Exemplo:
  - 10 aparece antes de 20;
  - null fica depois dos que têm ordem manual.

Nome pode seguir padrão do projeto:
- IsFeatured
- DisplayOrder
ou
- StorefrontDisplayOrder
se for mais explícito.

Critério:
- nomes devem ser claros para admin/storefront.
- não misturar com ordem interna/técnica.

Criar métodos de domínio, se o padrão do projeto pedir:
- ChangeDisplaySettings(...)
- SetFeatured(...)
- ChangeDisplayOrder(...)

Validar:
- DisplayOrder não deve ser negativo.
- Se usar nullable, null é permitido.
- Se usar int default, mínimo 0.

==================================================
3. MIGRATION
==================================================

Criar migration EF Core no schema catalog adicionando:

- is_featured boolean not null default false
- display_order integer null ou not null default 1000/9999, conforme decisão

Backfill:
- produtos existentes devem ficar:
  - is_featured = false
  - display_order = null ou default escolhido

Criar índices se fizer sentido:

Sugestão:
- índice composto para listagem pública default:
  - IsActive
  - IsFeatured
  - DisplayOrder
  - CreatedAt
  - Id

Ajustar nomes conforme padrão EF/Postgres do projeto.

Não criar índice excessivo se a query/read model já usa outra estratégia.

==================================================
4. DTOs E CONTRATO ADMIN/CATALOG
==================================================

Adicionar os campos aos DTOs necessários:

Public ProductDto/listagem:
- isFeatured? 
- displayOrder?

Decisão:
- Para o público, esses campos não precisam aparecer se não forem úteis para UI.
- Melhor: não expor `isFeatured` e `displayOrder` publicamente, salvo se o DTO for compartilhado.
- A API pública só precisa vir ordenada corretamente.

Admin Product DTO:
- isFeatured
- displayOrder

Create/Update Product admin:
- aceitar isFeatured/displayOrder, se fizer sentido no fluxo atual.
- Se a criação/edição de produto já é feita por comandos específicos, incluir nesses comandos.

Critério:
- Admin consegue ler e salvar esses campos.
- Público recebe lista ordenada, sem precisar conhecer a regra interna.

==================================================
5. PAGINAÇÃO
==================================================

Implementar/validar paginação em `GET /api/catalog/products`.

Contrato desejado:

GET /api/catalog/products?page=1&pageSize=16&sort=default

Parâmetros:
- page: inteiro >= 1, default 1
- pageSize: inteiro entre 1 e 48, default 16
- sort: default | newest | price_asc | price_desc | name_asc

Limites:
- pageSize máximo: 48
- default pageSize: 16

Resposta:
Manter o padrão de paginação já usado no projeto, se existir.

Exemplo desejado:
{
  "items": [...],
  "page": 1,
  "pageSize": 16,
  "totalItems": 52,
  "totalPages": 4,
  "hasNextPage": true,
  "hasPreviousPage": false
}

Se o projeto já usa outro wrapper paginado:
- reutilizar o padrão existente;
- não criar um segundo padrão.

Critério:
- frontend deve conseguir implementar “Carregar mais” com `hasNextPage`.

==================================================
6. SORTS
==================================================

Implementar os sorts:

1. default:
- isFeatured DESC
- displayOrder ASC, nulls last
- createdAt DESC
- id ASC

2. newest:
- createdAt DESC
- id ASC

3. price_asc:
- menor preço efetivo/fromPrice ASC
- id ASC

4. price_desc:
- menor preço efetivo/fromPrice DESC
- id ASC

5. name_asc:
- name ASC
- id ASC

Observações importantes:
- Para price_asc/price_desc, usar o preço já disponível na listagem ou salesSummary.fromPrice se já for calculado.
- Não recalcular preço de forma inconsistente.
- Se o preço da listagem tem regra existente, preservar.
- Ordenação deve ser determinística, sempre com id como desempate final.

Se price sort for complexo demais neste momento:
- implementar default/newest/name_asc agora;
- documentar price_asc/price_desc como pendência.
- Mas preferencialmente implementar todos se possível.

==================================================
7. ORDEM PADRÃO E DATA DE EDIÇÃO
==================================================

Não usar UpdatedAt como ordenação padrão.

Motivo:
- edição de descrição, imagem, estoque ou preço não deve jogar produto automaticamente para o topo da vitrine.

Usar CreatedAt como fallback de novidade.

Se existir PublishedAt no futuro, poderá substituir CreatedAt.
Mas não criar PublishedAt agora.

==================================================
8. BUSCA E CATEGORIA
==================================================

Manter compatibilidade com filtros existentes.

Se já existem:
- category
- categorySlug
- q/search
- active
- outros filtros

A paginação e sort devem funcionar junto com esses filtros.

Exemplo:
GET /api/catalog/products?categorySlug=calcas&page=2&pageSize=16&sort=default

Critério:
- filtros existentes não podem quebrar.
- totalItems deve refletir os filtros aplicados.

==================================================
9. PERFORMANCE
==================================================

Garantir que a query é eficiente:
- aplicar filtros antes de paginação;
- ordenar antes de Skip/Take;
- evitar carregar by-slug;
- evitar N+1;
- manter salesSummary calculado apenas para os produtos da página ou de forma eficiente.

Como o backend já tem `salesSummary` na listagem:
- garantir que paginação não faz o cálculo para todo o catálogo desnecessariamente, se possível.
- Se a arquitetura atual calcular após carregar a página, ótimo.
- Se precisar calcular antes por causa de sort por preço, avaliar custo e documentar.

==================================================
10. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes de Catalog:

1. GET /api/catalog/products retorna paginação com page/pageSize/totalItems/hasNextPage.
2. page default é 1.
3. pageSize default é 16.
4. pageSize máximo é 48.
5. page menor que 1 retorna erro de validação ou normaliza conforme padrão do projeto.
6. sort default ordena:
   - featured primeiro;
   - displayOrder menor primeiro;
   - createdAt desc;
   - id asc como desempate.
7. UpdatedAt não altera ordem default.
8. sort newest ordena por createdAt desc.
9. sort name_asc ordena por nome.
10. sort price_asc ordena pelo menor preço/fromPrice.
11. sort price_desc ordena pelo maior preço/fromPrice.
12. filtros de categoria continuam funcionando com paginação.
13. busca continua funcionando com paginação.
14. Produto sem SKU ativo não aparece, se essa for a regra atual.
15. salesSummary continua vindo na listagem.
16. by-slug continua inalterado.
17. Admin create/update salva isFeatured/displayOrder.
18. DisplayOrder negativo é inválido.
19. Migration/backfill não quebra produtos existentes.
20. Testes existentes de Catalog continuam passando.

Se houver testes de integração para API:
- adicionar cobertura no endpoint público.

==================================================
11. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/catalog/product-list-pagination-and-ordering.md
- docs/catalog/product-list-sales-summary.md
- docs/catalog/catalog.md ou equivalente
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:

1. Home/vitrine usa listagem pública paginada.
2. Frontend público deve usar “Carregar mais”.
3. API usa page/pageSize.
4. Ordem default:
   - featured desc;
   - displayOrder asc;
   - createdAt desc;
   - id asc.
5. UpdatedAt não define ordem comercial.
6. Admin pode controlar destaque e ordem.
7. salesSummary continua sendo resumo para ProductCard.
8. by-slug continua sendo detalhe completo.
9. Sorts suportados.
10. Limites de pageSize.

==================================================
12. NÃO FAZER
==================================================

Não implementar:
- frontend;
- admin frontend;
- drag and drop;
- seções especiais de home;
- coleção manual;
- banner dinâmico;
- preço por faixa;
- B2B;
- pedido mínimo global;
- frete;
- checkout;
- inventory;
- orders;
- payments.

Não alterar:
- salesRule por SKU;
- quantity;
- packageSize;
- checkout payload;
- ProductDetail/by-slug;
- lógica de Pix/pedidos.

Não usar:
- UpdatedAt como ordenação padrão.

==================================================
13. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Endpoints afetados.
2. Arquivos alterados.
3. Migration criada.
4. Campos adicionados ao Product.
5. DTOs/commands alterados.
6. Contrato de paginação.
7. Sorts implementados.
8. Regra default de ordenação.
9. Como filtros interagem com paginação.
10. Como salesSummary continua funcionando.
11. Testes criados/alterados.
12. Resultado dotnet build.
13. Resultado dotnet test.
14. Pendências para frontend/admin.

Critérios de aceite:
- GET /api/catalog/products é paginado.
- page/pageSize funcionam.
- hasNextPage disponível para “Carregar mais”.
- sort default é determinístico.
- produtos destacados aparecem primeiro.
- displayOrder controla ordem.
- UpdatedAt não muda ordem da vitrine.
- salesSummary continua na listagem.
- by-slug não muda.
- admin backend consegue salvar isFeatured/displayOrder.
- testes passam.