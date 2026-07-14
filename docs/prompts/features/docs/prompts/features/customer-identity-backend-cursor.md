Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core Identity, cookies HttpOnly, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL e segurança de e-commerce.

Objetivo:
Implementar o backend de Customer Identity do Shopflow, mantendo checkout convidado obrigatório e separando autenticação de cliente da autenticação de admin.

Contexto:
O Shopflow já possui o módulo `IdentityAccess` implementado para Admin.

O backend já possui:

* ASP.NET Core Identity com `ShopflowUser` / `ShopflowRole` usando Guid.
* Schema PostgreSQL `identity`.
* Cookie HttpOnly para admin.
* Policy `Backoffice`.
* Role inicial `Owner`.
* Claim admin `is_staff=true`.
* Endpoints admin:

  * POST /api/auth/admin/login
  * POST /api/auth/admin/logout
  * GET /api/auth/admin/me
  * GET /api/auth/csrf
* CSRF via header `X-CSRF-TOKEN`.
* Login e webhooks excluídos do CSRF.
* CORS com allowlist + credentials.
* Data Protection persistido.
* Scalar/OpenAPI apenas em Development.
* Endpoints admin de Catalog/Inventory protegidos.
* Endpoints sensíveis de Inventory/Orders/PaymentsPix revisados.
* Checkout convidado funcionando.

Decisões já tomadas:

* Não usar JWT.
* Não usar localStorage para token.
* Não usar Auth0, Clerk, Supabase Auth, IdentityServer, OpenIddict ou Cloudflare Access.
* Usar ASP.NET Core Identity próprio.
* Usar cookies HttpOnly.
* Separar admin e cliente por fluxo/cookie.
* Admin já foi feito primeiro.
* Customer Identity será feito agora.
* Guest Order Access Token fica para fase posterior.
* Account real fica para fase posterior.
* Mercado Pago/webhook fica pausado até HML/domínio.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Antes de implementar, leia:

* docs/prompts/00-project-context.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/security/README-identity-security-roadmap.md
* docs/security/SEC-004-endpoint-exposure-review.md
* docs/cart-checkout.md
* docs/orders.md
* docs/payments-pix.md
* módulo IdentityAccess já implementado
* Program.cs
* AdminAuthEndpoints.cs
* testes de IdentityAccess
* middleware CSRF atual
* configuração CORS atual
* configuração Data Protection atual

Siga a arquitetura existente.
Não crie outro sistema paralelo de autenticação.
Não quebre o admin auth já implementado.

==================================================
2. ESCOPO DA FEATURE
====================

Implementar Customer Identity backend com endpoints:

* POST /api/auth/customer/register
* POST /api/auth/customer/login
* POST /api/auth/customer/logout
* GET /api/auth/customer/me
* POST /api/auth/customer/forgot-password
* POST /api/auth/customer/reset-password
* POST /api/auth/customer/confirm-email

Criar autenticação de cliente com cookie HttpOnly separado:

Development:

* `shopflow_customer_dev`

HML/Produção:

* `__Host-shopflow_customer`

Regras:

* Cliente não usa JWT.
* Cliente não recebe token sensível para armazenar no frontend.
* Cookie deve ser HttpOnly.
* Cookie deve ser Secure em HML/Produção.
* Cookie de cliente não deve autenticar admin.
* Cookie de admin não deve autenticar cliente.
* Cliente não pode acessar endpoints Backoffice.
* Admin não deve ser tratado automaticamente como cliente, a menos que explicitamente tenha role/claim de cliente.
* Checkout convidado continua público e funcionando.
* Registro/login de cliente não pode bloquear checkout convidado.

==================================================
3. FORA DO ESCOPO
=================

Não implementar agora:

* Frontend customer auth.
* Login visual do cliente.
* Account real.
* `/account/orders` real.
* Guest Order Access Token.
* Vinculação automática de pedidos convidados.
* Admin Orders.
* Mercado Pago.
* Webhook de pagamento.
* Shipping.
* Notifications reais.
* MFA.
* Roles finas de admin.
* Auditoria completa.
* Redesign.
* Qualquer alteração no frontend.

