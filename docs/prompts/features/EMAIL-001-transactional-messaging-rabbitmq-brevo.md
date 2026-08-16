Você está atuando como **arquiteto de software / engenheiro .NET sênior** no projeto Shopflow Backend.

## Objetivo

Realizar uma **análise arquitetural completa, sem alterar código**, para definir como introduzir:

* RabbitMQ
* MassTransit
* Transactional Outbox
* processamento assíncrono orientado a eventos
* envio de e-mails transacionais via Brevo

A análise deve partir da implementação REAL existente no repositório.

**Não implemente nada nesta etapa.**
**Não crie migrations.**
**Não altere Docker Compose.**
**Não instale packages.**
**Não faça deploy.**
**Não altere arquivos.**

Queremos primeiro entender o que já existe e produzir um plano seguro de evolução.

---

# 1. Contexto atual

O Shopflow é um backend .NET modular para e-commerce.

Temos módulos como:

* Catalog
* Inventory
* CheckoutSession
* Orders
* PaymentsPix
* IdentityAccess/Customer
* demais BuildingBlocks existentes

Infra atual:

* .NET
* PostgreSQL
* Docker Compose
* Caddy
* VPS
* Cloudflare
* Cloudflare R2
* GitHub Actions
* ambientes TESTE e HML
* produção ainda será criada separadamente

Workers conhecidos atualmente:

* `EmailOutboxWorker`
* `MercadoPagoPixReconciliationWorker`
* `PendingCheckoutExpirationWorker`

Antes de implementar Brevo decidimos avaliar uma evolução para:

```text
Application transaction
        │
        ├── alteração de negócio
        │
        └── evento/outbox
                │
                ▼
        Transactional Outbox
                │
                ▼
            RabbitMQ
                │
                ▼
          MassTransit Consumer
                │
                ▼
             Brevo API
```

RabbitMQ seria inicialmente single-node dentro da infraestrutura Docker, sem exposição pública.

MassTransit seria a abstração .NET de mensageria.

IMPORTANTE:

Não assumimos que precisamos criar tudo do zero.

O projeto já possui `EmailOutboxWorker` e pode já possuir:

* tabela de outbox
* abstrações
* domain events
* retries
* background services
* mecanismos de idempotência

A análise deve descobrir exatamente o que existe.

---

# 2. Mapear o EmailOutboxWorker atual

Localize e analise completamente:

`EmailOutboxWorker`

Informe:

* projeto/assembly onde está
* como é registrado no DI
* como é iniciado
* frequência de execução
* se usa `BackgroundService`
* se usa `PeriodicTimer`
* se faz polling
* quais tabelas consulta
* qual DbContext utiliza
* como seleciona mensagens pendentes
* tamanho do batch
* ordenação
* locking/concurrency
* status possíveis
* retry atual
* backoff atual
* limite de tentativas
* tratamento de exceção
* tratamento de restart/crash
* como marca mensagem como processada
* se existe dead-letter equivalente
* se existe idempotência
* se pode ocorrer envio duplicado
* se múltiplas instâncias do worker seriam seguras

Desenhe o fluxo atual:

```text
evento de negócio
    ↓
?
    ↓
EmailOutbox
    ↓
EmailOutboxWorker
    ↓
?
```

Não presuma que o fluxo funciona de determinada maneira: valide no código.

---

# 3. Mapear o modelo/tabela de Email Outbox

Localize todas as entidades/tabelas relacionadas a:

* EmailOutbox
* OutboxMessage
* EmailMessage
* Notification
* TransactionalEmail
* nomes equivalentes

Documente o schema existente.

Exemplo do que queremos saber:

```text
Id
Type
Recipient
Payload
Status
Attempts
CreatedAt
NextAttemptAt
ProcessedAt
LastError
ProviderMessageId
```

Mas use os nomes REAIS encontrados.

Determine também:

* qual módulo é dono da tabela
* qual DbContext é responsável
* quais índices existem
* quais constraints existem
* se payload é JSON
* se template é persistido
* se destinatário é persistido
* se existe correlação com Order/Payment/Customer

