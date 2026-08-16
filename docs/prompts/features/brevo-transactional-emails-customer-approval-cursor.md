Você está atuando como backend engineer sênior do projeto Shopflow, especialista em .NET, Clean Architecture, DDD, EF Core, PostgreSQL, background workers, transactional emails, Brevo, outbox, templates, autenticação customer/admin, orders, Pix e fulfillment.

Objetivo:
Implementar a Fase 3 do Store Access / Customer Approval: e-mails transacionais com Brevo, incluindo aprovação de cadastro e movimentações de pedido.

Contexto:
As Fases 1 e 2 já foram concluídas.

Backend:
- StoreAccess Open/Closed implementado.
- Checkout AllowGuest implementado.
- CustomerAccessStatus implementado:
  - PendingApproval
  - Approved
  - Rejected
  - Suspended
- Cadastro em Closed cria customer Pending.
- Checkout/order bloqueiam guest e customer não aprovado.
- Admin endpoints de approvals existem:
  - GET /api/admin/customers/approvals
  - GET /api/admin/customers/approvals/count
  - POST approve/reject/suspend/reactivate
- Catálogo Closed protegido no backend.

Frontend:
- CustomerApprovedRoute implementado.
- Register Pending vai para /account/pending-approval.
- Admin approvals page implementada.
- Badge/card de pendências implementados.
- Guest checkout escondido.
- /pedido/:orderNumber legado preservado.

ADR:
docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md

Decisão:
Usar Brevo para e-mails transacionais.

Não implementar marketing.
Não implementar newsletter.
Não implementar SMS.
Não implementar WhatsApp Business API.
Não implementar chat.
Não implementar notification center completo.
Não alterar frontend neste prompt, exceto docs de contrato se necessário.
Não alterar StoreAccess/CustomerApproval já concluído, salvo integração de eventos/e-mails.

==================================================
1. ESCOPO
==================================================

Implementar e-mails transacionais para:

A) Customer Approval
1. Novo cadastro pendente → e-mail para admin.
2. Cadastro recebido → e-mail para cliente.
3. Cliente aprovado → e-mail para cliente.
4. Cliente recusado → e-mail para cliente.
5. Cliente suspenso → opcional nesta fase, mas recomendado.

B) Auth
6. Esqueci minha senha / reset password.
7. Confirmação de e-mail, se o fluxo já existir ou estiver parcialmente previsto.

C) Orders / Payments / Fulfillment
8. Novo pedido criado.
9. Pagamento confirmado.
10. Pedido enviado.
11. Pedido entregue.
12. Remessa enviada.
13. Remessa entregue.

Importante:
- falha de e-mail não pode quebrar cadastro, checkout, Pix, pedido, fulfillment ou remessa.
- usar outbox/idempotência para eventos operacionais.
- não vazar dados internos.
- não logar tokens/secrets.

==================================================
2. PROVIDER BREVO
==================================================

Implementar provider Brevo via API HTTP.

Configuração sugerida:

Brevo:
  Enabled: true
  BaseUrl: "https://api.brevo.com"
  ApiKey: ""
  SenderName: "VIP Assessoria Digital"
  SenderEmail: "no-reply@seudominio.com.br"
  ReplyToName: "Atendimento"
  ReplyToEmail: "atendimento@seudominio.com.br"
  SandboxMode: true
  TimeoutSeconds: 10

AdminNotifications:
  ApprovalRequestsEmail: "admin@seudominio.com.br"

Templates:
  UseBrevoTemplateIds: false
  CustomerApprovalRequestAdminTemplateId:
  CustomerRegistrationReceivedTemplateId:
  CustomerApprovedTemplateId:
  CustomerRejectedTemplateId:
  CustomerSuspendedTemplateId:
  PasswordResetTemplateId:
  EmailConfirmationTemplateId:
  OrderCreatedTemplateId:
  PaymentConfirmedTemplateId:
  OrderShippedTemplateId:
  OrderDeliveredTemplateId:
  DeliveryBatchShippedTemplateId:
  DeliveryBatchDeliveredTemplateId:

Regras:
- secrets via env vars.
- ApiKey nunca em appsettings commitado.
- SandboxMode em TESTE pode usar header X-Sib-Sandbox=drop.
- Produção deve usar SandboxMode=false.
- SenderEmail deve ser domínio/sender validado na Brevo.
- ReplyTo configurável.
- Timeout controlado.
- Logs sem ApiKey.

