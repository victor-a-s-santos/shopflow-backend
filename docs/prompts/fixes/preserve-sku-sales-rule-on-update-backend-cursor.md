Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, Clean Architecture, DDD, Catalog, EF Core, FluentValidation e contratos de API compatíveis.

Objetivo:
Corrigir o update de SKU/variant para NÃO sobrescrever a salesRule existente como Unit quando o payload vier sem salesRule.

Contexto:
A Fase 1 backend de wholesale sales rules foi implementada.

Contrato atual:
- SkuDto.SalesRule é obrigatório no read e sempre normalizado.
- salesRuleDisplay é null para Unit/MinimumQuantity/MultipleQuantity.
- salesRuleDisplay é preenchido para FixedPackage/AssortedPackage com packageSize > 1.
- Add/Update variant aceitam SalesRule como opcional:
  SalesRule: SkuSalesRuleWriteDto? = null.

Problema:
No update de SKU, o handler atual faz:

sku.ChangePrice(Price.From(cmd.RegularPrice, cmd.PromotionalPrice));
sku.ChangeSalesRule(SkuSalesRuleFactory.FromWriteDto(cmd.SalesRule));

Quando cmd.SalesRule vem null, FromWriteDto(null) retorna Unit.
Isso sobrescreve uma regra existente, por exemplo:
- MultipleQuantity → Unit
- FixedPackage → Unit
- AssortedPackage → Unit

Esse comportamento é perigoso porque:
- quebra compatibilidade com clientes antigos que ainda não enviam salesRule;
- pode apagar configuração de lote/múltiplo sem intenção;
- pode quebrar checkout/carrinho/admin depois da Fase 2;
- viola o princípio de update parcial/preservação em campos opcionais.

==================================================
1. REGRA CORRETA
==================================================

No ADD/CREATE de SKU:
- salesRule ausente deve virar Unit default.
- Isso continua correto.

No UPDATE de SKU:
- salesRule ausente deve preservar a regra existente.
- salesRule presente deve substituir/atualizar a regra.
- salesRule presente com salesMode vazio/inválido deve retornar validação, não resetar silenciosamente para Unit.
- Se o frontend quiser explicitamente voltar para Unit, deve enviar:
  {
    "salesMode": "Unit",
    "minimumQuantity": 1,
    "quantityStep": 1
  }
  ou o formato canônico aceito pelo contrato.

==================================================
2. AJUSTE NO HANDLER
==================================================

Alterar UpdateSkuCommandHandler.

Comportamento esperado:

sku.ChangePrice(Price.From(cmd.RegularPrice, cmd.PromotionalPrice));

if (cmd.SalesRule is not null)
{
    sku.ChangeSalesRule(SkuSalesRuleFactory.FromWriteDto(cmd.SalesRule));
}

Ou equivalente, respeitando o estilo do projeto.

Não chamar FromWriteDto(null) em update.

==================================================
3. VALIDAÇÃO
==================================================

Revisar validators de update:

- Se SalesRule == null:
  - não validar campos internos;
  - permitir update legado.
- Se SalesRule != null:
  - validar salesMode;
  - validar min/step/package conforme regras atuais;
  - salesMode vazio/string vazia deve falhar com ProblemDetails por campo;
  - não normalizar payload inválido para Unit silenciosamente.

Garantir que update com salesRule ausente não falha.

==================================================
4. CONTRATOS / DOCUMENTAÇÃO
==================================================

Atualizar:

- docs/catalog/sales-rules-contract.md
- docs/catalog/admin-product-contract.md, se necessário
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md ou technical-debt, se necessário

Documentar explicitamente:

Create/Add SKU:
- salesRule ausente = Unit.

Update SKU:
- salesRule ausente = preserva regra existente.
- para resetar para Unit, envie salesRule explicitamente com salesMode Unit.

==================================================
5. TESTES OBRIGATÓRIOS
==================================================

Adicionar/ajustar testes em Catalog:

1. Update SKU sem salesRule preserva Unit existente.
2. Update SKU sem salesRule preserva MultipleQuantity existente.
3. Update SKU sem salesRule preserva FixedPackage existente.
4. Update SKU sem salesRule preserva AssortedPackage existente.
5. Update SKU com salesRule Unit explícito reseta regra para Unit.
6. Update SKU com salesRule MultipleQuantity explícito atualiza regra.
7. Update SKU com salesRule vazio/inválido falha com ProblemDetails/validation.
8. Add/Create SKU sem salesRule continua criando Unit default.
9. Storefront by-slug após update sem salesRule ainda retorna a salesRule anterior.
10. Checkout após update sem salesRule continua aplicando a regra anterior.

Se houver testes específicos de handler, priorizar UpdateSkuCommandHandler.
Se houver testes de API/integração, cobrir contrato HTTP também.

==================================================
6. NÃO FAZER
==================================================

Não implementar frontend.
Não alterar checkout além do necessário para teste, se houver.
Não alterar Inventory.
Não alterar PaymentsPix.
Não alterar Orders.
Não alterar migration já criada, salvo se absolutamente necessário.
Não remover FromWriteDto(null) do fluxo de create/add, porque create precisa default Unit.

==================================================
7. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Mudança exata no UpdateSkuCommandHandler.
3. Mudança nos validators, se houve.
4. Testes criados/alterados.
5. Resultado dotnet build.
6. Resultado dotnet test.
7. Confirmação explícita:
   - create sem salesRule = Unit;
   - update sem salesRule = preserva;
   - update com salesRule Unit explícito = reseta para Unit;
   - salesMode vazio/inválido = erro, não reset silencioso.

Critérios de aceite:
- Nenhum update legado apaga salesRule existente.
- Regra de lote/múltiplo não volta para Unit sem intenção explícita.
- Testes passam.
- Documentação atualizada.