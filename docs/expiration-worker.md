# Worker de Expiração (Checkout / Orders / PaymentsPix)

Processo em background que expira fluxos pendentes de pagamento e libera reservas de estoque quando o cliente não conclui o pagamento no prazo.

## Onde roda

| Componente | Caminho |
|------------|---------|
| Host | `apps/api/Workers/Vls.Shopflow.Worker` |
| BackgroundService | `PendingCheckoutExpirationWorker` |
| Lógica testável | `ExpirationProcessor` em `Modules/Expiration/Vls.Shopflow.Expiration.Application` |
| Leitura cross-schema (recovery) | `ExpirationRecoveryReader` em `Modules/Expiration/Vls.Shopflow.Expiration.Infrastructure` |
| Docker | serviço `worker` no `docker-compose.yml` (`Dockerfile.worker`) |

O worker **não** roda dentro do `HttpApi`. É um processo separado que compartilha o mesmo PostgreSQL.

## Configuração

Seção `ExpirationWorker` (appsettings ou variáveis de ambiente):

| Chave | Padrão | Descrição |
|-------|--------|-----------|
| `Enabled` | `true` | `false` desliga o loop (útil em testes locais) |
| `IntervalSeconds` | `60` | Intervalo entre batches |
| `BatchSize` | `50` | Máximo de candidatos por fase |
| `CheckoutSessionTtlMinutes` | `15` | Documentação/referência; sessão usa `ReservationExpiresAt` definido na criação (15 min) |
| `PixPaymentTtlMinutes` | `15` | Fallback quando `PixPayment.ExpiresAt` é null |

Exemplo em Docker:

```
ExpirationWorker__Enabled=true
ExpirationWorker__IntervalSeconds=60
ExpirationWorker__BatchSize=50
```

## O que expira

| Entidade | Status de origem | Status final | Critério de vencimento |
|----------|------------------|--------------|------------------------|
| `CheckoutSession` | `Pending` | `Expired` | `ReservationExpiresAt <= now` |
| `Order` | `PendingPayment` | `Expired` | Vinculada a sessão/Pix expirado |
| `PixPayment` | `Pending` | `Expired` | `ExpiresAt <= now` ou `CreatedAt + PixPaymentTtlMinutes` |

**Nunca altera:** `Paid`, `Canceled`, registros já `Expired`.

## Estratégia de processamento (3 fases)

1. **Sessões expiradas** — busca `CheckoutSession` `Pending` com `ReservationExpiresAt` vencido; cancela reservas dos itens; marca sessão `Expired`; expira `Order`/`PixPayment` associados.
2. **Pix expirados** — busca `PixPayment` `Pending` vencido; se a sessão ainda estiver `Pending`, expira sessão + reservas; expira pedido e pagamento.
3. **Pedidos órfãos (recovery)** — `Order` `PendingPayment` cuja `CheckoutSession` já está `Expired`/`Canceled`, mas o pedido ainda não foi expirado (consistência eventual após falha parcial).

Cada item é processado individualmente. Falha em um item incrementa `Failures` e o batch continua.

## Cancelamento de reserva de estoque

- Usa `IInventoryReservationService.CancelReservationAsync` (mesmo fluxo do cancelamento manual de sessão).
- `InventoryReservationId` está em cada `CheckoutSessionItem`.
- Reserva já cancelada ou inexistente: log de warning, tratamento idempotente (`InvalidStockReservationStatusException`, `StockReservationNotFoundException`).
- **Não** confirma venda nem remove movimentos históricos.

## Idempotência

- Métodos `Expire()` nos aggregates ignoram transições inválidas.
- Queries de batch filtram por status `Pending` / `PendingPayment`.
- Reprocessar o mesmo registro não duplica efeitos.

## Logs

- Worker iniciado / desabilitado / parado
- Início de batch com quantidade de candidatos
- Sucesso por sessão, pedido e Pix (`Information`)
- Falhas por item (`Error`)
- Resumo do batch (contagens e falhas)

## Testes

| Projeto | Cobertura |
|---------|-----------|
| `Vls.Shopflow.Expiration.UnitTests` | `Expire()` no domain; `ExpirationProcessor` com mocks |
| `Vls.Shopflow.Expiration.IntegrationTests` | Fluxo completo sessão + pedido + Pix + liberação de estoque (PostgreSQL) |

## Limitações (etapa atual)

- Sem gateway Pix real, webhook ou confirmação de pagamento.
- Worker não marca `Order` como `Paid` nem confirma reserva (venda).
- Sem endpoint admin manual de expiração (`POST /api/admin/maintenance/...` não implementado).
- Consistência eventual entre schemas (sem transação distribuída).
- `CheckoutSessionTtlMinutes` na config não reescreve `ReservationExpiresAt` de sessões já criadas.

## Próximo passo recomendado

Webhook Pix real → marcar `PixPayment`/`Order` como `Paid` e **confirmar** reserva de estoque (`ConfirmReservationAsync`), em vez de cancelar.