Endpoint:
- POST {BaseUrl}/v3/smtp/email

Headers:
- api-key
- accept: application/json
- content-type: application/json
- se SandboxMode=true, adicionar X-Sib-Sandbox=drop dentro de headers conforme padrão Brevo.

Payload mínimo:
- sender
- to
- subject
- htmlContent ou textContent
- replyTo opcional
- params/templateId opcional se usar templates Brevo

==================================================
3. ABSTRAÇÕES
==================================================

Criar:

ITransactionalEmailSender
BrevoTransactionalEmailSender

DTOs:
- SendTransactionalEmailRequest
- TransactionalEmailRecipient
- TransactionalEmailSender
- SendTransactionalEmailResult

Criar serviço de alto nível:

IEmailNotificationService
ou
INotificationEmailService

Responsabilidade:
- montar e-mails de domínio/aplicação.
- chamar outbox.
- não acoplar handlers ao provider Brevo.

Domínio não deve depender diretamente de Brevo.

==================================================
4. OUTBOX DE E-MAIL
==================================================

Implementar outbox para e-mails transacionais.

Criar tabela em schema apropriado, por exemplo:

notifications.email_outbox

Campos sugeridos:
- Id
- Type
- RecipientEmail
- RecipientName
- Subject
- PayloadJson
- Status
- Attempts
- MaxAttempts
- LastError
- ProviderMessageId
- IdempotencyKey
- CreatedAt
- NextAttemptAt
- SentAt
- FailedAt

Status:
- Pending
- Processing
- Sent
- Failed
- Skipped

Regras:
- IdempotencyKey único.
- Não enviar duplicado.
- Falha temporária agenda retry.
- Max attempts marca Failed.
- Outbox processada por worker.
- Falha Brevo não quebra fluxo principal.

IdempotencyKey sugeridas:

Customer approval:
- customer:{customerId}:approval-request-admin
- customer:{customerId}:registration-received
- customer:{customerId}:approved
- customer:{customerId}:rejected
- customer:{customerId}:suspended

Auth:
- customer:{customerId}:password-reset:{tokenHash}
- customer:{customerId}:email-confirmation:{tokenHash}

Order:
- order:{orderId}:created
- order:{orderId}:paid
- order:{orderId}:shipped
- order:{orderId}:delivered

Delivery batch:
- order:{orderId}:batch-shipped:{batchId}
- order:{orderId}:batch-delivered:{batchId}

Não armazenar token em texto puro na idempotency key.
Se precisar diferenciar token, usar hash curto seguro.

==================================================
5. WORKER / PROCESSAMENTO
==================================================

Criar ou integrar com worker existente.

Processo:
1. Buscar N e-mails Pending com NextAttemptAt <= now.
2. Marcar como Processing de forma segura.
3. Enviar via ITransactionalEmailSender.
4. Se sucesso:
   - Status=Sent
   - SentAt=now
   - ProviderMessageId salvo
5. Se falha recuperável:
   - Attempts += 1
   - NextAttemptAt com backoff
   - Status=Pending
   - LastError sanitizado
6. Se max attempts:
   - Status=Failed
   - FailedAt=now

Backoff simples:
- 1 min
- 5 min
- 15 min
- 1h
ou fórmula documentada.

Evitar múltiplos workers enviando o mesmo item simultaneamente.
Usar lock/transaction conforme padrão EF/Postgres do projeto.

==================================================
6. CUSTOMER APPROVAL EMAILS
==================================================

### 6.1 Novo cadastro pendente — para admin

Evento:
- CustomerRegisteredPendingApproval

Destinatário:
- AdminNotifications:ApprovalRequestsEmail

Assunto:
Novo cadastro aguardando aprovação

Conteúdo:
- Nome
- E-mail
- Telefone, se houver
- Data da solicitação
- Link para admin approvals:
  {AdminBaseUrl}/admin/customers/approvals

Não incluir:
- senha
- hash
- token
- dados sensíveis desnecessários

### 6.2 Cadastro recebido — para cliente

Evento:
- CustomerRegisteredPendingApproval

Assunto:
Recebemos sua solicitação de cadastro

Conteúdo:
- confirmar recebimento
- informar que cadastro está em análise
- CTA para login/status ou WhatsApp, se BaseUrl configurado
- não prometer prazo fixo

### 6.3 Cliente aprovado — para cliente

Evento:
- CustomerApproved

