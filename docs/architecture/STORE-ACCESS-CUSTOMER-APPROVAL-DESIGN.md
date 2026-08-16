# STORE-ACCESS / CUSTOMER-APPROVAL — Design técnico (ADR)

> Status: **design only** — sem implementação de código, migrations, endpoints ou UI neste documento.  
> Data: 2026-08-16  
> Cliente atual: assessoria de compras / lojistas e revendedores (Vip Assessoria).  
> Relacionado: `docs/security/SEC-005-customer-identity-backend.md`, `docs/security/FE-SEC-003-admin-auth-separation.md`, `docs/cart-checkout.md`, `docs/orders.md`.

Este ADR **substitui**, para o cliente atual, a regra fixa de produto “checkout convidado permitido e prioritário” (`docs/prompts/00-project-context.md`). A capacidade técnica de convidado **não é apagada**; fica atrás de configuração.

---

## 1. Objetivo

Documentar a decisão de:

1. Tornar a loja **configurável** entre aberta e fechada.
2. Implementar **aprovação administrativa** de clientes.
3. **Desabilitar checkout convidado** para o cliente atual.
4. **Unificar a experiência de login** na vitrine **sem fundir** a arquitetura interna admin/customer.

Não implementar neste documento.

---

## 2. Contexto atual (código)

| Área | Estado hoje |
|------|-------------|
| Vitrine / catálogo | Público (`GET /api/catalog/products`, etc.) |
| Carrinho | `localStorage` no browser; sem API de carrinho |
| Checkout / pedido | `POST /api/checkout/sessions` e `POST /api/orders/from-checkout-session` **anônimos**; cookie customer é opcional |
| Guest token | `guestAccessToken` one-shot + `GET /api/orders/public/{orderNumber}` (EMAIL-001/002) |
| Customer auth | `/api/auth/customer/*`, cookie `CustomerCookie`, role `Customer` |
| Admin auth | `/api/auth/admin/*`, cookie `Identity.Application`, `IsStaff` |
| Cadastro customer | Cria `IsActive=true`, **não** loga automaticamente, login **sem** exigir e-mail confirmado |
| `ShopflowUser` | `IsStaff`, `IsActive`, `EmailConfirmed` — **não** há status de aprovação comercial |
| Login FE | `/login` (customer) e `/admin/login` (staff) — fluxos e cookies separados (`FE-SEC-003`) |

O modelo de negócio do cliente é **B2B (lojista/revendedor)**, não varejo aberto ao consumidor final. Pedido guest e catálogo público não combinam com “só quem a operação conhece e aprova compra”.

---

## 3. Decisão (resumo)

| Tema | Escolha |
|------|---------|
| Modo da loja | Configuração **`StoreAccess:Mode`** = `Open` \| `Closed` |
| Cliente atual (TESTE/HML/PROD deste tenant) | **`Closed`** |
| Checkout convidado | Flag **`Checkout:AllowGuest`** = **`false`** neste cliente; código guest **permanece** |
| Quem compra | Customer **aprovado** + cookie `CustomerCookie` |
| Aprovação | Campo próprio no usuário customer — **não** reutilizar `EmailConfirmed` nem `IsStaff` |
| Catálogo em `Closed` | Exige customer autenticado **e aprovado** |
| Login UX | Uma experiência de “entrar na loja” na vitrine; admin continua em `/admin/login` |
| Arquitetura interna | **Não fundir** schemes, cookies, policies, contexts React nem endpoints |

---

## 4. Modo da loja

### 4.1 Contratos

`Open` (capacidade futura / outro tenant):

- Catálogo público.
- Checkout guest permitido **somente se** `Checkout:AllowGuest=true`.
- Login/cadastro opcionais.

`Closed` (cliente atual):

- Páginas/APIs públicas da loja: home institucional (se existir), `/login`, `/register`, `/forgot-password`, `/reset-password`, `/confirm-email`.
- Catálogo, PDP, carrinho e checkout exigem sessão customer **aprovada**.
- Staff **não** usa a vitrine com cookie admin; opera só em `/admin/*`.

O frontend precisa de um contrato anônimo estável, por exemplo:

```text
GET /api/store/access
```

Resposta sugerida (sem secrets):

```json
{
  "mode": "Closed",
  "allowGuestCheckout": false,
  "requireApprovedCustomerToBrowse": true
}
```

A API de catálogo/checkout **não confia** só no FE: em `Closed`, requests sem customer aprovado recebem **401** com código estável (`STORE_ACCESS_REQUIRED` / `CUSTOMER_APPROVAL_REQUIRED`), sem enumerar usuários.

### 4.2 Onde vive a config

MVP: **configuração de ambiente** (`StoreAccess__Mode`, `Checkout__AllowGuest`). Um tenant, um cliente.

Admin UI para ligar/desligar a loja = evolução (tabela de settings). Não misturar com feature flags de pagamento/Brevo.

