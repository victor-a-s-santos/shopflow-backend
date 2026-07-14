Você está atuando como engenheiro backend sênior do projeto Shopflow.

Objetivo:
Implementar o módulo PaymentsPix MVP no backend com abstração de provider, sem integração real com gateway Pix nesta etapa.

Contexto atual:

* Catalog está implementado.
* Inventory está implementado.
* CartCheckout está implementado parcialmente:

  * cria sessão de checkout;
  * recalcula preço;
  * reserva estoque;
  * retorna payment Pix NotImplemented.
* Orders foi implementado:

  * cria Order a partir de CheckoutSession;
  * pedido nasce com status PendingPayment;
  * não confirma pagamento;
  * não confirma reserva de estoque;
  * não exige login.
* Frontend já faz:

  * POST /api/checkout/sessions;
  * POST /api/orders/from-checkout-session;
  * exibe pedido PendingPayment.
* PaymentsPix ainda não existe.
* Não há provedor Pix escolhido.
* A decisão atual é criar uma base técnica com provider abstrato, sem pagamento real.

Objetivo desta etapa:
Criar o módulo PaymentsPix MVP para registrar uma intenção/cobrança Pix associada a um Order PendingPayment, com provider abstrato e implementação fake/dev, sem marcar pagamento como aprovado.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Antes de implementar, leia:

* docs/prompts/00-project-context.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/orders.md
* docs/cart-checkout.md
* módulos existentes:

  * Catalog
  * Inventory
  * CartCheckout
  * Orders
* padrões de:

  * Domain
  * Application
  * Infrastructure
  * Endpoints Minimal API
  * EF Core migrations
  * Exceptions
  * Validators
  * Testes

Não inventar arquitetura nova.
Seguir os padrões existentes.

==================================================
2. ESCOPO
=========

Implementar PaymentsPix MVP com:

* Domain
* Application
* Infrastructure
* EF Core DbContext
* Migrations
* Endpoints Minimal API
* Provider abstraction
* Provider fake/dev
* Tests unitários
* Tests de integração
* Documentação

Endpoints sugeridos:

POST /api/payments/pix/orders/{orderId}
GET /api/payments/pix/{paymentId}
GET /api/payments/pix/by-order/{orderId}

Opcional para ambiente dev/teste, se fizer sentido:

POST /api/payments/pix/{paymentId}/simulate-paid

Mas só implementar esse endpoint se ficar claramente restrito/documentado como DEV/SANDBOX e sem fingir gateway real.

==================================================
3. FORA DO ESCOPO
=================

Não implementar agora:

* gateway real Pix;
* Mercado Pago, Pagar.me, Asaas, Efí ou banco real;
* webhook real;
* QR Code real;
* copia e cola real;
* baixa definitiva de estoque, se ainda não houver decisão consolidada;
* e-mail/WhatsApp;
* checkout frontend;
* admin pagamentos;
* conciliação financeira real;
* autenticação;
* captura de pagamento real.

Não marcar Order como Paid automaticamente neste MVP, exceto se houver endpoint explícito de simulação dev e a decisão for implementada com segurança.

==================================================
4. MODELAGEM DOMAIN
===================

Criar aggregate PixPayment.

Campos sugeridos:

PixPayment:

* Id
* OrderId
* Amount
* Status
* Provider
* ProviderPaymentId nullable
* QrCode nullable
* QrCodeImageUrl nullable
* CopyPasteCode nullable
* ExpiresAt nullable
* CreatedAt
* PaidAt nullable
* CanceledAt nullable
* FailedAt nullable
* FailureReason nullable

PixPaymentStatus enum:

* Pending
* Paid
* Canceled
* Expired
* Failed

PixPaymentProvider enum ou string:

* Fake
* NotConfigured
* FutureProvider

Regras:

