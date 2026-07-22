Você está atuando como engenheiro backend sênior do projeto Shopflow, especialista em .NET 10, ASP.NET Core Identity, Clean Architecture, DDD, Orders, Customer Identity, Guest Checkout, segurança de pedidos, ProblemDetails e APIs de e-commerce.

Objetivo:
Melhorar o fluxo pós-Pix aprovado para compra convidada, separando claramente:
1. compra/pagamento concluído;
2. acompanhamento de pedido como convidado;
3. criação opcional de conta;
4. login e vínculo de pedido quando a conta já existe.

Não mexer no frontend neste prompt.
Não alterar Mercado Pago, checkout, pagamentos, admin orders ou inventory, exceto se necessário para expor dados seguros de pedido.
Não implementar e-mail transacional agora.
Não implementar claim por e-mail sem token.
Não permitir acesso/vínculo apenas por orderId.

==================================================
1. CONTEXTO ATUAL
==================================================

O Shopflow já possui:

- Checkout convidado.
- Checkout logado.
- Orders.
- PaymentsPix Mercado Pago.
- Worker de reconciliação que marca Order/Pix como Paid.
- GuestOrderAccessToken para status de pedido convidado.
- Customer Auth com cookie HttpOnly.
- Customer Orders:
  - GET /api/customer/orders
  - GET /api/customer/orders/{orderId}
- Order.CustomerUserId nullable.
- Pedido guest não aparece automaticamente por e-mail.
- Endpoints recentes de guest claim:
  - POST /api/customer/orders/guest/{orderId}/create-account
  - POST /api/customer/orders/guest/{orderId}/claim

Problema atual:
Na tela pós-Pix, a criação de conta aparece como continuação obrigatória da compra. Quando a criação da conta falha, o usuário pode achar que o pedido falhou, mesmo com pagamento aprovado.

Também existe erro genérico:

{
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "password": [
      "Unable to complete registration."
    ]
  }
}

Isso esconde a causa real do ASP.NET Identity, como:
- senha fraca;
- e-mail já cadastrado;
- usuário existente;
- regra de maiúscula/minúscula;
- caractere especial;
- tamanho mínimo;
- erro de username.

==================================================
2. PRINCÍPIO DE PRODUTO
==================================================

A compra precisa terminar independentemente da criação de conta.

Fluxo correto:

Guest checkout
→ Order criada
→ Pix aprovado
→ Order Paid
→ Cliente consegue acompanhar pedido como guest
→ Opcionalmente cria conta ou faz login para vincular pedido

Criar conta NÃO pode ser requisito para:
- pedido existir;
- pedido estar pago;
- cliente acompanhar pedido guest;
- admin ver pedido.

==================================================
3. SEGURANÇA
==================================================

Toda operação de acompanhar/vincular pedido guest deve exigir prova de posse:

- orderId;
- GuestOrderAccessToken válido;
- token não expirado;
- token pertence ao pedido;
- pedido não está vinculado a outro customer.

Não permitir:
- claim só por e-mail;
- claim só por orderId;
- criação de conta sem token;
- vazar se o pedido existe quando token inválido;
- retornar GuestAccessToken novamente;
- retornar token hash;
- retornar providerOrderId/providerPaymentId;
- retornar dados internos Mercado Pago.

==================================================
4. ACESSO GUEST AO PEDIDO SEM CRIAR CONTA
==================================================

Criar ou revisar endpoint seguro para detalhe/status de pedido guest após compra.

Se já existir:
GET /api/orders/guest/{orderId}/status

Avaliar se ele é suficiente para a tela “Acompanhar pedido”.

Caso seja limitado demais, propor/criar endpoint:

GET /api/orders/guest/{orderId}

Headers:
X-ORDER-ACCESS-TOKEN: {guestAccessToken}

Retorno seguro sugerido:

{
  "id": "guid",
  "orderNumber": "10582",
  "status": "Paid",
  "paymentStatus": "Paid",
  "createdAt": "...",
  "paidAt": "...",
  "customerEmailMasked": "cli***@email.com",
  "amounts": {
    "subtotal": 169.90,
    "shippingAmount": 0,
    "total": 169.90
  },
  "items": [
    {
      "productName": "...",
      "skuCode": "...",
      "quantity": 1,
      "unitPrice": 169.90,
      "subtotal": 169.90
    }
  ],
  "shippingAddress": {
    "city": "...",
    "state": "...",
    "zipCode": "..."
  },
  "canCreateAccount": true,
  "accountExistsForEmail": false
}

Cuidado:
- Não retornar PII completa se o endpoint for guest, a menos que a posse via token seja considerada suficiente pelo padrão do projeto.
- Definir e documentar quais campos o guest pode ver.
- Não retornar token.

