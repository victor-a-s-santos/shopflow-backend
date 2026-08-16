# Aprovação de clientes

Fase 1 backend + Fase 3 e-mails transacionais. Frontend (fila, guards, login unificado) = Fase 2.

Enum persistido: `CustomerAccessStatus` (`PendingApproval`, `Approved`, `Rejected`, `Suspended`). JSON público usa `approvalStatus=Pending` como alias de `PendingApproval`.

Não usar `EmailConfirmed`, `IsStaff` ou role admin como aprovação comercial.

## Cadastro

Loja `Closed` (ou `RequireApproval=true` / checkout que exige aprovado): cria `PendingApproval`, `AccessRequestedAt=now`, **não** emite cookie.

```json
{
  "approvalStatus": "Pending",
  "message": "Cadastro enviado para aprovação."
}
```

Loja `Open` com `RequireApproval=false`: cria `Approved` (compatibilidade). Confirmação de e-mail continua técnica e independente.

Cadastro Pending dispara `ICustomerAccessNotifier` → outbox Brevo (admin + cliente). Ver `docs/customer/customer-approval-emails.md`.

## Login / me

`POST /api/auth/customer/login` e `GET /api/auth/customer/me` devolvem `approvalStatus`, `approvalRequestedAt`, `approvedAt` (e aliases `accessStatus` / `accessRequestedAt`).

Pending/Rejected/Suspended **podem autenticar**. Catálogo/checkout são bloqueados por policy.

## Admin

Backoffice + CSRF nas mutações. Ver `docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md`.

Aprovar / recusar / suspender / reativar enfileiram e-mail ao cliente (sem `AccessDecisionReason` no corpo).
