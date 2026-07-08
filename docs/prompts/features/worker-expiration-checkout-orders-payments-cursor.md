Você está atuando como engenheiro backend sênior do projeto Shopflow.

Objetivo:
Implementar um Worker de Expiração para sessões de checkout, pedidos pendentes e pagamentos Pix pendentes, liberando reservas de estoque quando o cliente não pagar dentro do prazo.

Contexto atual:

* Catalog está implementado.
* Inventory está implementado.
* CartCheckout está implementado:

  * cria CheckoutSession;
  * reserva estoque;
  * retorna status Pending;
  * possui cancelamento de sessão;
  * não possui worker de expiração.
* Orders está implementado:

  * cria Order a partir da CheckoutSession;
  * Order nasce PendingPayment;
  * Order não é marcada como Paid ainda.
* PaymentsPix MVP está implementado:

  * cria PixPayment Pending;
  * provider Fake;
  * gateway real Mercado Pago ainda não integrado;
  * não marca Order como Paid;
  * não confirma estoque.
* Frontend já cria:

  * CheckoutSession;
  * Order PendingPayment;
  * PixPayment Pending/Fake.
* Ainda não existe webhook real de pagamento.
* Ainda não existe provider Mercado Pago real.
* A prioridade agora é evitar reservas presas e deixar o ciclo de expiração preparado.

Importante:

* Não implementar Mercado Pago agora.
* Não implementar webhook real agora.
* Não implementar QR real agora.
* Não implementar frontend agora.
* Não implementar IdentityCustomer agora.
* Não implementar Shipping agora.
* Não fingir pagamento.
* Não marcar Order como Paid.
* O foco é expirar/cancelar fluxos pendentes e liberar estoque reservado.

==================================================

1. LEITURA OBRIGATÓRIA
   ==================================================

Antes de implementar, leia:

* docs/prompts/00-project-context.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/cart-checkout.md
* docs/orders.md
* docs/payments-pix.md
* módulos:

  * Inventory
  * CartCheckout
  * Orders
  * PaymentsPix
* padrões existentes de:

  * DI
  * Hosted Services / Workers, se já houver
  * EF Core
  * MediatR
  * UnitOfWork
  * exceptions
  * testes

Não inventar arquitetura nova sem necessidade.
Seguir os padrões existentes.

==================================================
2. OBJETIVO FUNCIONAL
=====================

Implementar um processo automático para:

1. Encontrar CheckoutSessions pendentes expiradas.
2. Encontrar Orders PendingPayment associadas a sessões/pagamentos expirados.
3. Encontrar PixPayments Pending expirados.
4. Cancelar/expirar esses registros.
5. Cancelar a reserva de estoque correspondente no Inventory.
6. Evitar processamento duplicado ou concorrente.
7. Registrar logs claros.

Fluxo desejado:

CheckoutSession Pending + ExpiresAt vencido
→ CheckoutSession Expired/Canceled
→ Order PendingPayment vinculada vira Expired ou Canceled
→ PixPayment Pending vinculado vira Expired
→ Inventory reservation cancelada

Se não houver relacionamento direto suficiente entre CheckoutSession, Order e PixPayment, criar os serviços/leitores necessários com o menor acoplamento possível e documentar a decisão.

==================================================
3. ESCOPO
=========

Implementar:

* Worker/HostedService ou serviço executável periódico.
* Configuração de intervalo.
* Configuração de batch size.
* Expiração de CheckoutSession.
* Expiração/cancelamento de Order PendingPayment.
* Expiração de PixPayment Pending.
* Cancelamento da reserva de estoque no Inventory.
* Logs.
* Testes unitários.
* Testes de integração, se viável.
* Documentação.

Sugestão de nomes:

* ExpirationWorker
* CheckoutExpirationWorker
* PendingCheckoutExpirationWorker
* ExpirePendingCheckoutsJob
* OrderPaymentExpirationService

Escolha um nome alinhado ao padrão do projeto.

==================================================
4. FORA DO ESCOPO
=================

Não implementar agora:

* Mercado Pago provider real.
* Webhook de pagamento real.
* QR Code real.
* Pix copia e cola real.
* Endpoint manual de simulate-paid.
* Tela frontend.
* Admin Orders.
* IdentityCustomer.
* Shipping.
* Notificações.
* Retentativa de pagamento.
* Recriação automática de Pix.
* Estorno.
* Reembolso.

