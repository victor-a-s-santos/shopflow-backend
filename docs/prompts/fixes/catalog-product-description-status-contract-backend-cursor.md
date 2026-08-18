Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, CQRS, EF Core, PostgreSQL, Catalog, Product/SKU, contratos de API e consistência entre admin create/edit/detail.

Objetivo:
Corrigir o contrato backend de Product para que `description` e `isActive` sejam campos oficiais, persistidos, retornados e atualizados de forma confiável nos fluxos admin de criação e edição de produto.

Contexto:
O admin operacional já está conectado:
- /admin/products
- /admin/products/new
- /admin/products/:id/edit

Gap identificado:
- A UI de create coleta `description` e `isActive`.
- Porém o wire/contrato de create ainda não persiste esses campos de forma confiável.
- No edit, `description` costuma não hidratar corretamente.
- No update/PUT, `description` e/ou status podem não ir de forma confiável.
- Isso gera risco de o operador cadastrar informações e elas não serem salvas.

Decisão:
`Product.Description` e `Product.IsActive` devem ser parte oficial do domínio/contrato admin.

Não alterar frontend neste prompt.
Não alterar loja pública além de expor description se já fizer sentido no detail/by-slug.
Não alterar checkout.
Não alterar inventory.
Não alterar orders.
Não alterar payments.
Não alterar salesRule.
Não alterar salesSummary, salvo se algum DTO compartilhado exigir cuidado.
Não remover campos da UI por decisão backend.

==================================================
1. AUDITORIA
==================================================

Auditar o módulo Catalog:

- Product entity/domain
- Product EF configuration
- migrations
- CreateProductCommand / Handler
- UpdateProductCommand / Handler
- DTOs admin/public
- ProductReadModel
- GetProductById / by-slug
- Product list admin
- Product list public
- validators
- tests

Responder internamente:
1. Product já tem Description?
2. Product já tem IsActive?
3. Create recebe esses campos?
4. Create persiste esses campos?
5. Detail/read retorna esses campos?
6. Update recebe esses campos?
7. Update preserva ou sobrescreve corretamente?
8. Frontend admin já espera esses campos no contrato?

==================================================
2. DOMÍNIO PRODUCT
==================================================

Garantir que Product tenha:

- Description: string? ou string
- IsActive: bool

Regras sugeridas:

Description:
- opcional;
- trim;
- string vazia deve virar null ou "", conforme padrão do projeto;
- máximo recomendado: 4000 caracteres, ou seguir padrão já existente;
- não pode quebrar produtos existentes.

IsActive:
- booleano;
- default true para novos produtos, salvo se explicitamente false no create;
- usado para controlar visibilidade/vendabilidade no catálogo público;
- produto inativo não aparece na listagem pública;
- admin continua vendo ativo/inativo.

Se Product já tem IsActive:
- não recriar.
- apenas garantir create/update/read consistentes.

Se Product não tem Description:
- adicionar ao domínio e persistence.

Criar métodos de domínio, se padrão pedir:
- ChangeDescription(...)
- Activate()
- Deactivate()
- ChangeStatus(...)
- UpdateBasicInfo(...)

Evitar set público descontrolado se o domínio usa encapsulamento.

==================================================
3. MIGRATION / EF
==================================================

Se Description não existir no banco:

Criar migration EF Core no schema catalog adicionando coluna:

- description text null

Ou, se o projeto usa varchar:
- description varchar(4000) null

Se IsActive já existe, não mexer.
Se IsActive não existir, avaliar cuidadosamente:
- provavelmente já existe, pois filtros active/inactive funcionam.
- não duplicar.

Atualizar EF configuration:
- coluna description;
- max length, se aplicável;
- nullable;
- nome conforme padrão snake_case.

Backfill:
- produtos existentes com description null.

Critério:
- migration não pode quebrar produtos existentes.
- dotnet build e migrations devem compilar.

==================================================
4. CREATE PRODUCT
==================================================

Atualizar contrato de criação admin.

CreateProductCommand / Request deve aceitar:

- description?: string | null
- isActive?: boolean

Regras:
- description trimada.
- description vazia vira null, se esse for o padrão.
- se isActive não vier, default true.
- se isActive=false vier, salvar false.
- não ignorar false por bug de coalescing.

Atenção:
- Em C#, cuidado com bool default.
- Não fazer algo que transforme false em true por engano.
- Usar bool? no request se precisar distinguir ausente de false.
- No Command final, deixar claro o default.

Critério:
- criar produto com isActive=false deve resultar em produto inativo.
- criar produto com description deve persistir description.
- criar produto sem description deve funcionar.

==================================================
5. UPDATE PRODUCT
==================================================

Atualizar contrato de edição admin.

UpdateProductCommand / Request deve aceitar:

- description?: string | null
- isActive?: boolean

Definir semântica:

Se o endpoint PUT é atualização completa:
- enviar description e isActive no payload e salvar exatamente o que veio.
- description null/empty deve limpar descrição, se usuário apagou.
- isActive false deve ser salvo como false.