==================================================
4. ARQUITETURA ESPERADA
=======================

Preferência:
Estender o módulo `IdentityAccess` existente.

Não criar módulo completamente separado se o `IdentityAccess` atual já é o lugar correto para usuários, roles, cookies e auth.

Usar:

* Domain/Application/Infrastructure conforme padrão do módulo.
* CQRS/MediatR se já estiver sendo usado no IdentityAccess.
* FluentValidation.
* EF Core/PostgreSQL.
* Testes de integração.

Separação lógica:

* Admin auth endpoints continuam em `AdminAuthEndpoints`.
* Customer auth endpoints devem ficar em algo como:

  * `CustomerAuthEndpoints.cs`

Se fizer sentido, criar:

* `RegisterCustomerCommand`
* `LoginCustomerCommand`
* `LogoutCustomerCommand`
* `GetCurrentCustomerQuery`
* `ForgotCustomerPasswordCommand`
* `ResetCustomerPasswordCommand`
* `ConfirmCustomerEmailCommand`

Ou seguir o padrão real já usado no módulo IdentityAccess.

==================================================
5. ROLES E CLAIMS
=================

Criar/garantir role:

* `Customer`

Criar/garantir claim para cliente, se fizer sentido no padrão atual:

* `is_customer=true`

Regras:

* Usuário cliente deve ter role `Customer`.
* Usuário cliente não deve ter role `Owner`.
* Usuário cliente não deve ter claim `is_staff=true`.
* Policy `Backoffice` deve continuar exigindo `Owner` + `is_staff=true`.
* Customer endpoints devem reconhecer apenas o cookie/scheme de customer.
* `/api/auth/customer/me` não deve retornar um admin logado apenas com cookie admin.
* `/api/auth/admin/me` não deve retornar um cliente logado apenas com cookie customer.

Adicionar testes cobrindo essa separação.

==================================================
6. COOKIE / AUTH SCHEMES
========================

Adicionar um esquema de cookie separado para customer.

Nomes esperados:

Development:

* `shopflow_customer_dev`

HML/Produção:

* `__Host-shopflow_customer`

Manter admin:

Development:

* `shopflow_admin_dev`

HML/Produção:

* `__Host-shopflow_admin`

Regras:

* Não sobrescrever cookie admin.
* Não reutilizar acidentalmente o scheme admin para cliente.
* Não quebrar `SignInManager`/Identity já existente.
* Se o Identity atual estiver amarrado a um cookie scheme padrão, adaptar com cuidado e documentar a decisão.
* Usar `HttpOnly`.
* Usar `Secure` fora de Development.
* Usar `SameSite` compatível com frontend/API em subdomínios.
* Em HML/Prod com `__Host-`, não definir `Domain` e usar `Path=/`.

Documentar a configuração.

==================================================
7. ENDPOINT: REGISTER
=====================

Endpoint:

POST /api/auth/customer/register

Request sugerido:

{
"email": "[cliente@email.com](mailto:cliente@email.com)",
"password": "SenhaSegura123",
"fullName": "Nome Cliente",
"phone": "11999999999"
}

Regras:

* Validar e-mail.
* Validar senha conforme política atual.
* Validar nome.
* Normalizar e-mail.
* Não permitir duplicidade de e-mail.
* Criar usuário com role `Customer`.
* Não criar staff.
* Não criar Owner.
* Não logar automaticamente se isso contrariar a decisão de confirmação de e-mail.
* Recomendo permitir login mesmo sem confirmação no MVP, mas retornar `emailConfirmed=false` e documentar.
* Se decidir exigir confirmação antes de login, documentar impacto e garantir que não bloqueia checkout convidado.
* Não revelar detalhes sensíveis.
* Retornar dados básicos do cliente, sem token sensível.

