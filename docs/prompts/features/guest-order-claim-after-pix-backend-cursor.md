Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core Identity, cookies HttpOnly, segurança de pedidos, e-commerce, checkout convidado, Customer Orders e GuestOrderAccessToken.

Contexto atual:
O Shopflow já possui:

- Checkout convidado.
- Checkout logado.
- Orders.
- PaymentsPix Mercado Pago.
- Worker de reconciliação que marca Order/Pix como Paid.
- GuestOrderAccessToken para acompanhar status de pedido convidado.
- Customer Auth com cookie HttpOnly.
- Customer Orders:
  - GET /api/customer/orders
  - GET /api/customer/orders/{orderId}
- Order.CustomerUserId nullable.
- Pedido guest não aparece automaticamente em Meus pedidos por e-mail.

Problema:
Depois que o Pix é aprovado, o cliente precisa ter um fluxo claro:

1. Se estava logado:
   - ir para o detalhe do pedido em /account/orders/{orderId}.

2. Se comprou como convidado:
   - poder criar uma senha/conta usando os dados do pedido;
   - ou fazer login se já tiver conta;
   - após isso, o pedido deve ficar vinculado ao CustomerUserId e aparecer em Meus pedidos.

Hoje isso não está completo.

Objetivo:
Criar backend seguro para o cliente convidado reivindicar/vincular um pedido guest após Pix aprovado, usando GuestOrderAccessToken como prova de posse do pedido.

Não mexer no frontend neste prompt.
Não implementar e-mail transacional.
Não implementar claim por e-mail sem token.
Não permitir que qualquer usuário reivindique pedido só sabendo o e-mail.
Não mexer em Mercado Pago, Pix, Admin Orders ou Inventory.

==================================================
1. REGRA DE SEGURANÇA PRINCIPAL
==================================================

Pedido guest só pode ser vinculado a uma conta customer se houver:

- orderId válido;
- GuestOrderAccessToken válido;
- token não expirado;
- pedido realmente existe;
- pedido ainda não está vinculado a outro CustomerUserId.

Não usar apenas e-mail.
Não aceitar claim sem token.
Não vazar se pedido existe para token inválido.
Não permitir customer com e-mail diferente reivindicar pedido.

==================================================
2. CASOS DE USO
==================================================

Implementar suporte para estes cenários:

A) Guest comprou e quer criar senha/conta:
- Cliente tem orderId + guestAccessToken no navegador.
- Informa senha.
- Backend usa e-mail/nome/telefone do próprio pedido.
- Cria CustomerUser se ainda não existir.
- Vincula Order.CustomerUserId.
- Opcionalmente autentica o customer com cookie HttpOnly, se esse for o padrão seguro do projeto.

B) Guest comprou, mas já existe conta com o mesmo e-mail:
- Backend não deve sobrescrever a senha.
- Retornar uma resposta clara:
  - AccountAlreadyExists
  - orientar frontend a pedir login.
- Após login customer, frontend poderá chamar endpoint de claim com token.

C) Customer logado quer reivindicar pedido guest:
- Requer CustomerCookie.
- Requer orderId + guestAccessToken.
- Customer e-mail deve bater com CustomerEmail do pedido.
- Se bater, vincular Order.CustomerUserId.
- Se não bater, retornar 403 ou 409 sem vazar dados sensíveis.

D) Pedido já vinculado:
- Se vinculado ao mesmo customer, retornar sucesso idempotente.
- Se vinculado a outro customer, retornar 404/409 seguro.

==================================================
3. ENDPOINTS SUGERIDOS
==================================================

Criar endpoints customer/guest seguros. Sugestão:

1. POST /api/customer/orders/guest/{orderId}/create-account

Body:
{
  "guestAccessToken": "...",
  "password": "...",
  "confirmPassword": "..."
}

Comportamento:
- valida token;
- usa e-mail/nome/telefone do pedido;
- cria customer se e-mail ainda não existir;
- vincula pedido ao CustomerUserId;
- opcionalmente faz sign-in;
- retorna DTO seguro:
{
  "orderId": "...",
  "customerCreated": true,
  "orderLinked": true,
  "redirectTo": "/account/orders/{orderId}"
}