---

# 4. Verificar atomicidade do Outbox atual

Este é um dos pontos mais importantes.

Determine se hoje ocorre algo como:

```text
BEGIN TRANSACTION

INSERT Order
INSERT EmailOutboxMessage

COMMIT
```

ou se acontece:

```text
INSERT Order
COMMIT

depois

INSERT EmailOutboxMessage
```

Precisamos saber se a criação da mensagem é realmente transacional com a alteração de negócio.

Analise especialmente:

* criação de pedido
* alteração de status
* pagamento
* customer
* password reset

Informe explicitamente:

**O Outbox atual é realmente Transactional Outbox? SIM/NÃO/PARCIAL.**

Explique com evidências do código.

---

# 5. Mapear Domain Events / Integration Events

Procure no projeto inteiro por:

* DomainEvent
* IntegrationEvent
* EventHandler
* Notification
* MediatR `INotification`
* `Publish`
* `IPublisher`
* event dispatcher
* nomes equivalentes

Mapeie a infraestrutura existente.

Precisamos descobrir se já temos eventos como:

```text
OrderCreated
OrderStatusChanged
PaymentConfirmed
PaymentFailed
PaymentExpired
CheckoutExpired
CustomerRegistered
PasswordResetRequested
```

Não invente eventos.

Liste somente os existentes e indique os que não existem, mas seriam necessários.

Para cada evento existente informe:

* onde nasce
* quem publica
* quando publica
* quem consome
* se ocorre dentro ou fora da transaction
* se é domain event ou integration event
* se atualmente dispara side effects

---

# 6. Mapear Orders

Analise os principais fluxos de Orders relacionados a notificações.

Especialmente:

* criação do pedido
* mudança de status
* cancelamento
* conclusão
* vínculo com customer
* pedido guest

Determine onde seria seguro gerar eventos como:

```text
OrderCreated
OrderCancelled
OrderStatusChanged
```

Também determine quais dados seriam necessários para e-mail sem fazer o consumer depender excessivamente do banco.

Exemplo:

```text
OrderId
OrderNumber
CustomerEmail
CustomerName
Total
Status
```

Mas não defina contrato final ainda.

---

# 7. Mapear PaymentsPix

Analise:

* criação do Pix
* confirmação
* expiração
* reconciliação
* provider Fake atual
* estrutura prevista para Mercado Pago
* `MercadoPagoPixReconciliationWorker`

Determine:

* como o status de pagamento muda
* onde PaymentConfirmed efetivamente acontece
* se mais de um fluxo pode confirmar o mesmo pagamento
* como evitar dois e-mails de pagamento confirmado
* como idempotência é tratada atualmente

Também avaliar arquitetura futura:

```text
Mercado Pago
      │
    webhook
      ↓
Shopflow API
      ↓
Payment status
      ↓
PaymentConfirmed integration event
      ↓
RabbitMQ
```

com:

`MercadoPagoPixReconciliationWorker`

permanecendo apenas como mecanismo de reconciliação/fallback.

Não implementar webhook agora.

---

# 8. Mapear PendingCheckoutExpirationWorker

Analise:

`PendingCheckoutExpirationWorker`

Informe:

* polling interval
* queries
* batch
* locking
* transações
* idempotência
* concorrência
* o que acontece quando checkout expira
* quais outros módulos são afetados

Depois responda:

**Existe benefício REAL em migrar este worker para RabbitMQ agora?**

Considere que expiração é baseada em tempo (`ExpiresAt`) e não necessariamente em um evento recebido.

Nossa hipótese inicial é mantê-lo como scheduled worker/polling.

Valide ou conteste essa hipótese com base no código.

---

# 9. Mapear MercadoPagoPixReconciliationWorker

Faça a mesma análise.

Determine se ele deve permanecer:

```text
Scheduled reconciliation worker
```

mesmo depois da introdução do RabbitMQ.

Avalie a arquitetura futura:

```text
Webhook = fluxo principal

ReconciliationWorker = fallback / consistência eventual
```

Informe se a implementação atual é compatível com essa evolução.

---

# 10. Mapear recuperação de senha

Analise IdentityAccess/Customer/Admin.

Descubra como funciona atualmente:

* forgot password
* geração de token
* reset
* envio ou simulação de envio
* resposta da API
* proteção contra enumeração de usuários

Determine como encaixar:

```text
PasswordResetRequested
        ↓
Transactional Outbox
        ↓
RabbitMQ
        ↓
SendTransactionalEmailConsumer
        ↓
Brevo
```

Sem expor token em logs.

---

# 11. Verificar se existe abstração de e-mail

Procure por:

* IEmailSender
* IEmailService
* INotificationSender
* SMTP
* SendGrid
* Brevo
* fake email
* console email
* nomes equivalentes

Informe o que pode ser reaproveitado.

Não queremos:

```text
Orders → Brevo
Payments → Brevo
Identity → Brevo
```

Queremos uma fronteira semelhante a:

```text
Business modules
       ↓
events/messages
       ↓
Notifications/Messaging
       ↓
ITransactionalEmailSender
       ↓
BrevoTransactionalEmailSender
```

Avalie como encaixar isso na arquitetura REAL existente.

---

# 12. Avaliar MassTransit

Verifique se MassTransit já existe como dependência.

Se não existir, avalie onde deveria ser registrado.

Não instalar.

Avalie uso de:

* MassTransit
* MassTransit.RabbitMQ
* MassTransit.EntityFrameworkCore

Verifique versões compatíveis com a versão .NET/EF Core atualmente utilizada pelo projeto.

Não altere `.csproj`.

Determine quais projetos deveriam referenciar quais packages.

Evite espalhar dependência de MassTransit pelos módulos de domínio.

---

# 13. Avaliar Transactional Outbox do MassTransit

Compare duas alternativas.

## Alternativa A

Manter o Outbox atual e criar dispatcher próprio:

```text
DB Outbox
   ↓
custom dispatcher
   ↓
RabbitMQ
```

## Alternativa B

Migrar/adaptar para MassTransit Transactional Outbox:

```text
EF transaction
   ↓
MassTransit Outbox
   ↓
RabbitMQ
```

Compare:

* quantidade de código
* risco de migração
* confiabilidade
* idempotência
* retries
* manutenção
* acoplamento
* compatibilidade com os DbContexts atuais
* necessidade de migrations
* impacto nos módulos

Dê uma recomendação objetiva para o Shopflow.

---

# 14. RabbitMQ no Docker

Analise `deploy/docker-compose.yml`.

Não altere.

Proponha como seria:

```text
rabbitmq:
  image: ...
  volumes:
  healthcheck:
  networks:
```

Requisitos:

* single node
* persistência
* sem porta `5672` pública
* sem `15672` pública
* comunicação somente pela rede Docker
* usuário/senha via `.env`
* healthcheck
* restart policy
* limites razoáveis para VPS de 4 GB

Avalie o consumo atual aproximado dos serviços configurados e risco de colocar RabbitMQ na VPS atual TESTE/HML.

Não executar comandos na VPS.

---

# 15. Topologia RabbitMQ proposta

Proponha topologia mínima.

Não criar dezenas de filas.

Começar, se possível, com algo semelhante a:

```text
SendTransactionalEmail
        ↓
send-transactional-email
```

ou topologia equivalente gerenciada pelo MassTransit.

Explique:

* exchange
* queue
* routing
* retry
* error queue
* acknowledgement
* durability
* persistence

Evite arquitetura excessivamente sofisticada.

---

# 16. Contratos de mensagens

Proponha quais integration messages realmente precisamos para o MVP.

Prioridade inicial:

P0:

* pedido criado
* pagamento confirmado
* recuperação de senha

Possíveis posteriores:

* pedido cancelado
* pedido enviado/status
* customer registrado

Avalie duas abordagens:

A:

```text
OrderCreated
PaymentConfirmed
PasswordResetRequested
```