Se o endpoint aceita objeto parcial:
- ausência de description preserva.
- ausência de isActive preserva.
- presença com null/empty limpa description.
- presença com false salva false.

Importante:
- entender o padrão atual do update.
- não quebrar preservação de salesRule quando salesRule ausente.
- não resetar campos de display/isFeatured/displayOrder quando display ausente.
- não resetar imagens/SKUs indevidamente.

Critério:
- editar apenas preço/SKU não deve apagar description.
- editar description deve salvar.
- limpar description deve limpar.
- alterar ativo/inativo deve salvar.
- isActive=false não pode ser ignorado.

==================================================
6. READ DTOs / DETAIL / BY-SLUG
==================================================

Garantir que os endpoints de leitura usados pelo admin retornem:

- description
- isActive

Endpoints prováveis:
- GET /api/admin/catalog/products/{id}
- GET /api/catalog/products/{slug} ou by-slug usado no PDP
- endpoints internos/read model

Admin detail/edit:
- deve retornar description para hidratar formulário.
- deve retornar isActive para hidratar status.

Public by-slug:
- se description é relevante para PDP, retornar description.
- se já existe campo de description público, manter.
- se não quiser expor no público ainda, documentar decisão.
- Recomendação: produto público deve poder exibir description no PDP, então retornar description no by-slug público se esse DTO já representa detalhe do produto.

Public list:
- não precisa retornar description para card/listagem, salvo se já retorna.
- manter payload enxuto.

Admin list:
- não precisa retornar description na tabela, salvo se já retorna.
- deve continuar retornando isActive.

==================================================
7. VALIDAÇÃO
==================================================

Atualizar validators:

Description:
- opcional;
- max length;
- trim;
- mensagens ProblemDetails consistentes.

IsActive:
- booleano válido.
- não exige validação extra.

Garantir ProblemDetails amigável se description exceder limite.

==================================================
8. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes no Catalog:

1. Create product com description persiste description.
2. Create product sem description funciona.
3. Create product com description vazia normaliza conforme regra.
4. Create product com isActive=true salva ativo.
5. Create product com isActive=false salva inativo.
6. Create product sem isActive usa default true.
7. Admin detail retorna description.
8. Admin detail retorna isActive.
9. Update product altera description.
10. Update product limpa description quando enviado vazio/null conforme regra definida.
11. Update product preserva description quando campo ausente, se update for parcial.
12. Update product altera isActive para false.
13. Update product altera isActive para true.
14. Update product com isActive=false não é ignorado.
15. Update product de outros campos não apaga description.
16. Update product sem display não apaga isFeatured/displayOrder.
17. Update product sem salesRule em SKU não reseta salesRule.
18. Produto inativo não aparece na listagem pública.
19. Produto inativo aparece na listagem admin.
20. Public by-slug retorna description se decisão for expor.
21. Public list continua enxuta e paginada.
22. Testes existentes de Catalog continuam passando.

Se houver testes HTTP:
- cobrir create/update/read via endpoints.
- validar ProblemDetails para description longa.

==================================================
9. DOCUMENTAÇÃO
==================================================

Atualizar:

- docs/catalog/catalog.md
- docs/catalog/admin-products-listing.md
- docs/catalog/product-list-pagination-and-ordering.md, se necessário
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:
- description é campo oficial do Product;
- isActive é persistido no create/update;
- create default isActive=true quando ausente;
- update não ignora false;
- admin detail retorna description/isActive;
- produto inativo não aparece na vitrine pública;
- produto inativo aparece no admin;
- pendência frontend para alinhar formulário, se houver.

==================================================
10. NÃO FAZER
==================================================

Não implementar:
- frontend;
- editor rich text;
- SEO/meta description;
- tradução multilíngue;
- histórico de alteração;
- publish workflow;
- novo status além de isActive;
- ProductCard;
- checkout;
- carrinho;
- orders;
- inventory;
- payments.

Não alterar:
- salesRule por SKU;
- salesSummary;
- quantity/packageSize;
- checkout payload;
- admin products pagination;
- public product listing pagination;
- admin inventory.

Não reintroduzir:
- campo publish antigo;
- regra de Product simples.

==================================================
11. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Se migration foi criada.
3. Como Product domain ficou.
4. Como create trata description/isActive.
5. Como update trata description/isActive.
6. Como detail/by-slug retorna description/isActive.
7. Como produto inativo se comporta na listagem pública/admin.
8. Testes criados/alterados.
9. Resultado dotnet build.
10. Resultado dotnet test.
11. Pendência frontend para ajustar o formulário.

Critérios de aceite:
- create persiste description.
- create persiste isActive=false.
- update hidrata/salva description.
- update salva isActive=false.
- detail admin retorna description/isActive.
- produto inativo some da loja pública.
- produto inativo aparece no admin.
- não quebra salesRule/display/images/SKUs.
- build/testes passam.