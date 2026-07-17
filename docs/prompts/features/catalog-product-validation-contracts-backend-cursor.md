Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL, validação de contratos HTTP, e-commerce, catálogo, SKUs, variantes, imagens e estoque.

Contexto:
Foi analisado um fluxo real de cadastro/edição de produto no admin. O frontend recebe apenas:

Validation failed
HTTP 400

Isso impede diagnóstico e operação. A análise indica possíveis inconsistências entre frontend e backend em:

* códigos de SKU duplicados;
* preços enviados em formato incorreto;
* atributo personalizado enviado fora do contrato;
* imagens novas/existentes/removidas representadas de forma ambígua;
* salvamento parcial de produto/variantes/imagens;
* tentativa de remover/inativar variantes existentes com estoque/movimentações;
* baixa de estoque baseada no saldo errado.

Objetivo:
Ajustar o backend para ter contratos claros, validações robustas e respostas ProblemDetails úteis para o frontend, sem quebrar o MVP existente.

Não mexer no frontend neste prompt.
Não implementar feature nova de negócio como frete, checkout, pagamento, e-mail ou nota fiscal.
Foco: Catalog/Admin Product, SKUs/variants, attributes, images e validações de Inventory quando relacionadas à baixa de estoque.

==================================================

1. INVESTIGAÇÃO OBRIGATÓRIA
   ==================================================

Antes de alterar código, mapear:

* endpoints de criação de produto admin;
* endpoints de edição de produto admin;
* endpoints de criação/edição de variante/SKU;
* endpoints de imagens do produto;
* DTOs usados no admin;
* validators FluentValidation;
* entidades Product, ProductVariant/Sku, ProductImage, AttributeDefinition, AttributeValueDefinition;
* regras atuais de SKU Code;
* migrations/índices relacionados a SKU Code;
* regras atuais de preço;
* contrato atual para atributos personalizados;
* fluxo de upload/multipart de imagens;
* endpoints de movimentação/baixa de estoque.

Registrar no resultado final:

* quais endpoints foram auditados;
* quais DTOs/validators foram alterados;
* qual é o contrato oficial após o ajuste.

==================================================
2. PROBLEMDETAILS E ERROS DE VALIDAÇÃO
======================================

O backend deve retornar erros úteis e mapeáveis pelo frontend.

Padronizar respostas HTTP 400/409 usando ProblemDetails ou ValidationProblemDetails, com:

* title;
* status;
* detail;
* traceId;
* errors por campo;
* códigos de erro quando possível.

Exemplos esperados:

{
"title": "Validation failed",
"status": 400,
"traceId": "...",
"errors": {
"variants[0].code": [
"O código “CONJUNTO-FLORES” já está sendo usado por outra variação."
],
"variants[1].promotionalPrice": [
"O preço promocional deve ser menor que o preço regular."
],
"variants[2].attributes[0].customName": [
"A cor personalizada deve ter um valor válido."
],
"images[1]": [
"A segunda imagem excede o tamanho permitido."
]
}
}

Requisitos:

* Não retornar somente “Validation failed” sem errors.
* Não esconder exceptions de validação.
* Não vazar stack trace em produção.
* Incluir traceId para suporte.
* Para conflito de unicidade, preferir 409 Conflict quando fizer sentido.
* Para payload inválido, usar 400.
* Para erro inesperado, usar 500 genérico com traceId.

==================================================
3. REGRA OFICIAL DE SKU CODE
============================

Definir e implementar regra única:

* `code` vazio/null/whitespace → backend gera automaticamente um código único.
* `code` informado → backend normaliza e valida unicidade.
* O nome do produto não deve virar automaticamente o mesmo código para todas as variações.
* SKU Code não deve duplicar entre variações do mesmo produto.
* Confirmar se a unicidade atual é global ou por produto.
* Documentar a decisão.

Sugestão de geração:

* base no nome do produto + atributos principais + sufixo se necessário.
* exemplos:

  * CONJUNTO-FLORES-ROSA-M
  * CONJUNTO-FLORES-VARIADAS-M
  * CONJUNTO-FLORES-AZUL-M
  * se colidir: CONJUNTO-FLORES-AZUL-M-2

Critérios:

* gerar código determinístico o suficiente para ser legível;
* garantir unicidade no banco;
* não depender do frontend para unicidade;
* tratar colisão de concorrência;
* retornar erro claro se usuário informar código duplicado.

Verificar:

* se existe índice único em banco;
* se precisa migration;
* se o índice deve ser global ou composto por ProductId + Code;
* se SKU Code pode ser alterado depois de ter estoque/movimentações.

Se SKU com estoque/movimentações não puder alterar Code, validar e retornar erro claro.