* Payment sempre pertence a um Order.
* Só criar pagamento Pix para Order com status PendingPayment.
* Não criar pagamento com Amount <= 0.
* Não criar mais de um PixPayment Pending para o mesmo Order.
* Se já existir PixPayment Pending para o Order, retornar o existente ou retornar 409 — escolha uma estratégia e documente.
* Payment criado com provider fake/dev deve ficar Pending.
* Não marcar como Paid automaticamente.
* Não gerar QR real se não houver provider real.
* Se houver campos de QR/copia-e-cola no fake, eles devem ser claramente fake/dev.

Minha recomendação:
Se já existir pagamento Pending para a Order, retornar o pagamento existente ao invés de criar outro, para tornar o endpoint idempotente.

==================================================
5. INTEGRAÇÃO COM ORDERS
========================

Criar abstração em PaymentsPix.Application:

IOrderPaymentReader

ou equivalente.

Ela deve permitir:

* buscar Order por Id;
* validar se existe;
* obter total;
* obter status;
* obter dados mínimos necessários.

Também criar, se necessário:

IOrderPaymentUpdater

ou equivalente para futura atualização de status.

Nesta fase:

* Payment deve consultar Order;
* validar Order PendingPayment;
* usar Total do Order como Amount;
* não alterar status da Order automaticamente na criação do Pix.

Se implementar simulate-paid:

* alterar PixPayment para Paid;
* chamar Orders para marcar Order como Paid;
* definir PaidAt;
* documentar que isso é somente dev/test.
* Avaliar se deve confirmar reserva de estoque no Inventory via fluxo já existente.
* Se não houver fluxo seguro para confirmar reserva, não confirmar estoque ainda e documentar a pendência.

Minha recomendação:
Nesta fase, não implementar simulate-paid se isso exigir mexer em Order/Inventory de forma apressada.
Criar somente payment Pending e deixar webhook/simulação para próxima etapa.

==================================================
6. PROVIDER ABSTRACTION
=======================

Criar interface:

IPixPaymentProvider

Método sugerido:

CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken)

Request:

* orderId
* amount
* customerName
* customerEmail
* expiresAt

Response:

* provider
* providerPaymentId nullable
* qrCode nullable
* qrCodeImageUrl nullable
* copyPasteCode nullable
* expiresAt nullable
* status

Implementação inicial:

FakePixPaymentProvider
ou
NotConfiguredPixPaymentProvider

Comportamento:

* não chama API externa;
* retorna status Pending;
* providerPaymentId fake/dev ou null;
* QR/copia-e-cola null ou texto claramente fake;
* mensagem/documentação deixando claro que gateway real será plugado depois.

Não fazer:

* QR Code real;
* copia-e-cola real;
* requisição externa;
* credenciais;
* secrets;
* callback/webhook real.

==================================================
7. COMMANDS / QUERIES
=====================

Criar CQRS/MediatR seguindo padrão do projeto.

Commands:

* CreatePixPaymentForOrderCommand

Queries:

* GetPixPaymentByIdQuery
* GetPixPaymentByOrderIdQuery

DTOs:

* CreatePixPaymentResponse ou PixPaymentDto
* PixPaymentProviderInfoDto, se necessário

Request:
POST /api/payments/pix/orders/{orderId}

Não precisa body no MVP, pois amount vem do Order.

Response:

{
"paymentId": "guid",
"orderId": "guid",
"status": "Pending",
"provider": "Fake",
"amount": 100.00,
"qrCode": null,
"qrCodeImageUrl": null,
"copyPasteCode": null,
"expiresAt": "...",
"createdAt": "...",
"message": "Pagamento Pix criado em modo preparação. Gateway real ainda não integrado."
}

==================================================
8. VALIDATION
=============

Validar:

* orderId obrigatório;
* Order precisa existir;
* Order precisa estar PendingPayment;
* Order total > 0;
* não duplicar payment Pending para mesma Order.

Erros esperados:

400:

* orderId inválido;
* Order total inválido.

404:

* Order não encontrada;
* PixPayment não encontrado.

409:

* Order não está PendingPayment;
* já existe pagamento Pix em status incompatível, se não optar por idempotência.

==================================================
9. BANCO / MIGRATIONS
=====================

