Você está atuando como engenheiro sênior fullstack/QA de contrato no projeto Shopflow.

Objetivo:
Auditar e alinhar o contrato real entre backend e frontend para o formulário admin de produtos, sem criar feature nova.

Contexto:
Após ajustes recentes, backend e frontend parecem desalinhados em alguns pontos:

1. Atributos customizados:
   - Backend reportou contrato: attributeDefinitionId + (attributeValueDefinitionId XOR customName)
   - Frontend reportou que a API real usa: { customName: "Cor", customValue: "Variadas" }
   - Precisamos descobrir qual é o contrato real por endpoint e corrigir documentação/código se necessário.

2. Imagens:
   - Backend reportou endpoints de upload/delete/set-primary image.
   - Frontend reportou pendência: DELETE de imagens de produto; removedImageIds só na UI.
   - Precisamos confirmar se o endpoint DELETE existe, qual rota, payload e se o frontend está consumindo.

3. Create variant:
   - Frontend reportou que create variant não persiste description/isActive no wire atual.
   - Precisamos confirmar se o backend espera esses campos, ignora ou se o frontend não envia.

Não implementar nova feature.
Não mexer em checkout, pagamentos, pedidos, customer ou admin orders.
Foco exclusivo: contrato real de Admin Product / Variants / Attributes / Images.

Tarefas:

1. Auditar backend:
   - DTOs de create/update product.
   - DTOs de create/update variant.
   - DTOs de attributes.
   - endpoints de images: upload, delete, set primary.
   - validators.
   - documentação criada em docs/catalog/*.

2. Auditar frontend, se estiver no workspace:
   - catalogService.ts
   - skuPayload
   - attributeSlotUtils
   - AdminProductForm/Edit
   - ImageUploader
   - docs/admin-product-form.md

3. Produzir uma tabela com o contrato real por endpoint:

- Create product
- Update product
- Add variant
- Update variant
- Upload image
- Delete image
- Set primary image

Para cada endpoint, documentar:
- rota;
- método;
- payload esperado;
- campos obrigatórios;
- formato de atributo predefinido;
- formato de atributo custom;
- campos de variant aceitos;
- response;
- erros ProblemDetails esperados.

4. Resolver a divergência de atributos:
   - Se o backend realmente aceita customName/customValue, atualizar docs backend e frontend para refletir isso.
   - Se o backend deveria aceitar attributeDefinitionId + customName, ajustar o backend ou criar compatibilidade sem quebrar clientes existentes.
   - Não deixar frontend e backend com contratos diferentes.
   - Criar testes para o formato escolhido.

5. Resolver a divergência de imagens:
   - Confirmar se DELETE image existe.
   - Se existe, garantir que o frontend saiba a rota/payload.
   - Se não existe, registrar como pendência bloqueante para remoção real de imagem ou ajustar docs para não prometer.
   - Testar set primary.

6. Resolver create variant description/isActive:
   - Confirmar se backend aceita/persiste.
   - Se frontend envia e backend ignora, corrigir backend ou frontend.
   - Se backend não deve aceitar, remover do wire/frontend ou documentar.
   - Criar teste para não haver perda silenciosa.

7. Testes mínimos obrigatórios:
   - criar produto com atributo predefinido;
   - criar produto com atributo custom “Variadas”;
   - editar produto mantendo atributo custom;
   - criar variant com isActive correto;
   - editar variant alterando isActive;
   - upload imagem;
   - delete imagem existente;
   - set primary;
   - ProblemDetails mapeável por campo.

8. Atualizar documentação:
   - docs/catalog/admin-product-contract.md
   - docs/catalog/product-attributes-contract.md
   - docs/catalog/product-images-contract.md
   - apps/web/docs/admin-product-form.md, se frontend disponível
   - apps/web/docs/ai-context/api-contracts.md, se frontend disponível

Resultado esperado no chat:
1. Contrato oficial final de atributos.
2. Se houve ajuste backend/frontend.
3. Se DELETE imagem existe e está consumido.
4. Se create variant persiste description/isActive.
5. Testes executados.
6. Build/typecheck.
7. Pendências restantes.

Critérios de aceite:
- Não existe divergência entre docs, backend e frontend.
- Atributo custom “Variadas” salva e recarrega corretamente.
- Remoção de imagem existente funciona ou está documentada como pendência real.
- isActive de variant não é perdido.
- Testes passam.