==================================================
4. VALIDAÇÃO DE PREÇOS
======================

Padronizar preço como decimal no backend, usando cultura invariável.

Regras:

* regularPrice obrigatório e > 0.
* promotionalPrice opcional.
* se informado:

  * promotionalPrice >= 0;
  * promotionalPrice < regularPrice.
* Não aceitar NaN, infinito, string formatada, moeda, vírgula brasileira ou valor inválido.
* Não aceitar mais de duas casas decimais se essa for a regra monetária do projeto.
* Retornar erro por campo:

  * variants[i].regularPrice
  * variants[i].promotionalPrice

Decidir e documentar:

* promotionalPrice igual ao regularPrice deve ser permitido?
* Recomendação: não permitir, porque não há promoção real.

==================================================
5. CONTRATO DE ATRIBUTOS PERSONALIZADOS
=======================================

Padronizar contrato de atributo de SKU/variante.

Para valor predefinido:
{
"attributeDefinitionId": "...",
"attributeValueDefinitionId": "..."
}

Para valor personalizado:
{
"attributeDefinitionId": "...",
"customName": "Variadas"
}

Regras:

* attributeDefinitionId obrigatório.
* Para valor predefinido:

  * attributeValueDefinitionId obrigatório;
  * customName deve estar null/vazio.
* Para valor personalizado:

  * customName obrigatório, trim, tamanho mínimo/máximo;
  * attributeValueDefinitionId deve estar null.
* Nunca aceitar attributeValueDefinitionId inválido junto com customName.
* Nunca aceitar customName como se fosse ID.
* Validar se attributeDefinitionId existe.
* Validar se attributeValueDefinitionId pertence à attributeDefinitionId.
* Retornar erro por campo:

  * variants[i].attributes[j].attributeValueDefinitionId
  * variants[i].attributes[j].customName

Aplicar em:

* criação de produto;
* edição de produto;
* criação de variante;
* atualização de variante;
* leitura/reconstrução dos DTOs de detalhe admin.

Documentar diferença entre:

* valor global cadastrado;
* valor personalizado do produto;
* valor personalizado exclusivo do SKU, se existir.

==================================================
6. CONTRATO E VALIDAÇÃO DE IMAGENS
==================================

Separar claramente no backend:

* imagens existentes;
* imagens novas;
* imagens removidas;
* imagem principal;
* ordem das imagens.

Validar:

* máximo de 10 imagens por produto;
* formatos aceitos;
* MIME type real, se possível;
* tamanho máximo por arquivo;
* imagem duplicada na mesma requisição, quando detectável;
* ID de imagem existente pertence ao produto;
* não aceitar IDs vazios;
* não aceitar URLs vazias como imagem existente;
* uma única imagem principal;
* ordem não duplicada;
* se remover imagem principal, exigir nova principal ou escolher regra padrão documentada.

Retornar erro individual:

* images[0]
* images[1]
* existingImages[0].id
* removedImageIds[0]
* primaryImageId

Não vazar path interno do servidor.

Se o contrato atual for multipart:

* documentar nomes dos campos esperados;
* validar metadados JSON;
* garantir que arquivo novo não seja tratado como existente.

Se o contrato atual for JSON + upload separado:

* documentar sequência segura.

==================================================
7. SALVAMENTO ATÔMICO / CONSISTENTE
===================================

Verificar o fluxo de salvar produto completo:

* dados básicos;
* categoria;
* imagens;
* atributos;
* variantes/SKUs;
* preços;
* status.

Objetivo:
Evitar persistência parcial quando uma parte falha.

Implementar, quando aplicável:

* transação no backend para create/update de produto completo;
* idempotência ou detecção de duplicidade quando retry ocorrer;
* não recriar variantes já existentes em segunda tentativa;
* não duplicar imagens em retry;
* não deixar produto parcialmente alterado se validação falhar.

Se o projeto hoje usa endpoints separados controlados pelo frontend:

* documentar risco;
* melhorar idempotência onde possível;
* garantir que validações críticas ocorram antes de persistir.

Critérios:

* validação deve ocorrer antes das mudanças principais;
* em erro, banco não deve ficar em estado parcialmente corrompido;
* retornar erro claro.

==================================================
8. VARIANTES EXISTENTES, ESTOQUE E EXCLUSÃO
===========================================

Diferenciar estados:

* SKU novo;
* SKU existente alterado;
* SKU existente inativado;
* SKU marcado para exclusão;
* SKU sem alteração.

Regra segura:

* SKU com estoque, reserva, pedido ou movimentação não deve ser excluído fisicamente.
* Comportamento preferido: inativar.
* Se usuário tentar remover combinação que afeta SKU existente com histórico, backend deve:

  * impedir hard delete;
  * inativar se esse for o contrato;
  * ou retornar erro claro pedindo inativação.