Assunto:
Seu acesso foi aprovado

Conteúdo:
- acesso liberado
- CTA para login:
  {StorefrontBaseUrl}/login

### 6.4 Cliente recusado — para cliente

Evento:
- CustomerRejected

Assunto:
Atualização sobre seu cadastro

Conteúdo:
- cadastro não aprovado neste momento
- orientar contato com equipe
- não incluir reason interna se houver risco operacional
- se incluir motivo, só com decisão explícita e conteúdo seguro

Recomendação:
- não enviar AccessDecisionReason por padrão.
- usar texto genérico seguro.

### 6.5 Cliente suspenso — para cliente

Evento:
- CustomerSuspended

Assunto:
Atualização sobre seu acesso

Conteúdo:
- acesso temporariamente bloqueado
- orientar contato com equipe
- não incluir reason interna por padrão.

==================================================
7. AUTH EMAILS
==================================================

### 7.1 Reset password

Integrar fluxo existente de esqueci senha.

Assunto:
Redefinição de senha

Conteúdo:
- link seguro de reset.
- expiração se conhecida.
- aviso para ignorar se não solicitou.

Segurança:
- não logar token.
- não salvar token puro no outbox se possível.
- se PayloadJson precisar conter link completo, evitar logging do PayloadJson.
- LastError não deve conter link/token.

### 7.2 Confirmação de e-mail

Se o fluxo já existir:
- enviar link de confirmação.

Se não existir ou estiver fora do escopo atual:
- documentar como pendência.

Não confundir:
- EmailConfirmed = confirmação técnica.
- ApprovalStatus = aprovação comercial.

==================================================
8. ORDER EMAILS
==================================================

### 8.1 Novo pedido criado

Evento:
- OrderCreatedFromCheckoutSession

Assunto:
Recebemos seu pedido #{orderNumber}

Conteúdo:
- Pedido #{orderNumber}
- total
- status: aguardando pagamento, se Pix pendente
- resumo dos itens
- link seguro de acompanhamento

Como agora loja Closed cria pedidos com CustomerUserId:
- preferir link para /account/orders/{orderId} ou rota definida.
- se por compatibilidade houver guest pedido legado, usar mecanismo de guest access existente.

Não incluir:
- GUID interno como identificador principal.
- internal notes.
- provider IDs.
- Pix technical fields indevidos.

### 8.2 Pagamento confirmado

Evento:
- OrderPaid / PaymentConfirmed

Assunto:
Pagamento confirmado — Pedido #{orderNumber}

Conteúdo:
- confirmação do pagamento
- resumo do pedido
- preferência de entrega, se houver
- link para acompanhar

### 8.3 Pedido enviado

Evento:
- OrderShipped

Assunto:
Seu pedido #{orderNumber} foi enviado

Conteúdo:
- método de entrega final, se houver
- trackingCode, se houver
- link para acompanhar

### 8.4 Pedido entregue

Evento:
- OrderDelivered

Assunto:
Seu pedido #{orderNumber} foi entregue

Conteúdo:
- confirmação de entrega
- canal de contato em caso de dúvida

==================================================
9. DELIVERY BATCH EMAILS
==================================================

Quando remessa for enviada/entregue:

MVP recomendado:
- enviar um e-mail por pedido envolvido.
- usar orderNumber.
- não expor dados internos da remessa.
- não expor deliveryBatchId se decisão atual for não expor ao customer.

Eventos:
- DeliveryBatchShipped
- DeliveryBatchDelivered

Idempotência:
- order:{orderId}:batch-shipped:{batchId}
- order:{orderId}:batch-delivered:{batchId}

Assuntos:
- Seu pedido #{orderNumber} foi enviado
- Seu pedido #{orderNumber} foi entregue

==================================================
10. TEMPLATES
==================================================

MVP:
- templates HTML no código, simples e profissionais.
- suporte futuro opcional a Brevo templateId.

Criar renderers:
- CustomerApprovalEmailTemplates
- AuthEmailTemplates
- OrderEmailTemplates

Layout:
- nome da loja;
- saudação;
- conteúdo objetivo;
- CTA;
- rodapé.

Texto em PT-BR.

Não incluir:
- nota interna;
- motivo interno, salvo decisão segura;
- GUID interno;
- hashes;
- access tokens;
- dados técnicos do gateway;
- QR Pix/copia e cola em e-mails que não precisam disso.

==================================================
11. LINKS / BASE URLS
==================================================

