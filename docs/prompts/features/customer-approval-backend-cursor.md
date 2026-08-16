Você está atuando como backend engineer sênior do projeto Shopflow, especialista em .NET, ASP.NET Core, Identity, Clean Architecture, DDD, EF Core, PostgreSQL, autenticação por cookies, políticas de acesso, checkout e backoffice.

Objetivo:
Implementar a Fase 1 do fluxo Store Access / Customer Approval no backend, conforme ADR:

docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md

Contexto:
O Shopflow passará a suportar loja configurável entre aberta e fechada.

Para o cliente atual:
- StoreAccess:Mode = Closed
- Checkout:AllowGuest = false
- novos clientes entram como Pending
- somente clientes Approved podem acessar/comprar
- checkout convidado fica desabilitado por configuração
- guest tracking legado permanece para compatibilidade
- admin/customer continuam com cookies, schemes e policies separados
- não fundir identidade admin e customer nesta fase

Não implementar frontend neste prompt.
Não implementar Brevo neste prompt.
Não implementar login visual unificado neste prompt.
Não remover guest tracking.
Não remover /admin/login.
Não fundir admin/customer identity.

==================================================
1. CONFIGURAÇÕES
==================================================

Adicionar configurações:

StoreAccess:
  Mode: "Open" | "Closed"

Checkout:
  AllowGuest: true | false

Para TESTE/cliente atual:
StoreAccess__Mode=Closed
Checkout__AllowGuest=false

Regras:
- Open: mantém comportamento configurável para loja aberta/futuro SaaS.
- Closed: catálogo/checkout devem exigir customer aprovado.
- AllowGuest=false: bloqueia novos checkouts/pedidos guest.
- Config default deve ser segura e documentada.
- Não quebrar dev/local sem configuração.

Criar options fortemente tipadas:
- StoreAccessOptions
- CheckoutAccessOptions ou equivalente

Validar valores inválidos:
- StoreAccess:Mode inválido deve falhar no startup ou cair para default seguro documentado.
- Checkout:AllowGuest ausente deve ter default explícito.

==================================================
2. CUSTOMER APPROVAL STATUS
==================================================

Adicionar enum:

CustomerApprovalStatus:
- Pending
- Approved
- Rejected
- Suspended

Adicionar campos no CustomerUser ou entidade equivalente:

- ApprovalStatus
- ApprovalRequestedAt
- ApprovedAt
- ApprovedByAdminId
- RejectedAt
- RejectedByAdminId
- RejectionReason
- SuspendedAt
- SuspendedByAdminId
- SuspensionReason

Se o modelo preferir nomes menores, manter semântica.

Migration:
- clientes existentes devem ser Approved.
- novos clientes devem entrar Pending quando StoreAccess:Mode=Closed ou quando RequireApproval estiver ativo conforme design.
- campo ApprovalStatus deve ter default coerente.

Não usar:
- EmailConfirmed como aprovação comercial.
- IsStaff como aprovação customer.
- Role admin para liberar customer.

==================================================
3. REGISTRO DE CUSTOMER
==================================================

Atualizar fluxo de cadastro customer.

Novo comportamento para loja Closed:
- cria usuário/customer com ApprovalStatus=Pending;
- ApprovalRequestedAt=now;
- não libera compra automaticamente;
- login pode ser permitido, mas status deve aparecer no /me;
- retornar resposta clara indicando Pending.

Resposta sugerida:

{
  "approvalStatus": "Pending",
  "message": "Cadastro enviado para aprovação."
}

Para loja Open:
- decidir se novo usuário entra Approved ou Pending conforme config.
- Recomendação: Open entra Approved, para preservar compatibilidade.

Importante:
- não quebrar confirmação de e-mail, se existir.
- EmailConfirmed continua sendo confirmação técnica de e-mail.
- ApprovalStatus é aprovação comercial/admin.

Criar evento/domínio/aplicação, se houver padrão:
- CustomerRegisteredPendingApproval
ou pelo menos preparar hook futuro para Brevo/admin notification.

Não implementar Brevo agora, apenas evento/outbox hook se já houver padrão simples.

==================================================
4. LOGIN / ME
==================================================