consumidores transformam eventos em e-mails.

B:

```text
SendTransactionalEmail
```

os módulos já produzem uma solicitação de e-mail.

Explique vantagens/desvantagens.

Queremos evitar transformar RabbitMQ apenas em uma "fila SMTP".

Prefira eventos de negócio quando fizer sentido, mas não force eventos onde isso aumentaria complexidade desnecessariamente.

---

# 17. Idempotência

Analise profundamente.

Precisamos evitar:

```text
PaymentConfirmed
PaymentConfirmed
```

causando dois e-mails.

Proponha estratégia para:

* MessageId
* CorrelationId
* EventId
* OrderId
* PaymentId
* consumer idempotency
* Inbox/Consumer Outbox, se apropriado
* duplicate delivery
* retry após timeout da Brevo

Explique particularmente o caso:

```text
Brevo recebeu e enviou
↓
resposta HTTP se perdeu
↓
consumer considera falha
↓
RabbitMQ redelivery
```

Como reduzir risco de e-mail duplicado?

Informe claramente quais garantias são possíveis e quais não são.

---

# 18. Retry / error queue

Proponha política inicial simples.

Exemplo conceitual:

```text
tentativa imediata
30s
2min
10min
30min
→ error/dead-letter
```

Mas adapte às capacidades reais de MassTransit/RabbitMQ.

Diferencie:

* erro transitório Brevo 429/5xx
* timeout
* erro permanente 400
* endereço inválido
* template inexistente

Não fazer retry infinito.

---

# 19. Brevo

A conta Brevo já está criada.

Domínio:

`vipassessoriadigital.com.br`

já está autenticado.

Remetente:

`Vip Assessoria Digital <no-reply@vipassessoriadigital.com.br>`

já está criado.

API Key de TESTE já foi criada, mas NÃO está no repositório.

Proponha configuração:

```text
Email__Provider=Brevo
Email__FromName=Vip Assessoria Digital
Email__FromAddress=no-reply@vipassessoriadigital.com.br
Email__Brevo__ApiKey=...
Email__EnvironmentPrefix=[TESTE]
```

ou estrutura melhor se fizer sentido.

Secrets reais somente no `.env.test` da VPS.

Nunca imprimir API key.

Avalie uso da API HTTP da Brevo, não SMTP.

---

# 20. Templates

Avalie onde os templates deveriam viver.

Compare:

A. Templates dentro do Shopflow

B. Templates gerenciados na Brevo por TemplateId

C. híbrido

Para o MVP, recomende uma abordagem considerando:

* versionamento
* facilidade de edição
* deploy
* personalização
* dependência do provider
* teste automatizado

---

# 21. Observabilidade

Proponha logs estruturados contendo:

```text
MessageId
CorrelationId
MessageType
RecipientDomain
Provider
Attempt
ProviderMessageId
Duration
Result
```

Não logar:

* password reset token
* API key
* conteúdo sensível
* dados pessoais desnecessários

Avalie métricas futuras:

```text
emails_pending
emails_sent
emails_failed
rabbitmq_queue_depth
consumer_errors
```

Não implementar monitoramento externo agora.

---

# 22. Segurança

Verifique riscos relacionados a:

* RabbitMQ credentials
* Brevo API key
* `.env`
* logs
* Docker
* portas
* Management UI
* PII nas mensagens
* password reset token
* backups do RabbitMQ
* mensagens persistidas

RabbitMQ não deve ficar publicamente acessível.

---

# 23. Impacto no EmailOutboxWorker atual

Ao final, dê uma decisão explícita:

`MANTER`

`ADAPTAR`

ou

`REMOVER`

o `EmailOutboxWorker`.

Explique a estratégia de migração.

Não devemos deixar:

```text
EmailOutboxWorker enviando e-mail
+
RabbitMQ consumer enviando o mesmo e-mail
```

simultaneamente.

---

# 24. Impacto nos demais workers

Para cada um:

### EmailOutboxWorker

MANTER / ADAPTAR / REMOVER

