# Próximas ações — backend

Documento de ponte. A lista viva continua em `docs/ai-context/next-actions.md`.

## Concluído nesta Fase 1

Store access / customer approval no backend: config Open/Closed + 4 modos internos, cadastro Pending, gates de catálogo/checkout, admin `/approvals`, codes ProblemDetails. Ver `docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md`.

## Pendente (não é backend desta fase)

- **Fase 2 frontend:** guards de catálogo/checkout, tela “cadastro em análise”, fila `/admin/customers/approvals`, badge de pendentes, login visual unificado. **Não** fundir cookies/policies. **Não** remover `/admin/login` no backend.
- **Fase 3 Brevo:** e-mail de “cadastro recebido”, “aprovado”, “recusado”; consumir hook `CustomerRegisteredPendingApproval`.
- Guest tracking legado permanece.