Schema sugerido:

payments_pix

Tabelas:

payments_pix.pix_payments

Campos:

* id
* order_id
* amount
* status
* provider
* provider_payment_id
* qr_code
* qr_code_image_url
* copy_paste_code
* expires_at
* created_at
* paid_at
* canceled_at
* failed_at
* failure_reason

Constraints:

* amount > 0
* status not null
* order_id not null

Índices:

* order_id
* unique partial index para pending por order, se o banco/padrão suportar
* provider_payment_id, se não null

Se EF/Postgres e migrations já suportarem partial index, usar:

* unique onde status = Pending para impedir múltiplos pagamentos pendentes para a mesma Order.

Se não, garantir por regra application + índice único simples quando fizer sentido.

Usar history table/migrations seguindo padrão do projeto.

==================================================
10. ENDPOINTS MINIMAL API
=========================

Criar endpoints em HttpApi seguindo padrão.

Endpoints:

POST /api/payments/pix/orders/{orderId}
GET /api/payments/pix/{paymentId}
GET /api/payments/pix/by-order/{orderId}

Tags:

* PaymentsPix

Status:

* 201 Created para criação nova
* 200 OK se retornar payment pending existente de forma idempotente
* 200 OK para consultas
* 400/404/409 para erros

Não criar:

* endpoint webhook real;
* endpoint QR real;
* endpoint confirmação real.

==================================================
11. TESTES
==========

Criar testes unitários:

Domain:

* cria PixPayment Pending válido;
* não permite amount <= 0;
* não marca como Paid automaticamente;
* status inicial Pending;
* provider Fake/NotConfigured.

Application:

* cria PixPayment para Order PendingPayment;
* falha se Order não existe;
* falha se Order não está PendingPayment;
* retorna existente se já existe Pending, se estratégia idempotente;
* falha se Order total <= 0.

Integration:

* POST /api/payments/pix/orders/{orderId} cria payment;
* GET /api/payments/pix/{paymentId} retorna payment;
* GET /api/payments/pix/by-order/{orderId} retorna payment;
* chamada duplicada retorna existing ou 409, conforme estratégia;
* Order inexistente retorna 404.

Executar:

dotnet test

==================================================
12. DOCUMENTAÇÃO
================

Criar/atualizar:

* docs/payments-pix.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/architecture.md, se necessário
* docs/testing.md, se necessário

Documentar claramente:

* PaymentsPix MVP existe.
* Gateway real ainda não existe.
* QR Code/copia-e-cola real ainda não existe.
* PixPayment nasce Pending.
* Order continua PendingPayment.
* Payment não significa pagamento aprovado.
* Frontend ainda não está integrado, a menos que explicitamente implementado depois.
* Próxima etapa será integrar frontend para chamar PaymentsPix e exibir “Pix em preparação” ou, depois, plugar provider real.

==================================================
13. BUILD E TESTES
==================

Executar:

dotnet build
dotnet test

Se houver erro pré-existente, documentar.
Não pular testes sem justificativa.

==================================================
14. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Entidades/domain criados.
3. Provider abstraction criada.
4. Provider fake/dev implementado.
5. Endpoints implementados.
6. Request/response final.
7. Como PaymentsPix consulta Order.
8. Estratégia para payment duplicado por Order.
9. Status inicial do PixPayment.
10. Se Order é alterado ou não nesta etapa.
11. Testes criados.
12. Resultado de dotnet build/test.
13. Docs atualizadas.
14. Dívidas restantes.
15. Próximo passo recomendado.

Critérios de aceite:

* Módulo PaymentsPix compila.
* `dotnet test` passa.
* `POST /api/payments/pix/orders/{orderId}` cria ou retorna PixPayment Pending.
* Não chama gateway real.
* Não gera QR real.
* Não marca Order como Paid.
* Não confirma estoque.
* Não implementa webhook real.
* Não quebra Catalog, Inventory, CartCheckout ou Orders.
* Docs deixam claro que é MVP/fake provider.