### PendingCheckoutExpirationWorker

MANTER / ADAPTAR / REMOVER

### MercadoPagoPixReconciliationWorker

MANTER / ADAPTAR / REMOVER

Justifique individualmente.

---

# 25. Estratégia de rollout

Proponha rollout incremental.

Preferência inicial:

### Fase 1

RabbitMQ + MassTransit infraestrutura.

### Fase 2

Brevo provider + consumer.

### Fase 3

Primeiro fluxo de e-mail.

### Fase 4

Demais eventos.

### Fase 5

Desativação do fluxo legado.

Mas adapte conforme a implementação real encontrada.

Precisamos conseguir testar em TESTE antes de HML.

---

# 26. Feature flag / configuração

Avalie se devemos ter:

```text
Messaging__Provider=RabbitMQ
Messaging__Enabled=true

Email__Provider=Brevo
Email__Enabled=true
```

e alguma flag temporária para convivência controlada com o fluxo antigo.

Evite flags permanentes desnecessárias.

---

# 27. Produção

Não implementar produção.

A análise deve apenas garantir que a solução seja reproduzível depois em:

```text
TESTE
HML
PROD
```

RabbitMQ de produção ficará na futura VPS de produção, separado do RabbitMQ TESTE/HML.

---

# 28. Resultado esperado

Entregue um relatório técnico contendo exatamente estas seções:

## A. Arquitetura atual encontrada

Diagrama ASCII da arquitetura REAL.

## B. EmailOutboxWorker atual

Fluxo, tabela, retry, polling, riscos.

## C. Domain Events / Integration Events existentes

Tabela com evento, origem, consumidor e transação.

## D. Workers atuais

Tabela:

| Worker | Função | Polling | Idempotência | Decisão futura |
| ------ | ------ | ------- | ------------ | -------------- |

## E. Lacunas encontradas

Classificar:

* Crítico
* Alto
* Médio
* Baixo

## F. Arquitetura proposta

Diagrama ASCII incluindo:

* API
* PostgreSQL
* Transactional Outbox
* MassTransit
* RabbitMQ
* Consumer
* Brevo
* workers periódicos mantidos

## G. Decisão sobre MassTransit Outbox

Manter custom ou migrar para MassTransit, com justificativa.

## H. Contratos/eventos propostos

Somente os necessários.

## I. Estratégia de idempotência

Producer + Outbox + RabbitMQ + Consumer + Brevo.

## J. Retry/error handling

Política proposta.

## K. RabbitMQ Docker

Configuração conceitual e impacto na VPS.

## L. Brevo

Provider, configurações e templates.

## M. Estratégia de rollout

Fases incrementais.

## N. Arquivos que provavelmente serão alterados

Liste caminhos REAIS do repositório.

## O. Migrations provavelmente necessárias

Liste sem criar.

## P. Packages NuGet provavelmente necessários

Liste sem instalar.

## Q. Riscos

Especialmente duplicidade de e-mail e perda de evento.

## R. Recomendação final

Diga explicitamente se recomenda:

1. RabbitMQ + MassTransit agora;
2. somente Postgres Outbox agora;
3. outra abordagem.

## S. Plano para `EMAIL-001`

Proponha o conteúdo do futuro documento:

`docs/features/EMAIL-001-transactional-messaging-rabbitmq-brevo.md`

---

# Restrições finais

NESTA ETAPA:

* não alterar nenhum arquivo;
* não criar `EMAIL-001` ainda;
* não instalar MassTransit;
* não adicionar RabbitMQ;
* não alterar Docker;
* não criar migrations;
* não implementar Brevo;
* não remover worker;
* não fazer deploy;
* não acessar/alterar VPS;
* não alterar TESTE/HML;
* não tocar produção;
* não expor secrets.

Primeiro queremos apenas o diagnóstico arquitetural baseado no código existente.

Ao final, além do relatório, dê uma conclusão curta:

**“A implementação recomendada é pequena / média / grande”**

e explique quais são os 3 maiores pontos de risco.