Se decidir manter só o status atual, documentar por quê e garantir que o frontend tenha dados suficientes para tela de sucesso:
- orderNumber;
- total;
- status;
- e-mail mascarado ou e-mail completo, conforme decisão.

==================================================
5. ORDER NUMBER AMIGÁVEL
==================================================

Adicionar número amigável de pedido para UI e atendimento.

Hoje o cliente vê GUID, que é ruim para suporte.

Manter:
- Order.Id = Guid interno.

Adicionar:
- Order.OrderNumber ou similar.

Opções:
A) inteiro sequencial por banco;
B) número formatado;
C) string curta única.

Recomendação:
- criar campo OrderNumber string ou long;
- garantir unicidade;
- gerar ao criar Order;
- exibir como #10582 ou #00010582.

Requisitos:
- migration segura;
- preencher pedidos existentes, se necessário;
- índice único;
- não substituir o Guid internamente;
- Admin Orders e Customer Orders podem continuar retornando Guid, mas devem também retornar orderNumber;
- Guest status/detail deve retornar orderNumber.

Atualizar DTOs:
- Admin Orders list/detail;
- Customer Orders list/detail;
- Guest order status/detail;
- Order creation response, se útil.

==================================================
6. CREATE ACCOUNT FROM GUEST ORDER
==================================================

Revisar endpoint:

POST /api/customer/orders/guest/{orderId}/create-account

Body:
{
  "guestAccessToken": "...",
  "password": "...",
  "confirmPassword": "..."
}

Comportamento correto:

1. Validar orderId + guestAccessToken.
2. Validar pedido existe, token válido, não expirado.
3. Verificar se Order.CustomerUserId já está preenchido.
4. Usar e-mail/nome/telefone do pedido.
5. Se não existe customer com esse e-mail:
   - criar customer;
   - aplicar regras reais do Identity;
   - se Identity falhar, retornar erros específicos;
   - vincular Order.CustomerUserId;
   - autenticar customer com cookie HttpOnly, se padrão atual permite;
   - retornar redirectTo: /account/orders/{orderId}.
6. Se já existe customer com esse e-mail:
   - NÃO tentar criar outro;
   - retornar 409 com code ACCOUNT_ALREADY_EXISTS.

Resposta sucesso:

{
  "code": "ACCOUNT_CREATED_AND_ORDER_LINKED",
  "orderId": "...",
  "orderNumber": "10582",
  "customerCreated": true,
  "orderLinked": true,
  "redirectTo": "/account/orders/{orderId}"
}

Resposta conta existente:

HTTP 409
{
  "code": "ACCOUNT_ALREADY_EXISTS",
  "message": "Já existe uma conta vinculada a este e-mail. Entre para adicionar este pedido ao seu histórico.",
  "redirectTo": "/login"
}

Senha inválida:

HTTP 400
{
  "code": "PASSWORD_REQUIREMENTS_NOT_MET",
  "message": "A senha não atende aos requisitos.",
  "errors": {
    "password": [
      "Use pelo menos 8 caracteres.",
      "Use pelo menos uma letra maiúscula."
    ]
  },
  "traceId": "..."
}

Não usar mensagem genérica “Unable to complete registration.” quando houver IdentityResult.Errors disponíveis.

==================================================
7. CLAIM APÓS LOGIN
==================================================

Revisar endpoint:

POST /api/customer/orders/guest/{orderId}/claim

Auth:
- CustomerCookie obrigatório.
- CSRF obrigatório se padrão do projeto exige em mutações autenticadas.

Body:
{
  "guestAccessToken": "..."
}

Regras:
- validar token;
- validar customer logado;
- comparar e-mail do customer com Order.CustomerEmail normalizado;
- se e-mail bater:
  - vincular Order.CustomerUserId;
  - retornar sucesso;
- se já vinculado ao mesmo customer:
  - sucesso idempotente;
- se vinculado a outro customer:
  - retornar 409 seguro;
- se e-mail diferente:
  - retornar 403/409 com mensagem segura.

Resposta sucesso:
{
  "code": "ORDER_LINKED",
  "orderId": "...",
  "orderNumber": "10582",
  "redirectTo": "/account/orders/{orderId}"
}

==================================================
8. CÓDIGOS DE ERRO OFICIAIS
==================================================

Padronizar códigos:

- ACCOUNT_ALREADY_EXISTS
- PASSWORD_REQUIREMENTS_NOT_MET
- INVALID_GUEST_ORDER_TOKEN
- GUEST_ORDER_TOKEN_EXPIRED
- ORDER_ALREADY_LINKED
- ORDER_LINKED_TO_ANOTHER_CUSTOMER
- CUSTOMER_EMAIL_DOES_NOT_MATCH_ORDER
- ORDER_NOT_PAID_YET, se necessário
- ORDER_NOT_FOUND_OR_ACCESS_DENIED
- ACCOUNT_CREATED_AND_ORDER_LINKED
- ORDER_LINKED