---

## 5. Aprovação administrativa de clientes

### 5.1 Por que um campo novo

| Campo existente | Papel | Por que não basta |
|-----------------|-------|-------------------|
| `EmailConfirmed` | Prova de posse do e-mail | Admin pode aprovar um lojista conhecido antes/depois do confirm |
| `IsActive` | Conta operacionalmente ligada/desligada | Já usado em login; misturar “pendente” com “desativado” perde auditoria |
| `IsStaff` | Backoffice | Nunca vira customer |

**Recomendação:** `CustomerApprovalStatus` só para `!IsStaff`:

| Status | Significado | Login vitrine | Comprar |
|--------|-------------|----------------|---------|
| `Pending` | Cadastrou; espera o admin | Senha correta → mensagem de **pendência** (não o 401 genérico de credencial) | Não |
| `Approved` | Liberado para a loja | Sim, se `IsActive` | Sim |
| `Rejected` | Recusado | Credencial genérica / conta indisponível | Não |
| `Suspended` | Aprovado no passado, cortado depois | Não | Não |

`IsActive=false` permanece como corte operacional (perda de senha em massa, fraude, etc.).

Cadastro: status inicial **`Pending`**. Continua **sem** login automático (já é o MVP atual).

### 5.2 Login

Ordem sugerida depois de localizar o usuário customer:

1. Inexistente / staff / sem role `Customer` → mensagem **genérica** (não enumerar).
2. Password falhou → genérica + lockout atual (5 / 15 min).
3. Password ok + `Pending` → **“Cadastro em análise.”** (o próprio usuário já sabe que existe).
4. Password ok + `Rejected` / `Suspended` / `!IsActive` → conta indisponível (opaca).
5. `Approved` + `IsActive` → emite `CustomerCookie`.

Não exigir `EmailConfirmed` no login neste ADR (comportamento atual). Pode-se exigir confirmação **antes de aprovar**, como política do admin, não como regra dura de Identity.

### 5.3 Admin

Novos endpoints **Backoffice** (não reutilizar listagem de Orders):

- listar customers com filtro de status (`Pending` primeiro);
- detalhe mínimo: nome, e-mail, telefone, data de cadastro, status, e-mail confirmado;
- `approve` / `reject` / `suspend` (mutação + CSRF + policy `Backoffice`).

Auditoria mínima: quem aprovou, quando, nota interna opcional (só admin).

E-mail transacional opcional pós-aprovação (“sua conta foi liberada”) — **fora do MVP deste ADR**; pode reusar outbox EMAIL-001 numa intent nova depois.

Não devolver `guestAccessToken`, hashes nem dados de staff nesta listagem.

---

## 6. Checkout convidado desligado (cliente atual)

### 6.1 Decisão

Para este cliente: **`Checkout:AllowGuest=false`**.

Efeitos:

- `POST /api/checkout/sessions` exige policy `Customer` + status `Approved`.
- `POST /api/orders/from-checkout-session` exige o mesmo; `CustomerUserId` **obrigatório** (não mais opcional).
- FE: remove o atalho de convidado (`GuestCheckoutNotice` deixa de ser “pode continuar sem conta”). Checkout redireciona para `/login?redirect=...`.
- Pedidos novos **não** emitem `guestAccessToken`. E-mails de pedido usam `/account/orders/{orderId}`.

### 6.2 O que não apagar

Infra guest **permanece no código** (token hash, `GET /api/orders/public/{n}`, EMAIL-002 `?t=`):

- pedidos guest **já existentes** em TESTE;
- possível `Open` + `AllowGuest=true` noutro tenant/futuro.

Não reprocessar outbox antiga nem reemitir token.

### 6.3 Claim

`create-account` / `claim` de pedido guest continuam válidos para **pedidos históricos**. Pedidos novos já nascem vinculados. Não é o fluxo feliz do cliente atual.

---

## 7. Experiência de login unificada ≠ arquitetura unificada

### 7.1 Unificar (UX)

Quando a loja está `Closed`, a vitrine **entra pela conta**:

- Header: Entrar / Criar conta (não “continuar como convidado”).
- `/login` e `/register` visualmente da **loja**, não do admin.
- Pós-cadastro: tela de “aguardando aprovação”, não a home logada.
- Customer aprovado: `/account/*` e checkout como hoje, com cookie customer.
- Visitante em `/cart` ou `/checkout` → `/login?redirect=`.

### 7.2 Não fundir (arquitetura)

Permanecem **dois mundos** (`SEC-005` / `FE-SEC-003`):

| | Customer | Admin |
|--|----------|--------|
| Rota de login | `/login` | `/admin/login` |
| API | `/api/auth/customer/*` | `/api/auth/admin/*` |
| Cookie | `CustomerCookie` | `Identity.Application` |
| Policy | `Customer` | `Backoffice` |
| Context FE | `AuthContext` | `AdminAuthContext` |
| Quem é | `role Customer` + `!IsStaff` | `IsStaff` |

