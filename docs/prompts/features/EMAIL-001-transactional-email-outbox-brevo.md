Você está atuando como engenheiro backend/.NET sênior no Shopflow.

Implemente a feature:

docs/features/EMAIL-001-transactional-email-outbox-brevo.md

com base nas duas análises arquiteturais anteriores.

DECISÃO ARQUITETURAL FECHADA:

- Não usar RabbitMQ.
- Não usar MassTransit.
- Não usar TransactionScope.
- Não compartilhar DbConnection/DbTransaction entre DbContexts como solução principal.
- Manter PostgreSQL Email Outbox.
- Manter Brevo HTTP provider existente.
- Manter PendingCheckoutExpirationWorker.
- Manter MercadoPagoPixReconciliationWorker.
- Adaptar EmailOutboxWorker.
- Identity confirm/reset permanece pós-commit/best-effort.
- Ciclo de pedido usa uma intenção durável em Orders.

==================================================
1. CRIAR O EMAIL-001
==================================================

Criar:

docs/features/EMAIL-001-transactional-email-outbox-brevo.md

Documentar:

- arquitetura atual
- problema de enqueue pós-commit
- decisão orders.email_intents
- EmailOutbox
- Brevo
- idempotência
- locking
- retries
- reclaim
- self-healing
- rollout TESTE → HML → PROD
- RabbitMQ/MassTransit explicitamente adiados

==================================================
2. ORDER EMAIL INTENT
==================================================

Criar no módulo Orders uma entidade/tabela durável:

orders.email_intents

Modelo sugerido, adaptando aos padrões reais:

Id
OrderId
Type
IdempotencyKey
PayloadJson
Status
CreatedAt
DispatchedAt

Status mínimo:

Pending
Dispatched

Garantias:

- IdempotencyKey UNIQUE.
- índice em Status + CreatedAt.
- intent criada no MESMO OrdersDbContext.
- intent persistida no MESMO SaveChanges do fato de negócio.
- não chamar NotificationsDbContext nessa transação.
- não armazenar HTML renderizado.
- armazenar payload mínimo necessário.
- não armazenar secrets.
- GuestAccessToken pode existir no PayloadJson de Created porque é necessário para o e-mail, mas:
  - nunca logá-lo;
  - nunca retornar em endpoints administrativos;
  - documentar implicação de segurança/backups.

Types iniciais:

OrderCreated
PaymentConfirmed
OrderShipped
OrderDelivered

==================================================
3. ORDER CREATED
==================================================

Alterar CreateOrderFromCheckoutSessionCommandHandler.

Antes do SaveChanges que cria o Order:

- criar OrderCreated EmailIntent.
- usar:
  order:{OrderId:D}:created

Persistir payload necessário para renderizar o e-mail posteriormente, incluindo o raw GuestAccessToken quando necessário.

Order + intent devem entrar no mesmo SaveChanges.

Se o SaveChanges falhar:

- nem Order nem intent podem permanecer.

Remover o risco de depender exclusivamente de NotifyOrderCreatedAsync pós-commit.

Não enviar e-mail diretamente neste handler.

==================================================
4. PAYMENT CONFIRMED
==================================================

Este é o fluxo P0.

Alterar o fluxo:

MercadoPagoPixPaidTransitionService
OrderPaidWriter

Quando o Order passa para Paid:

- criar PaymentConfirmed EmailIntent no OrdersDbContext antes do mesmo SaveChanges.
- key:
  order:{OrderId:D}:paid

O estado Order.Paid + intent devem ser atômicos.

IMPORTANTE:

Hoje AlreadyPaid não recria a notificação.

Corrigir o fluxo para que:

Order Status = Paid
+
não existe intent/outbox correspondente
→ possa ser reparado de forma idempotente.

Não depender exclusivamente de MarkedPaid para garantir o e-mail.

Webhook + reconciliation não podem gerar intents duplicadas.

IdempotencyKey unique é a proteção final.

Não tentar tornar Inventory + Order + Pix + e-mail uma única transação nesta feature.

