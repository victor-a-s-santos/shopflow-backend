# Template Cursor — Implementação Backend

Você está atuando como engenheiro backend sênior do Shopflow implementando um módulo ou feature **server-side**.

**Leia antes de codar:**

- `docs/prompts/00-project-context.md`
- `docs/ai-context/shopflow-current-state.md`
- Código existente do módulo mais próximo (ex.: `CartCheckout` como referência de integração cross-module)
- Doc do módulo: `docs/catalog.md`, `docs/inventory.md`, `docs/cart-checkout.md`

---

## Stack e padrões obrigatórios

| Padrão | Uso |
|--------|-----|
| Clean Architecture | Domain / Application / Infrastructure por módulo |
| DDD | Entidades ricas, invariantes no domain |
| CQRS + MediatR | Commands, Queries, Handlers |
| FluentValidation | Validators + `ValidationBehavior` pipeline |
| EF Core | DbContext por módulo, schema PostgreSQL dedicado |
| Minimal APIs | Endpoints em `HttpApi/Endpoints/` — sem Controllers MVC |
| Migrations | EF migrations; aplicadas no startup (padrão atual) |
| Erros HTTP | Middleware global — 400/404/409 JSON |
| Testes | Unit (domain/handlers) + Integration (Postgres) |

---

## Regras

1. **Analisar padrões existentes** — copiar estrutura de Catalog/Inventory/CartCheckout.
2. **Não quebrar módulos existentes** — regressão = `dotnet test` verde.
3. **Não implementar fora do escopo** do prompt.
4. **Produto sempre com SKU** · **estoque por skuId**.
5. **Checkout convidado** — API não deve exigir `customerId` autenticado na sessão (usar dados inline).
6. **Preço no backend** — nunca confiar no preço enviado pelo frontend como fonte de verdade.
7. **Reserva de estoque** — via Inventory module services, não SQL ad hoc duplicado.
8. **OpenAPI/Scalar** — endpoints documentados via tags.
9. **Sem domain events dispatch** até infra existir — seguir padrão atual (exceptions + UoW).

---

## Estrutura de entrega

```
src/Modules/<Module>/
  Domain/
  Application/
    CommandHandlers/
    QueryHandlers/
    Validators/
  Infrastructure/
    Persistence/
    Migrations/
ApiGateways/Vls.Shopflow.HttpApi/Endpoints/<Module>Endpoints.cs
tests/Vls.Shopflow.<Module>.UnitTests/
tests/Vls.Shopflow.<Module>.IntegrationTests/
docs/<module>.md (atualizar)
```

---

## Checklist

### Implementação

- [ ] Domain entities + value objects
- [ ] Commands/Queries + Handlers
- [ ] Validators FluentValidation
- [ ] Repositories / Read models
- [ ] DbContext + migration
- [ ] DI extension (`Add<Module>ModuleFromConfig`)
- [ ] Endpoints Minimal API
- [ ] Integração cross-module (se necessário)
- [ ] Registro no `Program.cs`

### Testes

```bash
cd apps/api && dotnet test
```

- [ ] Unit tests — invariantes domain + handlers happy/edge path
- [ ] Integration tests — Postgres (`SHOPFLOW_TEST_DB` ou localhost)
- [ ] Concorrência (se estoque/reserva)

### Documentação

- [ ] `docs/<module>.md` — endpoints, payloads, erros
- [ ] `docs/ai-context/shopflow-current-state.md`
- [ ] `docs/ai-context/next-actions.md` (se prioridade mudar)
- [ ] Scalar verificável em dev

### Frontend (se integração faz parte do escopo)

- [ ] Service TypeScript
- [ ] Tipos alinhados ao contrato
- [ ] Cursor revisão + Cypress

---

## Erros HTTP — padrão existente

| Código | Quando |
|--------|--------|
| 400 | ValidationException |
| 404 | NotFoundException |
| 409 | ConflictException (estoque, status inválido) |

---

## Escopo desta implementação

<!-- PREENCHER -->

### Módulo

…

### Endpoints

…

### Integrações

…

### Fora do escopo

…

---

## Critérios de aceite

- [ ] `dotnet test` passa
- [ ] Migrations aplicam cleanly
- [ ] Endpoints testáveis via Scalar/Postman
- [ ] Módulos existentes intactos
- [ ] Documentação atualizada

---

## Próximo módulo típico (referência)

Após CartCheckout frontend integrado → **Orders** (converter sessão em pedido).

Use `01-feature-template.md` para planejar antes de implementar.