Atualizar DTO de /api/customer/auth/me ou equivalente para incluir:

- approvalStatus
- approvalRequestedAt
- approvedAt

Login com senha correta:
- se Pending, pode autenticar, mas frontend deve saber que está Pending.
- se Rejected/Suspended, pode retornar status com mensagem ou negar login com ProblemDetails, conforme padrão atual.

Recomendação:
- permitir autenticação para Pending, Rejected e Suspended, mas bloquear acesso/checkout por policy.
- Isso permite tela "cadastro em análise" e WhatsApp.

ProblemDetails/codes se negar alguma operação:
- CUSTOMER_APPROVAL_PENDING
- CUSTOMER_ACCESS_REJECTED
- CUSTOMER_ACCESS_SUSPENDED

==================================================
5. POLÍTICA DE CUSTOMER APROVADO
==================================================

Criar serviço/policy:

ICustomerAccessService
ou
CustomerAccessPolicy

Responsabilidade:
- avaliar se customer pode acessar catálogo;
- avaliar se customer pode criar checkout;
- avaliar se guest checkout está permitido;
- avaliar StoreAccess:Mode.

Regras para Closed:
- customer precisa estar autenticado;
- customer precisa ApprovalStatus=Approved;
- guest não pode criar checkout quando Checkout:AllowGuest=false.

Regras para Open:
- comportamento existente pode continuar;
- se Checkout:AllowGuest=true, guest checkout permanece permitido;
- se Checkout:AllowGuest=false, checkout exige customer logado e, se config exigir, aprovado.

Backend é fonte da verdade.
Frontend não pode ser única barreira.

==================================================
6. CHECKOUT — BLOQUEAR GUEST E NÃO APROVADO
==================================================

Atualizar:

POST /api/checkout/sessions

Regras:
- se Checkout:AllowGuest=false e request não tem customer autenticado:
  retornar 401 ou 403 com code GUEST_CHECKOUT_DISABLED ou CUSTOMER_LOGIN_REQUIRED.
- se StoreAccess:Mode=Closed e customer não está Approved:
  retornar 403 com code conforme status:
    CUSTOMER_APPROVAL_PENDING
    CUSTOMER_ACCESS_REJECTED
    CUSTOMER_ACCESS_SUSPENDED
- se customer Approved:
  checkout continua normal.

Não quebrar:
- itens;
- SalesRule;
- estoque;
- CEP;
- delivery preferences;
- Pix.

Codes sugeridos:
- CUSTOMER_LOGIN_REQUIRED
- GUEST_CHECKOUT_DISABLED
- CUSTOMER_APPROVAL_PENDING
- CUSTOMER_ACCESS_REJECTED
- CUSTOMER_ACCESS_SUSPENDED
- CUSTOMER_ACCESS_NOT_APPROVED

==================================================
7. ORDER CREATION — REFORÇO
==================================================

Atualizar:

POST /api/orders/from-checkout-session

Regras:
- para novas sessões em modo Closed, order precisa ter CustomerUserId.
- não criar novo pedido guest quando Checkout:AllowGuest=false.
- validar que a checkout session pertence a customer permitido/aprovado.
- manter compatibilidade com pedidos antigos/guest tracking legado.

Não tornar coluna CustomerUserId obrigatória no banco agora, para preservar histórico.
Mas no fluxo novo Closed, CustomerUserId deve ser obrigatório na prática.

==================================================
8. CATÁLOGO PRIVADO
==================================================

Quando StoreAccess:Mode=Closed:
- endpoints que expõem catálogo completo/preços/SKUs devem exigir CustomerCookie + Approved.

Auditar endpoints públicos:
- GET /api/catalog/products
- GET /api/catalog/products/{slug}
- GET /api/catalog/categories
- GET /api/inventory/skus/{id}, se expõe dado sensível
- outros endpoints storefront

Decisão recomendada:
- proteger produtos/list/detail/preços/SKUs em Closed.
- categorias podem ser públicas ou protegidas. Para cliente atual, preferir proteger junto para evitar vazamento de catálogo.
- health, auth, register, forgot/reset, csrf, CEP continuam públicos.

Implementar sem quebrar Open:
- em Open, catálogo público continua funcionando.
- em Closed, sem customer aprovado retorna 401/403.