Documentar quais retornam 400, 401, 403, 404, 409.

Critério:
- frontend precisa conseguir decidir fluxo com base em `code`, não em texto livre.

==================================================
9. STATUS PÓS-PAGAMENTO
==================================================

Verificar se criação de conta/claim deve ser permitida apenas após pagamento aprovado.

Decisão recomendada:
- permitir criação/claim após pedido criado e token válido, mas a UI deve incentivar após Paid.
- Se backend exigir Paid, retornar ORDER_NOT_PAID_YET quando Pending.
- Documentar decisão.

Para MVP, pode permitir claim antes de Paid, desde que isso não mude status de pedido nem pagamento.

==================================================
10. PROBLEMDETAILS
==================================================

Padronizar erros em ProblemDetails compatível com frontend.

Requisitos:
- title;
- status;
- detail;
- traceId;
- code;
- errors por campo quando aplicável.

Para IdentityResult:
- mapear erros do Identity para `password`, `email`, `userName` quando possível;
- preservar descrição legível;
- não usar só erro genérico.

==================================================
11. TESTES OBRIGATÓRIOS
==================================================

Criar/ajustar testes:

Guest access:
1. Guest consegue consultar/acessar pedido com token válido.
2. Token ausente falha.
3. Token inválido falha.
4. Token expirado falha.
5. Não retorna token/hash/provider IDs.

OrderNumber:
6. Order criada recebe orderNumber.
7. orderNumber é único.
8. Admin Orders retorna orderNumber.
9. Customer Orders retorna orderNumber.
10. Guest status/detail retorna orderNumber.

Create account:
11. Guest com token válido cria customer e vincula pedido.
12. Guest sem token não cria conta.
13. Guest com token inválido não cria conta.
14. Guest com token expirado não cria conta.
15. E-mail já existente retorna 409 ACCOUNT_ALREADY_EXISTS.
16. Senha fraca retorna PASSWORD_REQUIREMENTS_NOT_MET com erros reais do Identity.
17. Não retorna “Unable to complete registration.” se houver erros específicos.
18. Sucesso autentica customer ou documenta se não autenticar.
19. Pedido vinculado aparece em Customer Orders.

Claim:
20. Customer logado com mesmo e-mail + token válido vincula pedido.
21. Customer logado com e-mail diferente falha.
22. Pedido já vinculado ao mesmo customer é idempotente.
23. Pedido vinculado a outro customer falha.
24. Claim sem login retorna 401.
25. Claim sem CSRF falha se padrão exigir.
26. Claim com CSRF passa.
27. Pedido guest sem claim não aparece só por e-mail.

Segurança:
28. Não vaza PII indevida.
29. Não vaza token.
30. Rate limit aplicado aos endpoints sensíveis.

Não chamar Mercado Pago real.
Não depender de frontend.

==================================================
12. DOCUMENTAÇÃO
==================================================

Atualizar/criar:

- docs/orders/post-pix-guest-flow.md
- docs/orders/guest-order-claim.md
- docs/orders/customer-orders.md
- docs/orders/admin-orders.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Documentar:
- compra convidada termina sem conta;
- conta é opcional;
- guest acompanha pedido com token;
- create-account;
- account already exists;
- login + claim;
- orderNumber;
- códigos de erro;
- segurança;
- limitações sem e-mail transacional.

==================================================
13. NÃO FAZER
==================================================

Não implementar:
- frontend;
- e-mail;
- link mágico;
- reset de senha novo;
- pagamento;
- checkout novo;
- admin UI;
- tracking;
- cancelamento;
- reembolso;
- nota fiscal;
- claim sem token;
- recuperação sem token.

==================================================
14. RESULTADO ESPERADO
==================================================

Ao final, retorne:

1. Arquivos alterados.
2. Endpoints criados/ajustados.
3. DTOs ajustados.
4. Códigos de erro oficiais.
5. Como ficou OrderNumber.
6. Como ficou guest access sem conta.
7. Como ficou create-account.
8. Como ficou login+claim.
9. Testes criados/alterados.
10. Resultado dotnet build.
11. Resultado dotnet test.
12. Pendências para frontend.

Critérios de aceite:
- compra guest não depende de criação de conta;
- guest consegue acompanhar pedido sem conta usando token;
- create-account retorna ACCOUNT_ALREADY_EXISTS quando aplicável;
- Identity errors não viram mensagem genérica;
- claim exige token + customer logado + e-mail compatível;
- orderNumber aparece nos DTOs necessários;
- nenhum token/secret vaza;
- build passa;
- testes passam.