Response sugerido:

{
"customerId": "guid",
"email": "[cliente@email.com](mailto:cliente@email.com)",
"fullName": "Nome Cliente",
"phone": "11999999999",
"emailConfirmed": false
}

Status:

* 201 Created em sucesso.
* 400 validação.
* 409 e-mail já cadastrado, ou 400 genérico, conforme padrão de segurança adotado.
* Não retornar senha, hash, claims internas sensíveis.

==================================================
8. ENDPOINT: LOGIN
==================

Endpoint:

POST /api/auth/customer/login

Request:

{
"email": "[cliente@email.com](mailto:cliente@email.com)",
"password": "SenhaSegura123"
}

Regras:

* Login usa cookie HttpOnly customer.
* Não retornar JWT.
* Não retornar token de sessão.
* Não salvar nada em localStorage.
* Verificar que usuário tem role `Customer`.
* Usuário apenas admin/Owner não deve conseguir login como customer, salvo se também tiver role Customer explicitamente.
* Erro de credenciais deve ser genérico.
* Rate limit no login customer.
* Não vazar se e-mail existe ou não.
* Respeitar lockout se ASP.NET Identity estiver configurado.
* Atualizar security stamp/sign-in conforme padrão do Identity.

Response sugerido:

{
"customerId": "guid",
"email": "[cliente@email.com](mailto:cliente@email.com)",
"fullName": "Nome Cliente",
"phone": "11999999999",
"emailConfirmed": true,
"roles": ["Customer"]
}

Status:

* 200 sucesso.
* 401 credenciais inválidas.
* 403 se usuário não é Customer ou está bloqueado, se fizer sentido.
* Mensagem sempre genérica para evitar enumeração.

==================================================
9. ENDPOINT: LOGOUT
===================

Endpoint:

POST /api/auth/customer/logout

Regras:

* Exigir autenticação customer.
* Usar cookie customer.
* Invalidar/sign out apenas customer.
* Não derrubar cookie admin.
* Deve exigir CSRF, salvo se o middleware atual tratar de outra forma.
* Retornar 204 No Content ou 200 conforme padrão.

==================================================
10. ENDPOINT: ME
================

Endpoint:

GET /api/auth/customer/me

Regras:

* Exigir autenticação customer.
* Retornar dados básicos do cliente.
* Não retornar dados sensíveis.
* Não retornar claims internas desnecessárias.
* Se não autenticado, 401.
* Se cookie admin existir mas customer não, 401.

Response sugerido:

{
"customerId": "guid",
"email": "[cliente@email.com](mailto:cliente@email.com)",
"fullName": "Nome Cliente",
"phone": "11999999999",
"emailConfirmed": true,
"roles": ["Customer"]
}

==================================================
11. ENDPOINT: FORGOT PASSWORD
=============================

Endpoint:

POST /api/auth/customer/forgot-password

Request:

{
"email": "[cliente@email.com](mailto:cliente@email.com)"
}

Regras:

* Sempre retornar resposta genérica, mesmo se e-mail não existir.
* Não vazar existência da conta.
* Gerar token de reset usando ASP.NET Identity.
* Não implementar e-mail real completo agora, se não houver infraestrutura de notificações.
* Criar uma abstração mínima, se necessário:

  * `IIdentityEmailSender`
  * implementação Development/NoOp/Logging
* Em Development/Test, pode registrar log seguro ou permitir inspeção em testes.
* Em Produção, não retornar token na resposta.
* Não enviar token em response pública.
* Documentar que envio real de e-mail/notificação será fase futura.

Response:

{
"message": "Se o e-mail estiver cadastrado, enviaremos instruções para redefinição de senha."
}

Rate limit:

* Aplicar rate limit por IP/e-mail quando possível.

==================================================
12. ENDPOINT: RESET PASSWORD
============================

Endpoint:

POST /api/auth/customer/reset-password

Request:

{
"email": "[cliente@email.com](mailto:cliente@email.com)",
"token": "token-recebido",
"newPassword": "NovaSenhaSegura123"
}

Regras:

* Validar token via ASP.NET Identity.
* Validar política de senha.
* Resposta genérica quando falhar.
* Não logar o token.
* Não autenticar automaticamente após reset, a menos que seja decisão explícita documentada.
* Invalidar sessões antigas se Identity permitir via security stamp.
* Retornar 200/204 em sucesso.

==================================================
13. ENDPOINT: CONFIRM EMAIL
===========================

Endpoint:

POST /api/auth/customer/confirm-email

Request:

{
"email": "[cliente@email.com](mailto:cliente@email.com)",
"token": "token-recebido"
}

Regras:

* Gerar token de confirmação no registro.
* Confirmar via ASP.NET Identity.
* Não vazar detalhes sensíveis.
* Não retornar token.
* Em Development/Test, facilitar testes sem expor token em produção.
* Documentar que envio real de e-mail será feito depois.

==================================================
14. CSRF
========

Revisar middleware CSRF atual.

Regras:

* Login customer deve ficar fora do CSRF, como admin login.
* Register, forgot-password, reset-password e confirm-email podem ficar fora do CSRF se forem endpoints públicos sem sessão autenticada, mas documentar a decisão.
* Logout customer deve usar CSRF.
* Futuras mutations autenticadas de customer devem exigir CSRF.
* Não quebrar admin CSRF existente.
* Não quebrar webhook exclusions.

Atualizar testes para garantir:

* logout sem CSRF falha se middleware exigir;
* logout com CSRF passa;
* login não exige CSRF.

==================================================
15. RATE LIMIT
==============

Adicionar rate limit para:

* POST /api/auth/customer/login
* POST /api/auth/customer/register
* POST /api/auth/customer/forgot-password
* POST /api/auth/customer/reset-password

Sugestão inicial:

* login: 10 req/min por IP.
* register: 5 req/min por IP.
* forgot-password: 5 req/min por IP/e-mail se viável.
* reset-password: 5 req/min por IP.

Seguir padrão já usado no admin login.

==================================================
16. PASSWORD POLICY / LOCKOUT
=============================

Revisar política atual de Identity.

Definir/documentar:

* tamanho mínimo.
* exigência de número/letra maiúscula/símbolo, conforme padrão atual.
* lockout após tentativas inválidas, se já configurado.
* tempo de lockout.

Não deixar senha fraca em produção.

==================================================
17. DADOS DO USUÁRIO
====================

Se `ShopflowUser` já tiver campos suficientes, reutilizar.

Campos mínimos para customer:

* Id
* Email
* FullName ou Name
* PhoneNumber
* EmailConfirmed
* CreatedAt, se existir

Se precisar adicionar campos:

* criar migration.
* atualizar mapping.
* atualizar testes.
* não quebrar admin.

Não adicionar endereço nesta feature.
Endereços ficam para Account real.

==================================================
18. CHECKOUT CONVIDADO
======================

Muito importante:
Não alterar fluxo de checkout convidado.

Os endpoints abaixo continuam públicos:

* POST /api/checkout/sessions
* POST /api/orders/from-checkout-session
* POST /api/payments/pix/orders/{orderId}

Não exigir customer login para comprar.
Não vincular pedido ao customer agora.
Não bloquear pedido por e-mail não confirmado.

Se precisar preparar campo opcional futuro de CustomerId no Order/Checkout, não implementar agora sem necessidade.
Documentar como fase posterior.

==================================================
19. SEPARAÇÃO ADMIN X CUSTOMER — TESTES OBRIGATÓRIOS
====================================================

Criar testes para garantir:

1. Cliente registrado não acessa endpoint Backoffice.
2. Cliente logado em `/auth/customer/me` funciona.
3. Cliente logado em `/auth/admin/me` não funciona.
4. Admin logado em `/auth/admin/me` funciona.
5. Admin sem role Customer não funciona em `/auth/customer/me`.
6. Cookie customer não substitui cookie admin.
7. Logout customer não derruba sessão admin.
8. Logout admin não derruba sessão customer, se tecnicamente testável.
9. POST Catalog admin com cookie customer retorna 403 ou 401.
10. Checkout convidado continua funcionando sem cookie customer.

==================================================
20. TESTES FUNCIONAIS CUSTOMER
==============================

Criar testes de integração para:

* register customer válido → 201.
* register e-mail duplicado → erro controlado.
* login válido → 200 + Set-Cookie customer.
* login inválido → 401 genérico.
* `/me` sem login → 401.
* `/me` com cookie customer → 200.
* logout com cookie customer → limpa sessão.
* forgot-password sempre retorna mensagem genérica.
* reset-password com token válido altera senha.
* confirm-email com token válido confirma e-mail.
* rate limit, se viável sem fragilizar testes.

Se tokens de reset/confirm forem difíceis de capturar sem e-mail real, criar suporte de teste limpo no próprio teste, sem expor token em endpoint público de produção.

==================================================
21. MIGRATIONS
==============

Se houver alteração em schema/tabelas:

* criar migration EF Core.
* garantir schema `identity`.
* não quebrar migration existente `InitialIdentityAccess`.
* atualizar docker-compose/envs se necessário.
* garantir ConnectionStrings__IdentityAccess.

Se não houver alteração de schema, documentar que a feature reutiliza as tabelas Identity existentes.

==================================================
22. CONFIGURAÇÃO
================

Adicionar/atualizar appsettings/envs para:

Customer cookie:

* nome dev/prod.
* Secure.
* SameSite.
* expiration/sliding expiration.

Rate limit customer:

* login/register/forgot/reset.

Data Protection:

* reutilizar persistência existente.

Não colocar secrets em appsettings commitado.

==================================================
23. DOCUMENTAÇÃO
================

Criar:

* docs/security/SEC-005-customer-identity-backend.md

Atualizar:

* docs/security/README-identity-security-roadmap.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/testing.md, se existir
* docs/cart-checkout.md, apenas para reafirmar checkout convidado se necessário

Documentar:

* endpoints customer.
* cookie customer.
* separação admin/customer.
* CSRF.
* rate limits.
* forgot/reset/confirm com email sender pendente.
* checkout convidado preservado.
* Account real ainda pendente.
* Guest Order Access Token ainda pendente.
* Mercado Pago ainda pausado até HML/domínio.

==================================================
24. BUILD E TESTES
==================

Executar:

dotnet build
dotnet test

Não pular testes.
Não remover testes existentes.
Se algum teste quebrar, corrigir a causa real.

==================================================
25. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Endpoints customer criados.
3. Como o cookie customer foi configurado.
4. Como separou admin/customer.
5. Roles/claims adicionadas.
6. Como register funciona.
7. Como login funciona.
8. Como logout funciona.
9. Como `/me` funciona.
10. Como forgot/reset/confirm foram implementados.
11. Como CSRF foi tratado.
12. Rate limits adicionados.
13. Migrations criadas ou justificativa se não houve.
14. Testes criados.
15. Resultado de build/test.
16. Docs atualizadas.
17. Limitações conhecidas.
18. Próximo passo recomendado.

Critérios de aceite:

* Customer register funciona.
* Customer login funciona com cookie HttpOnly separado.
* Customer logout funciona.
* Customer `/me` funciona.
* Nenhum JWT foi criado.
* Nenhum token sensível é retornado para o frontend.
* Admin cookie continua funcionando.
* Customer cookie não acessa Backoffice.
* Admin cookie não autentica customer por acidente.
* Checkout convidado continua público.
* CSRF não foi quebrado.
* Rate limit aplicado aos endpoints críticos.
* dotnet build passa.
* dotnet test passa.
* Docs refletem o estado real.