Adicionar configuração:

AppUrls:
  StorefrontBaseUrl: "https://teste.seudominio.com.br"
  AdminBaseUrl: "https://teste.seudominio.com.br"

Ou equivalente já existente.

Links:
- Login customer: {StorefrontBaseUrl}/login
- Pending approval: {StorefrontBaseUrl}/account/pending-approval
- Admin approvals: {AdminBaseUrl}/admin/customers/approvals
- Customer order detail: {StorefrontBaseUrl}/account/orders/{orderId}
- Guest legacy order: rota existente /pedido/{orderNumber}?t=... apenas quando necessário.

Não logar URLs com tokens.

==================================================
12. ADMIN NOTIFICATION CENTER
==================================================

Não implementar notification center completo nesta fase.

O backend já possui:
- approvals count.
- approvals list.

Nesta fase, para “notificação admin”, implementar:
- e-mail para admin via Brevo quando novo cadastro Pending for criado.
- manter count/list já existentes.

Se desejar deixar base futura:
- documentar que notification center fica fora do escopo.
- não criar tabelas genéricas de notificações agora, salvo se já houver padrão.

==================================================
13. LOGGING E SEGURANÇA
==================================================

Logs permitidos:
- e-mail enfileirado: type, recipient masked, idempotencyKey.
- e-mail enviado: type, outboxId, providerMessageId.
- falha: provider status, mensagem sanitizada.

Nunca logar:
- Brevo ApiKey.
- reset token.
- guest access token.
- senha.
- payload completo com token.
- conteúdo sensível.
- internal note.
- AccessDecisionReason, se tratar como interno.

Mascarar e-mails em logs se houver helper.

==================================================
14. TESTES UNITÁRIOS
==================================================

Criar testes:

Brevo provider:
1. monta payload com sender/to/subject/html.
2. envia api-key no header sem logar.
3. sandbox adiciona X-Sib-Sandbox=drop.
4. sucesso captura messageId.
5. 4xx/5xx vira erro controlado.
6. timeout vira erro recuperável.

Outbox:
7. cria pending com idempotencyKey.
8. não duplica idempotencyKey.
9. worker envia pending.
10. sucesso marca Sent.
11. falha agenda retry.
12. max attempts marca Failed.
13. não processa item Sent.
14. não loga secrets/tokens.

Templates:
15. admin approval email contém link admin.
16. registration received contém status em análise.
17. approved contém link login.
18. rejected não inclui internal reason por padrão.
19. suspended não inclui internal reason por padrão.
20. reset password contém link, mas logs não.
21. order created contém orderNumber.
22. payment confirmed contém orderNumber.
23. shipped contém tracking se houver.
24. delivered renderiza corretamente.
25. delivery batch shipped não expõe batchId interno.
26. delivery batch delivered não expõe batchId interno.
27. internalOrderNote nunca aparece.
28. provider IDs de Pix não aparecem.

Events:
29. CustomerRegisteredPendingApproval enfileira admin + customer.
30. CustomerApproved enfileira customer.
31. CustomerRejected enfileira customer.
32. OrderPaid enfileira payment confirmed.
33. OrderShipped enfileira shipped.
34. OrderDelivered enfileira delivered.
35. DeliveryBatchShipped enfileira um e-mail por pedido.
36. DeliveryBatchDelivered enfileira um e-mail por pedido.

==================================================
15. TESTES INTEGRATION / HTTP
==================================================

Se houver padrão de testes HTTP:

1. register Closed enfileira e-mail admin + customer.
2. approve customer enfileira e-mail approved.
3. reject customer enfileira e-mail rejected.
4. forgot password enfileira e-mail reset.
5. order created enfileira e-mail order created.
6. payment confirmed enfileira e-mail payment confirmed.
7. ship order enfileira shipped.
8. deliver order enfileira delivered.
9. ship batch enfileira e-mails por pedido.
10. deliver batch enfileira e-mails por pedido.

Se não houver infraestrutura:
- criar unit/application tests.
- documentar o que ficou NOT RUN.

==================================================
16. CONFIG / ENVS
==================================================

Atualizar env examples:

Brevo__Enabled=false
Brevo__BaseUrl=https://api.brevo.com
Brevo__ApiKey=
Brevo__SenderName=VIP Assessoria Digital
Brevo__SenderEmail=no-reply@seudominio.com.br
Brevo__ReplyToName=Atendimento
Brevo__ReplyToEmail=atendimento@seudominio.com.br
Brevo__SandboxMode=true
Brevo__TimeoutSeconds=10