Codes:
- STORE_ACCESS_REQUIRES_LOGIN
- STORE_ACCESS_REQUIRES_APPROVAL

==================================================
9. ADMIN CUSTOMER APPROVAL ENDPOINTS
==================================================

Criar endpoints Backoffice + CSRF para mutations.

Sugestão:

GET /api/admin/customers/approvals
GET /api/admin/customers/approvals/count
GET /api/admin/customers/{customerId}

POST /api/admin/customers/{customerId}/approve
POST /api/admin/customers/{customerId}/reject
POST /api/admin/customers/{customerId}/suspend
POST /api/admin/customers/{customerId}/reactivate

List params:
- page
- pageSize
- q
- status
- createdFrom
- createdTo
- sort

Para MVP:
- listar Pending por default.
- q busca nome/e-mail/telefone.

Approve:
- Pending/Rejected/Suspended -> Approved
- set ApprovedAt
- set ApprovedByAdminId
- clear rejection/suspension reason se fizer sentido

Reject:
payload:
{
  "reason": "..."
}
- Pending -> Rejected
- set RejectedAt
- set RejectedByAdminId
- reason opcional max 1000

Suspend:
payload:
{
  "reason": "..."
}
- Approved -> Suspended
- set SuspendedAt
- set SuspendedByAdminId
- reason opcional max 1000

Reactivate:
- Suspended/Rejected -> Approved, se permitido
- documentar transição

Regras:
- admin endpoints exigem Backoffice.
- mutations exigem CSRF.
- customer inexistente -> 404.
- operação inválida -> ProblemDetails.
- não permitir customer comum aprovar.

DTO list item:
- customerId
- name
- email
- phone
- approvalStatus
- approvalRequestedAt
- approvedAt
- rejectedAt
- suspendedAt

DTO detail:
- dados do customer
- approval status/timestamps
- razões
- dados seguros/operacionais

Não expor senha/hash/tokens.

==================================================
10. ADMIN DASHBOARD COUNT
==================================================

Expor count de aprovações pendentes:

GET /api/admin/customers/approvals/count

Resposta:

{
  "pending": 3
}

Ou incluir em endpoint existente de dashboard, se houver padrão melhor.

Objetivo:
Frontend mostrará badge/card.

Não implementar notification center.

==================================================
11. PROBLEM DETAILS / CODES
==================================================

Adicionar/mapeiar codes:

- CUSTOMER_LOGIN_REQUIRED
- GUEST_CHECKOUT_DISABLED
- CUSTOMER_APPROVAL_PENDING
- CUSTOMER_ACCESS_REJECTED
- CUSTOMER_ACCESS_SUSPENDED
- CUSTOMER_ACCESS_NOT_APPROVED
- STORE_ACCESS_REQUIRES_LOGIN
- STORE_ACCESS_REQUIRES_APPROVAL
- CUSTOMER_APPROVAL_INVALID_STATUS
- CUSTOMER_APPROVAL_REASON_TOO_LONG
- CUSTOMER_NOT_FOUND

Mensagens PT-BR sugeridas:
- "Para comprar, entre com uma conta aprovada."
- "Seu cadastro ainda está em análise."
- "Seu cadastro não foi aprovado neste momento."
- "Seu acesso está temporariamente bloqueado."
- "O checkout como convidado está desabilitado."
- "Esta loja está disponível apenas para clientes aprovados."
- "Não foi possível alterar o status deste cliente."

==================================================
12. COMPATIBILIDADE
==================================================

Preservar:
- guest tracking legado;
- pedidos antigos sem CustomerUserId;
- guest access token para pedidos antigos;
- Open mode para futuro SaaS;
- admin/customer schemes separados;
- /admin/login por enquanto;
- customer auth existente;
- reset password existente;
- Pix;
- delivery;
- remessas;
- R2.

Não reescrever auth.

==================================================
13. TESTES UNITÁRIOS
==================================================

Criar/ajustar testes:

Config/policy:
1. StoreAccess Open permite catálogo público.
2. StoreAccess Closed exige customer.
3. StoreAccess Closed exige Approved.
4. Checkout AllowGuest=false bloqueia guest.
5. Checkout AllowGuest=true preserva guest em Open.
6. Pending não pode criar checkout.
7. Rejected não pode criar checkout.
8. Suspended não pode criar checkout.
9. Approved pode criar checkout.

Register/auth:
10. Closed register cria Pending.
11. Open register cria Approved ou comportamento documentado.
12. /me retorna approvalStatus.
13. clientes existentes migrados como Approved.

Checkout/order:
14. guest checkout bloqueado quando AllowGuest=false.
15. pending checkout retorna ProblemDetails.
16. approved checkout funciona.
17. order creation em Closed exige CustomerUserId.
18. pedido antigo guest continua acessível via tracking legado.

Catalog:
19. Closed bloqueia products list sem login.
20. Closed bloqueia product detail sem aprovação.
21. Open mantém products list público.
22. Approved acessa catálogo Closed.

Admin:
23. approvals list exige Backoffice.
24. approvals count exige Backoffice.
25. approve muda Pending -> Approved.
26. reject muda Pending -> Rejected.
27. suspend muda Approved -> Suspended.
28. reactivate muda Suspended -> Approved.
29. reason max length validado.
30. customer comum não acessa admin approval.
31. DTO não expõe hash/tokens.

==================================================
14. TESTES HTTP / INTEGRATION
==================================================

Se houver padrão HTTP:
1. GET catalog Closed sem auth -> 401/403.
2. GET catalog Closed com Pending -> 403.
3. GET catalog Closed com Approved -> 200.
4. POST checkout guest com AllowGuest=false -> 401/403.
5. POST checkout Pending -> 403.
6. POST checkout Approved -> sucesso.
7. POST admin approve sem CSRF -> bloqueia.
8. POST admin approve com Backoffice+CSRF -> sucesso.
9. GET approvals sem admin -> 401/403.

Se não houver suite HTTP, criar unit/handler e documentar ausência.

==================================================
15. DOCUMENTAÇÃO
==================================================

Atualizar:
- docs/architecture/STORE-ACCESS-CUSTOMER-APPROVAL-DESIGN.md, se necessário
- docs/customer/customer-approval.md
- docs/checkout/checkout-session.md
- docs/catalog/store-access.md
- docs/orders/guest-order-access.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md
- deploy/.env.example
- deploy/.env.prod.example, se existir
- apps/web/docs/ai-context/api-contracts.md, se existir no monorepo

Documentar:
- StoreAccess Mode Open/Closed
- Checkout AllowGuest
- CustomerApprovalStatus
- endpoints admin
- codes ProblemDetails
- comportamento legado de guest tracking
- que login visual unificado é frontend/Fase 2
- que Brevo approval e-mails é Fase 3

==================================================
16. NÃO FAZER
==================================================

Não implementar:
- frontend
- Brevo
- e-mail
- login visual unificado
- /admin/login redirect
- notification center
- SaaS multi-tenant completo
- unificação real admin/customer identity
- remoção de guest tracking

Não alterar:
- Pix
- Delivery/Fulfillment
- Remessas
- R2
- Inventory
- SalesRules
- PaymentsPix

Não desabilitar:
- CSRF
- Backoffice policy
- Customer policy

==================================================
17. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Configurações criadas.
3. Enum/status criado.
4. Migration criada.
5. Register atualizado.
6. /me/login atualizado.
7. Checkout gate implementado.
8. Order creation reforçado.
9. Catalog gate implementado.
10. Admin approval endpoints criados.
11. Pending approvals count criado.
12. ProblemDetails/codes criados.
13. Testes criados/alterados.
14. Resultado dotnet build.
15. Resultado dotnet test afetado.
16. Docs atualizadas.
17. Pendências para frontend/Brevo.

Critérios de aceite:
- StoreAccess Open/Closed funciona.
- Closed bloqueia catálogo para não aprovado.
- Closed bloqueia checkout para não aprovado.
- AllowGuest=false bloqueia novo guest checkout.
- Register cria Pending.
- Admin aprova cliente.
- Approved compra.
- Rejected/Suspended não compram.
- Pedidos antigos guest continuam compatíveis.
- Admin endpoints protegidos.
- Build/testes passam.