# EMAIL-001 — E-mail transacional (intents + outbox + Brevo)

Documentação da feature implementada. RabbitMQ, MassTransit e `TransactionScope` **não** entram neste desenho e ficam adiados.

## Arquitetura atual

```
Pedido / pagamento / fulfillment
        │
        │  mesmo OrdersDbContext.SaveChanges
        ▼
orders.email_intents (Pending)
        │
        │  Worker: OrderEmailIntentDispatcherWorker
        ▼
IEmailNotificationService (render template no código)
        │
        ▼
notifications.email_outbox (Pending)
        │
        │  Worker: EmailOutboxWorker
        ▼
Brevo HTTP POST /v3/smtp/email
```

Identity (confirm/reset) **não** usa `orders.email_intents`. Continua pós-commit/best-effort: `OutboxIdentityEmailSender` → outbox.

Aprovação de cadastro também é pós-commit/best-effort: `OutboxCustomerAccessNotifier` → outbox. Ver `docs/customer/customer-approval-emails.md`.

## Problema que esta feature corrige

O enqueue de e-mail de pedido era pós-commit, em `NotificationsDbContext` separado. Se o commit do pedido/pagamento/fulfillment passasse e o enqueue falhasse (ou `AlreadyPaid` pulasse a notificação), o e-mail podia ser perdido sem registro durável no bounded context de Orders.

Não há transação compartilhada entre `OrdersDbContext` e `NotificationsDbContext` (nem `TransactionScope`). A garantia de Orders é a intent; a garantia de envio é o outbox.

## `orders.email_intents`

| Coluna | Papel |
|--------|--------|
| `Id` | PK |
| `OrderId` | Pedido |
| `Type` | `OrderCreated` / `PaymentConfirmed` / `OrderShipped` / `OrderDelivered` |
| `IdempotencyKey` | UNIQUE: `order:{OrderId:D}:created\|paid\|shipped\|delivered` |
| `PayloadJson` | Dados mínimos para o template (não HTML) |
| `Status` | `Pending` / `Dispatched` |
| `CreatedAt` / `DispatchedAt` | Auditoria |

Índices: unique em `IdempotencyKey`; `(Status, CreatedAt)`; `(OrderId, Type)`.

Regras:

- Intent criada no **mesmo** `OrdersDbContext` e persistida no **mesmo** `SaveChanges` do fato de negócio.
- Não chama Notifications nessa transação.
- Guest access token raw pode existir só no payload de `OrderCreated` (necessário para o link do e-mail). Nunca logar, nunca devolver em endpoints admin. Implicação: backups de Postgres podem conter o token; tratar como dado sensível.
- `OrderCreated` **não** é reconstruído depois: se a intent nunca existiu, o raw token não pode ser recriado.

## Fluxos

### OrderCreated

`CreateOrderFromCheckoutSessionCommandHandler` adiciona o pedido (e o hash do guest token) e a intent `OrderCreated` **antes** de `SaveChanges`. Falha no commit → nem pedido nem intent.

### PaymentConfirmed (P0)

`OrderPaidWriter.MarkAsPaidAsync` cria intent `PaymentConfirmed` no mesmo `SaveChanges` que marca `Paid`.

`AlreadyPaid` também chama `EnsurePendingAsync` (reparo idempotente). Webhook e reconciliação Mercado Pago reutilizam a mesma key; unique evita duplicata.

**Fora de escopo:** Inventory + Order + Pix + e-mail **não** viram uma transação única. O problema preexistente de multi-commit entre esses módulos permanece.

### Shipped / Delivered

Handlers individuais e de remessa (`DeliveryBatch`) criam **uma intent por pedido** no mesmo `SaveChanges` da mudança de estado.

## Dispatcher

Hosted service no **Worker existente** (`OrderEmailIntentDispatcherWorker`), sem container novo.

1. Reparo limitado: `Paid` / `Shipped` / `Delivered` sem intent correspondente (não repara `Created`).
2. `FOR UPDATE SKIP LOCKED` nas intents `Pending`.
3. Se o outbox já tem a `IdempotencyKey`, marca `Dispatched`.
4. Senão chama `IEmailNotificationService` (enqueue). Unique violation no outbox = sucesso.
5. Só marca `Dispatched` depois de confirmar a row no outbox.
6. Falha em Notifications deixa a intent `Pending` para retry.
7. Não fala com a Brevo. Quem envia é o EmailOutbox.

## EmailOutbox (hardening)

- Claim: `FOR UPDATE SKIP LOCKED`.
- Órfãos `Processing`: `ProcessingStartedAt` + `EmailOutbox__ProcessingTimeoutSeconds` (default 120) voltam a ser elegíveis.
- Unique de `IdempotencyKey`: insert concorrente é idempotente (não é falha operacional).
- Retry: timeout / 429 / 5xx transitório; 4xx permanente; `MaxAttempts` limitado.
- Configuração Brevo ausente ou `Brevo__Enabled=false`: a mensagem volta para `Pending` **sem** consumir `Attempts` (`ReleaseForConfigurationRetry`). Não usa `Skipped` — em produção o e-mail precisa permanecer recuperável quando a chave ainda não está no ambiente.
- `EmailOutbox__Enabled=false`: o worker não claima; rows ficam `Pending`.
- `Skipped` permanece no domínio para um “não enviar” explícito; o processor não o usa para falta de config.

## Brevo

Provider HTTP existente (`BrevoTransactionalEmailSender`). Sem `Email__Provider` enquanto só existe Brevo. Sem TemplateId. API key só em secret/VPS, nunca em git.

## Identity

Confirm/reset permanecem pós-commit. Forgot password sempre responde mensagem genérica; falha de enqueue não revela se o e-mail existe; tokens não são logados.

## Rollout

TESTE (Brevo sandbox) → HML → PROD. Este documento não autoriza deploy. Ver comandos de TESTE no final da implementação / runbooks existentes.

RabbitMQ / MassTransit: **adiados**. Outbox Postgres + intents cobrem a durabilidade necessária agora.

## Configuração

```
EmailOutbox__ProcessingTimeoutSeconds=120
OrderEmailIntentDispatcher__Enabled=true
OrderEmailIntentDispatcher__IntervalSeconds=15
OrderEmailIntentDispatcher__BatchSize=20
```

Mais: `docs/integrations/brevo-transactional-emails.md`, `docs/orders/order-emails.md`.