==================================================
5. DECISÕES DE STATUS
=====================

Verificar enums existentes antes de alterar.

Status esperados:

CheckoutSession:

* Pending
* Expired ou Canceled, conforme já existir

Order:

* PendingPayment
* Expired ou Canceled, conforme já existir

PixPayment:

* Pending
* Expired

Regras:

* Apenas registros Pending/PendingPayment devem expirar.
* Paid nunca deve ser expirado.
* Canceled nunca deve ser reprocessado.
* Expired nunca deve ser reprocessado.
* Se PixPayment estiver Paid no futuro, Order não deve expirar.
* Nesta etapa, como não há pagamento real, PixPayment Pending vencido deve virar Expired.

Se algum status não existir, adicionar com cuidado e atualizar migrations/testes/docs.

==================================================
6. POLÍTICA DE EXPIRAÇÃO
========================

Definir configuração por appsettings/env:

Sugestões:

ExpirationWorker__Enabled=true
ExpirationWorker__IntervalSeconds=60
ExpirationWorker__BatchSize=50
ExpirationWorker__CheckoutSessionTtlMinutes=15
ExpirationWorker__PixPaymentTtlMinutes=15

Regras:

* Em Development/Staging pode rodar a cada 60s.
* Em teste automatizado deve poder ser desabilitado.
* Worker deve ser idempotente.
* Worker deve processar em batches.
* Worker não deve travar a aplicação se uma expiração falhar.
* Worker deve logar falhas por item e continuar.

Se CheckoutSession/PixPayment já tiverem `ExpiresAt`, usar esse campo.
Se não tiverem, usar CreatedAt + configuração TTL.

Não criar TTL hardcoded espalhado no código.

==================================================
7. CANCELAMENTO DA RESERVA DE ESTOQUE
=====================================

A reserva de estoque foi criada no CartCheckout.

O worker deve liberar a reserva usando o fluxo correto existente do Inventory.

Verificar:

* Existe ReservationId na CheckoutSession?
* Existe vínculo entre CheckoutSession e StockReservation?
* Existe serviço para CancelReservation?
* Existe command/handler `CancelStockReservationCommand`?

Regras:

* Não manipular SQL bruto do Inventory se já existe command/service.
* Não dar baixa em estoque.
* Não remover movimento histórico.
* Apenas cancelar reserva pendente.
* Se a reserva já estiver cancelada/confirmada/expirada, tratar idempotentemente.
* Logar quando a reserva não puder ser cancelada.

Se hoje CartCheckout não guarda ReservationId suficiente, avaliar adicionar esse vínculo de forma mínima e segura.
Não fazer gambiarra silenciosa.

==================================================
8. INTEGRAÇÃO ENTRE MÓDULOS
===========================

Evitar acoplamento indevido.

Pode criar portas/interfaces como:

* ICheckoutExpirationRepository
* IOrderExpirationRepository
* IPixPaymentExpirationRepository
* IInventoryReservationCanceller

Ou usar Application services existentes, conforme padrão do projeto.

A orquestração pode ficar em um módulo Worker/BackgroundTasks ou em HttpApi, mas idealmente com separação clara.

Decisão esperada:

* Worker no host HttpApi como BackgroundService.
* Lógica de expiração em Application service testável.
* Repositórios em Infrastructure.

Documentar a escolha.

==================================================
9. TRANSAÇÃO / CONSISTÊNCIA
===========================

A expiração envolve múltiplos módulos.

Regras:

* Ser idempotente.
* Processar item por item.
* Evitar transação distribuída complexa.
* Preferir consistência eventual com logs claros.
* Se falhar ao cancelar reserva, não marcar tudo como expirado sem registrar claramente.
* Se marcar sessão expirada e falhar order/payment, próxima execução deve conseguir completar.
* Evitar duplicidade com filtros por status.

Estratégia recomendada:

* Buscar candidatos vencidos.
* Para cada candidato:

  1. cancelar reserva se existir;
  2. marcar CheckoutSession Expired/Canceled;
  3. marcar Order Expired/Canceled, se existir;
  4. marcar PixPayment Expired, se existir;
  5. salvar alterações;
  6. logar sucesso.

Se for tecnicamente melhor mudar a ordem, documentar.

==================================================
10. COMMANDS / SERVICES
=======================

Criar services/commands necessários.

Sugestão:

ExpirePendingCheckoutsCommand
ExpirePendingCheckoutsCommandHandler