Se conta já existe:
- retornar 409:
{
  "code": "AccountAlreadyExists",
  "message": "Já existe uma conta com este e-mail. Faça login para vincular o pedido."
}

2. POST /api/customer/orders/guest/{orderId}/claim

Auth:
- CustomerCookie obrigatório.

Body:
{
  "guestAccessToken": "..."
}

Comportamento:
- valida token;
- valida customer logado;
- compara e-mail do customer com CustomerEmail do pedido;
- vincula Order.CustomerUserId;
- retorna sucesso.

Observação:
Se o projeto preferir outro naming, manter padrão existente, mas documentar.

==================================================
4. VALIDAÇÕES
==================================================

Senha:
- usar as regras atuais do ASP.NET Identity;
- retornar ProblemDetails por campo:
  - password;
  - confirmPassword.

Token:
- obrigatório;
- válido;
- não expirado;
- hash bate com GuestOrderAccessToken armazenado;
- respeitar rate limit.

Order:
- existe;
- é guest ou já pertence ao mesmo customer;
- não pertence a outro customer.

E-mail:
- para claim logado, e-mail do customer deve bater com e-mail do pedido normalizado.

==================================================
5. SEGURANÇA
==================================================

Obrigatório:

- Não retornar GuestAccessToken novamente.
- Não retornar token hash.
- Não retornar dados internos de pagamento.
- Não usar providerOrderId/providerPaymentId.
- Não permitir claim por e-mail.
- Rate limit nos endpoints.
- Logs sem token bruto.
- Logs com orderId e traceId, mas sem PII excessiva.
- Se token inválido, resposta genérica.
- Se pedido não existe ou token inválido, evitar enumeração.

==================================================
6. INTEGRAÇÃO COM CUSTOMER ORDERS
==================================================

Após vincular:

- GET /api/customer/orders deve passar a listar o pedido.
- GET /api/customer/orders/{orderId} deve retornar o detalhe.
- Pedido guest de mesmo e-mail sem claim continua não aparecendo.

==================================================
7. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes:

1. create-account com token válido cria customer e vincula pedido.
2. create-account com token inválido falha.
3. create-account com token expirado falha.
4. create-account quando e-mail já existe retorna 409 AccountAlreadyExists.
5. create-account não retorna guestAccessToken/token hash.
6. claim logado com mesmo e-mail vincula pedido.
7. claim logado com e-mail diferente falha.
8. claim sem login retorna 401.
9. claim com token inválido falha.
10. pedido já vinculado ao mesmo customer é idempotente.
11. pedido vinculado a outro customer não é reivindicado.
12. após claim, GET /api/customer/orders lista o pedido.
13. pedido guest sem claim não aparece por e-mail.
14. rate limit funciona ou está aplicado conforme padrão.
15. ProblemDetails retorna erros por campo para senha inválida.

Não chamar Mercado Pago real.
Não depender de frontend.

==================================================
8. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/orders/guest-order-claim.md
- docs/orders/customer-orders.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:
- diferença entre guest status e customer orders;
- por que guest não aparece por e-mail automaticamente;
- fluxo pós-Pix aprovado;
- endpoints criados;
- segurança por token;
- account already exists;
- pendências futuras:
  - e-mail transacional;
  - claim via link mágico;
  - recuperação de pedido sem token.

==================================================
9. NÃO FAZER
==================================================

Não implementar:
- frontend;
- e-mail;
- link mágico;
- reset de senha;
- Mercado Pago;
- Admin Orders;
- reembolso;
- cancelamento;
- checkout novo;
- claim por e-mail sem token.

==================================================
10. RESULTADO ESPERADO
==================================================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Endpoints implementados.
3. DTOs criados.
4. Como fica o fluxo para guest criar conta.
5. Como fica o fluxo para customer logado reivindicar pedido.
6. Testes criados/alterados.
7. Resultado dotnet build.
8. Resultado dotnet test.
9. Pendências para frontend.

Critérios de aceite:
- guest pode criar conta/senha após Pix usando token válido;
- se já existe conta, backend orienta login;
- customer logado pode reivindicar pedido guest com token válido e mesmo e-mail;
- pedido reivindicado aparece em Customer Orders;
- pedido guest não aparece só por e-mail;
- nenhum token/secret vaza;
- build passa;
- testes passam.