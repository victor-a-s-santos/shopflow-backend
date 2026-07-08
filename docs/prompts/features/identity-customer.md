# Feature: IdentityCustomer

Índice dos prompts desta feature. **Frontend visual concluído** (maio/2026).

| Prompt | Arquivo | Uso |
|--------|---------|-----|
| Lovable (UI) | [identity-customer-frontend-lovable.md](./identity-customer-frontend-lovable.md) | Referência do que foi pedido/entregue |
| Cursor (revisão) | [identity-customer-cursor-review.md](./identity-customer-cursor-review.md) | Checklist da revisão executada |

**Estado atual:** visual-only — backend IdentityCustomer ainda scaffold.

**Próximo passo do projeto (global):** integrar CartCheckout frontend — ver `docs/ai-context/next-actions.md`.

---

## Prompt histórico (revisão completa)

O conteúdo abaixo era o prompt original de revisão. Mantido como referência; a versão atualizada com resultados está em `identity-customer-cursor-review.md`.

<details>
<summary>Expandir prompt original</summary>

Você está atuando como engenheiro full stack sênior do projeto Shopflow.

Contexto: Lovable implementou preparação visual IdentityCustomer (rotas auth/conta, AuthContext stub, authService stub, dropdown Header, aviso checkout).

Decisão: checkout convidado; login opcional; sem backend IdentityCustomer.

Objetivo: revisar build, types, rotas, Cypress, arquitetura frontend, APIs indevidas.

Não implementar: backend Identity, JWT, Orders real, APIs de clientes.

Validar: rotas auth/conta, AuthRequiredState, GuestCheckoutNotice, checkout convidado, Cypress identity spec, docs.

Ver `identity-customer-cursor-review.md` para checklist completo e resultados.

</details>