Documentar o problema preexistente de Order/Pix/Inventory multi-commit como fora do escopo EMAIL-001.

==================================================
5. SHIPPED / DELIVERED
==================================================

Alterar:

OrderFulfillmentCommandHandlers
DeliveryBatchCommandHandlers

Quando o estado é persistido:

- criar intent no mesmo OrdersDbContext/SaveChanges.

Keys:

order:{OrderId:D}:shipped
order:{OrderId:D}:delivered

Batch:

- uma intent por Order.
- unique key evita duplicação.
- todos persistidos no mesmo SaveChanges correspondente à alteração do batch quando tecnicamente compatível com o fluxo real.

==================================================
6. INTENT DISPATCHER
==================================================

Adicionar processamento no Worker existente, sem criar processo/container novo.

Responsabilidade:

orders.email_intents Pending
        ↓
converter para chamada do EmailNotificationService
        ↓
notifications.email_outbox
        ↓
marcar intent Dispatched

Requisitos:

- batch configurável.
- processamento idempotente.
- se email_outbox já possui IdempotencyKey:
  considerar dispatch realizado.
- unique violation de IdempotencyKey no outbox deve ser considerada sucesso/idempotência, não erro fatal.
- só marcar intent Dispatched depois de confirmar que existe row correspondente em notifications.email_outbox.
- falha no NotificationsDbContext deixa intent Pending.
- retry em execução futura.
- não renderizar/enviar pela Brevo no dispatcher de intent.
- EmailOutbox continua sendo responsável pelo provider.

Adicionar configuração somente se necessário, por exemplo:

OrderEmailIntentDispatcher__Enabled=true
OrderEmailIntentDispatcher__IntervalSeconds=...
OrderEmailIntentDispatcher__BatchSize=...

Não criar flags excessivas.

==================================================
7. HARDENING EMAIL OUTBOX
==================================================

Corrigir EmailOutboxRepository / Processor.

A) Claim concorrente

Usar PostgreSQL:

FOR UPDATE SKIP LOCKED

ou implementação equivalente segura.

Objetivo:

duas instâncias não processam a mesma row Pending.

B) Processing órfão

Hoje crash após MarkProcessing pode perder mensagem.

Implementar lease/reclaim.

Preferência:

ProcessingStartedAt

ou solução equivalente clara.

Se mensagem permanece Processing por mais de timeout configurável:

→ voltar a ser elegível/retry.

Config possível:

EmailOutbox__ProcessingTimeoutSeconds

C) Unique violation

Se enqueue encontra violação de IdempotencyKey porque outra execução inseriu primeiro:

→ considerar sucesso.

Não logar como falha operacional.

D) Retry

Manter política atual quando adequada:

- timeout / 429 / 5xx = transitório
- 4xx permanente = Failed
- MaxAttempts limitado
- sem retry infinito

==================================================
8. BREVO
==================================================

Não reimplementar provider.

Reutilizar:

ITransactionalEmailSender
BrevoTransactionalEmailSender

Config existente:

Brevo__Enabled
Brevo__BaseUrl
Brevo__ApiKey
Brevo__SenderName
Brevo__SenderEmail
Brevo__ReplyToEmail
Brevo__SandboxMode
Brevo__TimeoutSeconds

Não adicionar Email__Provider enquanto existe só Brevo.

Não colocar API key real em arquivos versionados.

==================================================
9. TEMPLATES
==================================================

Manter templates no código Shopflow.

Reusar/adaptar TransactionalEmailTemplates.

Criar ou validar templates:

OrderCreated
PaymentConfirmed
OrderShipped
OrderDelivered

Não usar Brevo TemplateId agora.

==================================================
10. IDENTITY
==================================================

Não migrar Identity para orders.email_intents.

Manter:

CustomerRegistrationService
CustomerPasswordService
OutboxIdentityEmailSender

como pós-commit/best-effort.

ForgotPassword:

- API deve continuar resposta genérica.
- falha no enqueue não pode expor existência do usuário.
- remover/evitar logs contendo password reset token.

ConfirmEmail:

- manter comportamento atual.
- não criar transaction sharing nesta feature.

==================================================
11. SELF-HEALING
==================================================

Implementar apenas se necessário após intents.

Regras:

Paid:
Order Paid + intent ausente
→ criar intent idempotente se for possível reconstruir payload.

Created:
não tentar reconstruir raw GuestAccessToken se intent nunca existiu.
A intent criada na transação é a garantia.

Shipped/Delivered:
podem ser reconciliados pelo estado do Order + key.

Não criar um worker genérico excessivamente complexo.

Se implementar reconciliation:

- batch limitado.
- key unique.
- logs estruturados.
- configurável.

==================================================
12. OBSERVABILIDADE
==================================================

Logs estruturados devem usar quando disponíveis:

IntentId
OutboxId
IdempotencyKey
OrderId
EmailType
Attempt
Provider
ProviderMessageId
Duration
Result

Não logar:

Brevo ApiKey
password reset token
guest access token
HTML completo
senha
cookie

==================================================
13. MIGRATION
==================================================

Criar migration Orders para orders.email_intents.

Possível migration Notifications para ProcessingStartedAt, se escolhido.

Não criar tabelas RabbitMQ/MassTransit.

Não alterar schemas desnecessariamente.

==================================================
14. TESTES OBRIGATÓRIOS
==================================================

Adicionar testes para:

1.
Create Order SaveChanges falha:
- Order não existe
- intent não existe

2.
Create Order commit:
- Order existe
- exatamente uma intent created
- payload contém os dados necessários

3.
Dispatcher com Notifications indisponível:
- intent permanece Pending

4.
Dispatcher reexecutado:
- uma row email_outbox apenas

5.
PaymentConfirmed:
- Order Paid + uma intent
- webhook + reconciliation → uma intent

6.
AlreadyPaid:
- notificação ausente pode ser reparada de forma idempotente

7.
Shipped:
- exatamente uma intent

8.
Delivered:
- exatamente uma intent

9.
Batch:
- uma intent por Order

10.
SKIP LOCKED:
- dois processors não reivindicam a mesma mensagem

11.
Processing órfão:
- mensagem volta a processamento após timeout

12.
Unique violation:
- considerada sucesso/idempotência

13.
Forgot password:
- resposta continua genérica mesmo com falha no enqueue

14.
Nenhum teste/log expõe GuestAccessToken ou password reset token.

==================================================
15. BUILD/TEST
==================================================

Executar:

dotnet build
dotnet test

Reportar quantidade de testes e falhas.

Não fazer deploy.

==================================================
16. DOCUMENTAÇÃO DE DEPLOY
==================================================

Atualizar documentação existente com novas configurações, migrations e validação.

Preparar TESTE para:

Brevo__Enabled=true
Brevo__ApiKey=<secret somente na VPS>
Brevo__SenderName=Vip Assessoria Digital
Brevo__SenderEmail=no-reply@vipassessoriadigital.com.br
Brevo__BaseUrl=https://api.brevo.com
Brevo__SandboxMode=true inicialmente

Não alterar .env real.

==================================================
17. FORA DE ESCOPO
==================================================

Não implementar:

- RabbitMQ
- MassTransit
- SMTP
- Mercado Pago novo
- mudança de VPS
- produção
- transaction compartilhada entre DbContexts
- TransactionScope
- event bus genérico
- domain events genéricos
- analytics
- CRM
- ERP

==================================================
18. SAÍDA ESPERADA
==================================================

Ao final entregar:

1. Resumo executivo.
2. Arquivos alterados.
3. Migration criada.
4. Schema de orders.email_intents.
5. Fluxo OrderCreated.
6. Fluxo PaymentConfirmed.
7. Fluxo Shipped/Delivered.
8. Funcionamento do dispatcher.
9. Hardening do EmailOutbox.
10. Estratégia de idempotência.
11. Configurações novas.
12. Testes executados.
13. Riscos/pendências.
14. Passo a passo para deploy apenas em TESTE.
15. Checklist de validação com Brevo Sandbox.
16. Rollback recomendado.

Não execute deploy.