Regras duras:

- Staff **não** autentica em `/login` da loja (já bloqueado).
- Customer **não** autentica em `/admin/login`.
- Logout de um cookie não derruba o outro.
- CSRF e schemes continuam separados.
- **Não** criar um único `POST /api/auth/login` “inteligente” que escolhe o papel.

“Tela única” no sentido de **um só formulário que loga admin ou cliente** está **rejeitada**. Unificar é o *produto loja*, não o backoffice.

---

## 8. Impacto por módulo

| Módulo | Mudança prevista na implementação futura |
|--------|------------------------------------------|
| IdentityAccess | `CustomerApprovalStatus` + register `Pending` + login gated + admin approve/reject/suspend + `GET /api/store/access` |
| HttpApi | Policy/filtro de loja fechada em catálogo público, checkout e create-order |
| Catalog (leitura pública) | Em `Closed`, exigir customer aprovado **ou** endpoint de vitrine autenticado |
| CartCheckout | Recusar sessão anônima se `AllowGuest=false` |
| Orders | Recusar create-order anônimo; não emitir guest token nesse modo |
| Notifications | Pedidos logados → link `/account/orders/{id}`; guest link só se o pedido tiver token |
| Frontend | Guard de vitrine; checkout exige login; fila de aprovação no admin; `GET /store/access` no boot |
| Cypress | Specs de guest checkout deixam de ser o caminho feliz deste cliente |

Carrinho `localStorage` pode continuar no browser; **converter em pedido** exige login aprovado. Em `Closed`, a PDP nem deveria popular o carrinho sem sessão — o guard de rota resolve na UX; o backend resolve na API.

---

## 9. Segurança

- Não enumerar e-mails no register/login (exceto pendência **depois** de senha válida).
- Aprovar/rejeitar só `Backoffice` + CSRF.
- Cookie customer HttpOnly; sem JWT na vitrine.
- Não logar senha, token de e-mail, `guestAccessToken`, ApiKey Brevo.
- Rate limits atuais de register/login permanecem.
- Admin seed (`SHOPFLOW_ADMIN_*`) nunca vira customer aprovado por engano.

---

## 10. Alternativas rejeitadas

| Alternativa | Motivo |
|-------------|--------|
| Apagar guest token / rotas public | Quebra pedidos já criados e EMAIL-002 |
| Usar só `EmailConfirmed` como “aprovado” | Confunde posse de e-mail com crédito comercial |
| Usar só `IsActive=false` no cadastro | Não distingue pendente, recusado e suspenso |
| Fundir cookies/schemes admin+customer | Viola SEC-005 / FE-SEC-003; risco de staff na vitrine e customer no backoffice |
| Um único endpoint de login | Superfície de enumeração e vazamento de papel |
| Loja fechada só no FE | Catálogo/checkout anônimos continuariam na API |
| `AllowGuest` hardcoded `false` no código | Impede `Open` futuro; a decisão é **config por ambiente/tenant** |

---

## 11. Ordem de implementação (quando houver prompt de código)

Sem estimar calendário. Ordem técnica:

1. **Config** `StoreAccess:Mode` + `Checkout:AllowGuest` + `GET /api/store/access`.
2. **Desligar guest** no create-session / create-order quando `AllowGuest=false` (backend primeiro; FE em seguida).
3. **Approval** no `ShopflowUser` + register `Pending` + login gated + admin list/approve.
4. **Closed** nas APIs de catálogo.
5. **FE:** guards da vitrine, checkout autenticado, tela “em análise”, admin de aprovação.
6. TESTE: cadastro → pendente → admin aprova → login → checkout Pix **sem** convidado.

Cada passo precisa ser observável sozinho (flag). Não misturar com merge de identity.

---

## 12. Fora de escopo

- Implementação neste PR/documento.
- Multi-tenant SaaS (vários clientes Shopflow com modos diferentes no mesmo processo).
- Login social, 2FA, magic link.
- Fundir `Identity.Application` e `CustomerCookie`.
- Reemissão de `guestAccessToken`.
- Aprovação por CNPJ/documento (pode ser fase seguinte no cadastro).
- E-mail “conta aprovada” (outbox) — desejável depois, não bloqueia o modelo.
- HML/PROD deploy.

---

## 13. Referências

- `docs/security/SEC-005-customer-identity-backend.md`
- `docs/security/FE-SEC-003-admin-auth-separation.md`
- `docs/cart-checkout.md`
- `docs/orders.md`
- `docs/features/EMAIL-001-transactional-email-outbox-brevo.md`
- `docs/features/EMAIL-002-guest-order-link-validation.md` (se presente na branch)
- `docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md` (perfil lojista/revendedor)
