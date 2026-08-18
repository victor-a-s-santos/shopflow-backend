Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, Inventory, Catalog, segurança Backoffice e APIs administrativas.

Objetivo:
Criar um endpoint administrativo batch para consultar disponibilidade de estoque de múltiplos SKUs em uma única chamada, substituindo o N+1 atual do frontend Admin na edição de produto.

Contexto:
Hoje o frontend Admin, na edição de produto, carrega estoque preview assim:

GET /api/inventory/skus/{skuId}

uma vez para cada SKU existente.

Isso está correto do ponto de vista de domínio, porque:

* Catalog não salva estoque;
* Inventory é a fonte da verdade;
* estoque na edição de produto é somente leitura;
* ajuste real continua em Admin → Estoque.

Mas existe uma dívida técnica documentada:
Criar endpoint batch/admin para consultar estoque de múltiplos SKUs.

==================================================

1. ESCOPO
   ==================================================

Criar endpoint:

POST /api/admin/inventory/skus/availability

Payload:

{
"skuIds": [
"uuid-1",
"uuid-2"
]
}

Resposta:

{
"items": [
{
"skuId": "uuid-1",
"availableQuantity": 20,
"quantityOnHand": 25,
"reservedQuantity": 5,
"exists": true
},
{
"skuId": "uuid-2",
"availableQuantity": null,
"quantityOnHand": null,
"reservedQuantity": null,
"exists": false
}
]
}

Regras:

* Endpoint é Backoffice/Admin.
* Não é público.
* Não altera estoque.
* Não reserva.
* Não confirma reserva.
* Não cancela reserva.
* Apenas leitura.
* Deve usar o módulo Inventory como fonte da verdade.
* Não consultar dados de Catalog.
* Não criar inventory item automaticamente.
* SKU sem inventário deve retornar exists=false, sem quebrar a requisição.
* Resposta deve preservar a ordem dos skuIds enviados, se possível.
* Duplicados no payload devem ser tratados de forma previsível.
* Limitar quantidade máxima de skuIds por request, por exemplo 100.
* Validar payload vazio.
* Validar Guid inválido.
* Não expor dados operacionais desnecessários.

==================================================
2. SEGURANÇA
============

Endpoint:

* protegido por policy Backoffice.
* requer cookie admin válido.
* por ser POST, deve respeitar CSRF conforme padrão atual.
* não abrir endpoint público.
* não aceitar anonymous.

Critérios:

* sem cookie admin → 401.
* customer cookie não pode acessar → 403/401.
* admin sem CSRF em POST → falha conforme padrão atual.
* admin com CSRF → 200.

==================================================
3. IMPLEMENTAÇÃO
================

Implementar seguindo padrões existentes do Inventory:

Possíveis nomes:

* GetSkuAvailabilityBatchEndpoint
* GetSkuAvailabilityBatchQuery
* GetSkuAvailabilityBatchQueryHandler
* SkuAvailabilityBatchResponse
* SkuAvailabilityBatchItemDto

Usar EF/Core ou repositório existente de Inventory, conforme padrão real.

Consulta desejada:

* buscar todos os InventoryItems por SkuId em uma query.
* calcular availableQuantity conforme regra atual do Inventory:
  available = quantityOnHand - reservedQuantity
  ou usar propriedade/método existente, se houver.

Não duplicar regra se já existir método de domínio.

==================================================
4. TESTES
=========

Criar testes para:

1. Admin autenticado recebe disponibilidade de múltiplos SKUs.
2. Endpoint retorna availableQuantity correto.
3. SKU sem inventory retorna exists=false.
4. Payload vazio retorna validação.
5. Payload acima do limite retorna validação.
6. SKUs duplicados não quebram.
7. Sem admin cookie retorna 401.
8. Customer não acessa.
9. POST sem CSRF falha conforme padrão.
10. Endpoint não altera estoque.
11. Endpoint não cria inventory item.
12. Endpoint não chama reserva/confirm/cancel.

Executar:

dotnet build
dotnet test

==================================================
5. DOCUMENTAÇÃO
===============

Criar ou atualizar:

* docs/inventory.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/security/SEC-004-endpoint-exposure-review.md, se fizer sentido

Documentar:

* endpoint batch criado;
* uso pelo Admin Product Edit;
* que é somente leitura;
* que é Backoffice;
* que não substitui ajuste real de estoque;
* que Catalog continua sem salvar stockQuantity.

==================================================
6. RESULTADO ESPERADO
=====================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Endpoint criado.
3. DTOs criados.
4. Como a disponibilidade é calculada.
5. Como SKU sem inventário é tratado.
6. Como segurança Backoffice/CSRF foi aplicada.
7. Testes criados.
8. Resultado build/test.
9. Docs atualizadas.
10. Próximo passo recomendado.

Critérios de aceite:

* POST /api/admin/inventory/skus/availability existe.
* Endpoint é protegido por Backoffice.
* Endpoint exige CSRF.
* Endpoint retorna disponibilidade de vários SKUs em uma chamada.
* Endpoint não altera estoque.
* Endpoint não cria inventário.
* SKU sem inventário retorna exists=false.
* dotnet build passa.
* dotnet test passa.