ou Application service:

IExpirationProcessor
ExpirationProcessor

Resultado esperado do processamento:

{
"processed": 10,
"expiredCheckoutSessions": 10,
"expiredOrders": 8,
"expiredPixPayments": 8,
"canceledReservations": 10,
"failures": 0
}

Esse resultado pode ser interno para logs/testes.

==================================================
11. ENDPOINT MANUAL OPCIONAL
============================

Opcional, se fizer sentido para teste/homologação:

POST /api/admin/maintenance/expire-pending-checkouts

Mas só implemente se:

* houver padrão de endpoint admin/maintenance;
* ficar claramente protegido ou documentado como não expor publicamente;
* não criar risco em produção.

Minha recomendação:
Não implementar endpoint manual agora, a menos que facilite muito os testes.
Priorizar worker + Application service testável.

==================================================
12. TESTES
==========

Criar testes unitários para:

* expira CheckoutSession Pending vencida;
* não expira CheckoutSession não vencida;
* não expira Order Paid;
* expira Order PendingPayment associada;
* expira PixPayment Pending vencido;
* não expira PixPayment Paid;
* cancela reserva de estoque;
* idempotência: rodar duas vezes não duplica efeitos;
* erro em um item não interrompe batch inteiro.

Criar testes de integração, se viável:

* criar fluxo completo:
  CheckoutSession + Order + PixPayment Pending
  avançar tempo / setar ExpiresAt vencido
  executar processor
  verificar:

  * CheckoutSession expirou;
  * Order expirou/cancelou;
  * PixPayment expirou;
  * reserva foi cancelada;
  * estoque disponível voltou.

Se for difícil testar BackgroundService, teste o Application service diretamente.

Executar:

dotnet build
dotnet test

==================================================
13. LOGS
========

Adicionar logs:

* worker iniciado;
* worker desabilitado;
* batch iniciado;
* quantidade de candidatos;
* item expirado com sucesso;
* falha por item;
* resumo do batch.

Não logar dados sensíveis desnecessários.
Pode logar IDs técnicos.

==================================================
14. CONFIGURAÇÃO
================

Adicionar configuração em appsettings.Development.json e/ou documentação:

ExpirationWorker:
Enabled: true
IntervalSeconds: 60
BatchSize: 50
CheckoutSessionTtlMinutes: 15
PixPaymentTtlMinutes: 15

Em testes, garantir que o worker pode ficar desligado para não interferir.

==================================================
15. DOCUMENTAÇÃO
================

Atualizar/criar:

* docs/expiration-worker.md
* docs/cart-checkout.md
* docs/orders.md
* docs/payments-pix.md
* docs/ai-context/shopflow-current-state.md
* docs/ai-context/next-actions.md
* docs/ai-context/technical-debt.md
* docs/testing.md, se necessário

Documentar:

* worker existe;
* quais status ele expira;
* TTL configurável;
* como reserva é liberada;
* limitações;
* que pagamento real/webhook Mercado Pago ainda não existe;
* que Paid não deve ser expirado;
* que worker é idempotente;
* como desabilitar em ambiente/teste.

==================================================
16. BUILD E TESTES
==================

Executar:

dotnet build
dotnet test

Não mexer no frontend.

Se algum teste pré-existente quebrar, corrigir causa real.
Não pular testes sem justificativa.

==================================================
17. RESULTADO ESPERADO
======================

Ao final, retorne:

1. Arquivos criados/alterados.
2. Onde o worker foi registrado.
3. Configurações adicionadas.
4. Estratégia de expiração.
5. Como identifica candidatos vencidos.
6. Como cancela reserva de estoque.
7. Como atualiza CheckoutSession.
8. Como atualiza Order.
9. Como atualiza PixPayment.
10. Como garante idempotência.
11. Testes criados.
12. Resultado de dotnet build/test.
13. Docs atualizadas.
14. Limitações conhecidas.
15. Próximo passo recomendado.

Critérios de aceite:

* Worker compila.
* Worker pode ser habilitado/desabilitado por config.
* Worker expira CheckoutSession pendente vencida.
* Worker expira Order PendingPayment associada.
* Worker expira PixPayment Pending associado.
* Worker cancela reserva de estoque.
* Worker não altera Order Paid.
* Worker não altera PixPayment Paid.
* Worker é idempotente.
* dotnet test passa.
* Docs refletem o estado real.