Verificar integração com Inventory:

* existe forma de saber se SKU tem estoque/movimentações/reservas?
* se não houver, documentar limitação e impedir exclusão física por padrão.

Retornar erros claros:

* variants[i].id
* variants[i].isActive
* removedVariantIds[i]

==================================================
9. INVENTORY — BAIXA DE ESTOQUE
===============================

Auditar endpoint de baixa/remoção de estoque.

Regras:

* quantidade obrigatoriamente > 0;
* não permitir baixa maior que o estoque disponível;
* disponível = total físico - reservado;
* baixa operacional comum não deve consumir quantidade reservada por checkout;
* motivo obrigatório ou recomendado conforme regra do projeto;
* impedir duplicidade por duplo envio, se existir idempotency key;
* retornar saldo resultante, se já houver contrato.

Se atualmente a baixa permite usar estoque físico total ignorando reservado, ajustar para usar disponível ou documentar claramente se existe motivo técnico para diferente.

Retornar erro claro:

* quantity;
* skuId;
* reason.

==================================================
10. TESTES OBRIGATÓRIOS
=======================

Criar/ajustar testes para:

ProblemDetails:

1. Erro de validação retorna errors por campo.
2. traceId aparece.
3. Erro inesperado não vaza stack trace em produção.

SKU Code:
4. code vazio gera único.
5. code duplicado no mesmo produto retorna erro claro.
6. code duplicado global/escopo definido retorna erro claro.
7. produto com múltiplas variantes não recebe mesmo code automaticamente.
8. alteração de code com estoque/movimentação segue regra definida.

Preços:
9. regularPrice <= 0 falha.
10. promotionalPrice < 0 falha.
11. promotionalPrice >= regularPrice falha, se essa for a regra.
12. preço com mais de duas casas falha, se aplicável.

Atributos:
13. valor predefinido válido passa.
14. customName válido passa.
15. customName + attributeValueDefinitionId juntos falham.
16. attributeValueDefinitionId inválido falha.
17. attributeValueDefinitionId de outra definição falha.
18. customName vazio falha.

Imagens:
19. mais de 10 imagens falha.
20. MIME/type inválido falha.
21. arquivo acima do limite falha.
22. removedImageId que não pertence ao produto falha.
23. múltiplas principais falham.
24. duplicidade detectável falha ou gera alerta conforme regra.

Transação:
25. falha em variante não persiste metade do produto.
26. retry não duplica variante/imagem.

Inventory:
27. baixa com quantity <= 0 falha.
28. baixa maior que disponível falha.
29. baixa não consome reservado.
30. baixa válida atualiza saldo.

Não chamar serviços externos.
Não depender de frontend.

==================================================
11. DOCUMENTAÇÃO
================

Atualizar/criar:

* docs/catalog/admin-product-contract.md
* docs/catalog/sku-code-rules.md
* docs/catalog/product-images-contract.md
* docs/catalog/product-attributes-contract.md
* docs/inventory/stock-movements.md
* docs/ai-context/shopflow-current-state.md, se existir
* docs/ai-context/backend-next-actions.md ou equivalente
* docs/ai-context/technical-debt.md

Documentar:

* contrato oficial de criação/edição de produto;
* contrato de variantes;
* regra de SKU Code;
* regra de preço;
* contrato de atributo custom;
* contrato de imagem;
* erros ProblemDetails esperados;
* regra de exclusão/inativação de SKU;
* regra de baixa de estoque por disponível.

==================================================
12. NÃO FAZER
=============

Não implementar:

* frete;
* pagamento;
* checkout;
* e-mail;
* nota fiscal;
* dashboard;
* refatoração completa do catálogo;
* mudança visual frontend;
* upload externo S3/R2, salvo se já existir e for apenas correção;
* alteração manual de pedido;
* nova feature de produto fora do escopo.

==================================================
13. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos alterados.
2. Endpoints auditados.
3. Contratos definidos.
4. Migrations criadas, se houver.
5. Validações adicionadas.
6. Como ficam os erros ProblemDetails.
7. Testes criados/alterados.
8. Resultado dotnet build.
9. Resultado dotnet test.
10. Pendências para o frontend.

Critérios de aceite:

* backend retorna erros de validação úteis;
* SKU Code não duplica silenciosamente;
* preço inválido não passa;
* atributo custom tem contrato único;
* imagem inválida retorna erro específico;
* salvamento não deixa produto em estado parcial;
* SKU com estoque/histórico não é excluído fisicamente sem regra segura;
* baixa de estoque respeita disponível;
* build passa;
* testes passam.