AdminNotifications__ApprovalRequestsEmail=admin@seudominio.com.br

AppUrls__StorefrontBaseUrl=https://teste.seudominio.com.br
AppUrls__AdminBaseUrl=https://teste.seudominio.com.br

TESTE:
- Brevo__Enabled=true
- Brevo__SandboxMode=true inicialmente
- depois validar envio real com destinatário controlado, se autorizado.

PROD:
- Brevo__Enabled=true
- Brevo__SandboxMode=false
- sender/domínio validado
- ApiKey real via secret
- admin email real
- URLs produção

==================================================
17. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/integrations/brevo-transactional-emails.md
- docs/customer/customer-approval.md
- docs/customer/customer-approval-emails.md
- docs/orders/order-emails.md
- docs/orders/delivery-fulfillment-phase-2.md, se necessário
- docs/orders/delivery-batch-phase-3.md, se necessário
- docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md
- docs/qa/BREVO-TRANSACTIONAL-EMAILS-SMOKE.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:
- env vars.
- sandbox mode.
- e-mails implementados.
- outbox.
- retries.
- idempotency keys.
- segurança.
- como validar em TESTE.
- o que fica fora do escopo.
- Brevo produção exige sender/domínio validado.

==================================================
18. QA / SMOKE TESTE
==================================================

Criar checklist smoke:

1. Configurar Brevo sandbox.
2. Register customer em loja Closed.
3. Verificar outbox:
   - admin approval request.
   - customer registration received.
4. Processar worker.
5. Verificar status Sent/Skipped conforme sandbox.
6. Aprovar customer.
7. Verificar e-mail approved.
8. Rejeitar customer teste.
9. Verificar e-mail rejected.
10. Forgot password.
11. Verificar e-mail reset.
12. Criar pedido com customer approved.
13. Verificar order created.
14. Confirmar Pix sandbox/reconciliation.
15. Verificar payment confirmed.
16. Marcar pedido enviado.
17. Verificar shipped.
18. Marcar entregue.
19. Verificar delivered.
20. Criar remessa com dois pedidos.
21. Marcar remessa enviada/entregue.
22. Verificar um e-mail por pedido.
23. Confirmar que internal notes não aparecem.
24. Confirmar que tokens/secrets não aparecem em logs.
25. Repetir eventos e garantir que não duplica por idempotencyKey.

==================================================
19. NÃO FAZER
==================================================

Não implementar:
- frontend.
- campanhas/marketing.
- newsletter.
- SMS.
- WhatsApp Business.
- chat.
- notification center completo.
- editor de templates admin.
- webhook de deliverability Brevo, salvo se trivial e isolado.
- tracking UI de e-mail.

Não alterar:
- StoreAccess policy já pronta.
- CustomerApprovalStatus já pronto.
- Checkout/Pix rules.
- Delivery/Remessas rules.
- R2.
- Inventory.
- Catalog.

Não remover:
- guest tracking legado.
- /admin/login.
- cookies/policies separados.

==================================================
20. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Configurações Brevo/AdminNotifications/AppUrls criadas.
3. Abstração de e-mail criada.
4. Provider Brevo criado.
5. Outbox criada.
6. Worker/processamento criado ou integrado.
7. Templates criados.
8. Eventos customer approval integrados.
9. Eventos auth integrados.
10. Eventos order/payment/fulfillment/remessa integrados.
11. Estratégia de idempotência.
12. Estratégia de retry.
13. Segurança/logging.
14. Migrations criadas.
15. Testes criados/alterados.
16. Resultado dotnet build.
17. Resultado dotnet test.
18. Docs atualizadas.
19. Smoke checklist criado.
20. Pendências restantes.

Critérios de aceite:
- cadastro Pending enfileira e-mail para admin e cliente.
- aprovação enfileira e-mail para cliente.
- recusa enfileira e-mail para cliente.
- reset password envia/enfileira e-mail.
- novo pedido enfileira e-mail.
- pagamento confirmado enfileira e-mail.
- enviado/entregue enfileiram e-mails.
- remessa enviada/entregue notifica pedidos envolvidos.
- Brevo sandbox funciona.
- falha Brevo não quebra fluxo principal.
- idempotência evita duplicados.
- secrets/tokens/internal notes não vazam.
- build/testes passam.