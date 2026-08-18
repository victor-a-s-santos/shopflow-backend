Leia os documentos:

- docs/customer/customer-approval-emails.md
- docs/qa/BREVO-TRANSACTIONAL-EMAILS-SMOKE.md
- docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md

Objetivo:
Executar smoke controlado em TESTE para validar Brevo/EMAIL-001 após implementação da Fase 3.

Escopo:
Validar e-mails transacionais de customer approval, auth, pedidos, pagamentos, fulfillment e remessas.

Não implementar feature nova.
Não alterar regra de negócio.
Não alterar frontend.
Não rodar em produção.
Não desabilitar StoreAccess Closed.
Não desabilitar CSRF.
Não logar secrets/tokens.

Validar configuração TESTE:

- Brevo__Enabled=true
- Brevo__SandboxMode=true
- Brevo__ApiKey configurado via secret/env
- Brevo__SenderEmail configurado
- Brevo__ReplyToName configurado
- AdminNotifications__ApprovalRequestsEmail configurado
- PublicApp__AdminBaseUrl configurado
- PublicApp__StorefrontBaseUrl configurado, se existir
- Worker ativo

Executar smoke:

1. Cadastrar customer novo em loja Closed.
2. Confirmar customer Pending.
3. Confirmar outbox:
   - admin approval request
   - customer registration received
4. Processar worker.
5. Confirmar status do outbox.
6. Aprovar customer.
7. Confirmar e-mail de approved.
8. Rejeitar customer teste.
9. Confirmar e-mail de rejected.
10. Suspender customer teste.
11. Confirmar e-mail de suspended.
12. Reativar customer teste.
13. Confirmar e-mail de reactivated, se implementado.
14. Executar forgot/reset password.
15. Confirmar e-mail de reset.
16. Criar pedido com customer Approved.
17. Confirmar e-mail de order created.
18. Confirmar Pix sandbox/reconciliation.
19. Confirmar e-mail de payment confirmed.
20. Marcar pedido como enviado.
21. Confirmar e-mail de shipped.
22. Marcar pedido como entregue.
23. Confirmar e-mail de delivered.
24. Criar remessa com dois pedidos.
25. Marcar remessa enviada.
26. Confirmar um e-mail shipped por pedido.
27. Marcar remessa entregue.
28. Confirmar um e-mail delivered por pedido.
29. Repetir operação crítica e confirmar que idempotencyKey evita duplicidade.
30. Validar logs:
    - sem Brevo ApiKey
    - sem reset token
    - sem guest token
    - sem AccessDecisionReason
    - sem internalOrderNote
    - sem provider IDs sensíveis

Atualizar:

docs/qa/BREVO-TRANSACTIONAL-EMAILS-SMOKE.md

Adicionar seção:

"Execução em TESTE"

Com:
1. Data/hora.
2. Ambiente.
3. Configs mascaradas.
4. Resultado por fluxo.
5. Outbox IDs/tipos, sem payload sensível.
6. Status Sent/Skipped/Failed.
7. Erros encontrados.
8. Riscos.
9. Decisão:
   - PASS
   - PASS WITH RISKS
   - BLOCKED

Critérios de PASS:
- cadastro Pending gera e-mails esperados.
- aprovação/recusa/suspensão/reativação geram e-mails esperados.
- reset password gera e-mail.
- pedido/pagamento/enviado/entregue geram e-mails.
- remessa gera um e-mail por pedido.
- falha de e-mail não quebra fluxo principal.
- idempotência evita duplicidade.
- logs não vazam secrets/tokens/internal notes/reason interno.