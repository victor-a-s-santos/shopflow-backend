# ADR — Store Access Mode, Customer Approval e Login Unificado

## Status

Proposto para implementação.

## Contexto

O Shopflow está evoluindo de uma loja aberta para um modelo de assessoria de compras voltado a lojistas e revendedores.

O cliente definiu que nem todo usuário deve poder comprar automaticamente. Novos usuários devem solicitar cadastro e aguardar aprovação administrativa antes de acessar a loja e realizar pedidos.

Ao mesmo tempo, o produto deve continuar preparado para um futuro modelo SaaS, onde diferentes lojas poderão ter regras diferentes de acesso:

* loja aberta;
* loja com catálogo público, mas checkout restrito;
* loja privada;
* loja B2B com aprovação manual;
* loja com compra como convidado.

Também foi levantada a possibilidade de unificar a experiência de login entre usuário comum e admin, evitando que o operador precise acessar uma URL separada como `/admin/login`.

## Decisão

O Shopflow passará a suportar uma política configurável de acesso à loja.

A loja atual usará o modo privado com aprovação administrativa.

O login visual será unificado em uma rota pública única, mas a arquitetura interna de admin e customer continuará separada nesta fase para reduzir risco técnico e preservar segurança.

## Decisões principais

### 1. StoreAccessMode configurável

Criar configuração para definir como cada loja/ambiente controla o acesso.

Modos sugeridos:

```text
PublicCatalogAndGuestCheckout
PublicCatalogLoginCheckout
PublicCatalogApprovedCheckout
PrivateCatalogApprovedOnly
```

Descrição:

```text
PublicCatalogAndGuestCheckout
- catálogo público;
- carrinho público;
- checkout como convidado permitido;
- conta opcional.

PublicCatalogLoginCheckout
- catálogo público;
- carrinho público;
- checkout exige login;
- aprovação administrativa não obrigatória.

PublicCatalogApprovedCheckout
- catálogo público;
- carrinho público;
- checkout exige cliente logado e aprovado.

PrivateCatalogApprovedOnly
- catálogo, carrinho e checkout exigem cliente logado e aprovado.
```

Para o cliente atual, o modo recomendado é:

```text
PrivateCatalogApprovedOnly
```

Com isso:

* visitante não aprovado não acessa catálogo completo;
* visitante pode acessar login, cadastro, esqueci senha e páginas públicas permitidas;
* cliente pendente não compra;
* cliente aprovado compra normalmente.

### 2. Checkout convidado configurável

O checkout convidado não será removido do código neste momento.

Ele será controlado por configuração:

```text
Checkout__AllowGuestCheckout=false
```

Para o cliente atual:

```text
Checkout__AllowGuestCheckout=false
```

Regras:

* novos pedidos como convidado ficam bloqueados;
* endpoints legados de guest tracking podem permanecer para compatibilidade;
* pedidos antigos sem CustomerUserId continuam legíveis conforme regras atuais;
* novos pedidos devem exigir cliente aprovado quando a política da loja assim determinar.

### 3. CustomerAccessStatus

Adicionar status de acesso ao cliente.

Status sugeridos:

```text
PendingApproval
Approved
Rejected
Suspended
```

Significado:

```text
PendingApproval
- usuário solicitou cadastro;
- ainda não pode comprar;
- aguarda aprovação administrativa.

Approved
- usuário liberado;
- pode acessar/comprar conforme StoreAccessMode.

Rejected
- cadastro recusado;
- não pode comprar;
- pode ser orientado a falar com a equipe.

Suspended
- usuário já aprovado, mas bloqueado posteriormente;
- não pode comprar até reativação.
```

Clientes existentes devem ser migrados como:

```text
Approved
```

para evitar quebra de operação.

### 4. Login visual unificado

Criar uma experiência única de login:

```text
/login
```

ou, se o projeto preferir rotas em português:

```text
/entrar
```

A rota `/admin/login` deve deixar de ser a tela principal de login e passar a redirecionar para o login único.

Após login, o frontend decide o destino:

```text
Admin/staff → /admin
Cliente aprovado → loja ou /account/orders
Cliente pendente → /account/pending-approval
Cliente recusado → /account/access-rejected
Cliente suspenso → /account/access-suspended
```

### 5. Manter `/admin/*` nesta fase

Apesar do login visual ser unificado, as rotas administrativas continuam sob:

```text
/admin/*
```

Motivos:

* preservar organização;
* preservar guards e policies existentes;
* reduzir risco de regressão;
* evitar colisão com rotas da loja;
* manter separação operacional clara;
* facilitar testes e segurança.

Não será feita nesta fase uma fusão total entre admin user e customer user.

### 6. Manter policies/cookies separados nesta fase

A arquitetura atual já separa:

```text
Admin/Backoffice
Customer
```

A decisão desta fase é:

```text
Unificar experiência de login, não unificar completamente a identidade interna.
```

Portanto:

* admin continua usando policy Backoffice;
* customer continua usando policy Customer;
* rotas admin continuam exigindo Backoffice;
* rotas customer continuam exigindo Customer;
* checkout passa a exigir Customer aprovado quando configurado;
* nenhuma rota admin deve aceitar customer comum;
* nenhuma regra crítica deve depender apenas do frontend.

Uma refatoração futura para SaaS poderá criar um modelo único:

```text
User
TenantMembership
Roles
StoreAccessPolicy
```

Mas isso fica fora do escopo atual.

## Regras de backend

### Cadastro

Ao registrar novo usuário customer:

```text
AccessStatus = PendingApproval
AccessRequestedAt = now
```

O cadastro não libera compra automaticamente.

O backend deve disparar evento/notificação para aprovação administrativa.

### Login/me

O endpoint de login ou `/me` deve retornar o status de acesso do cliente.

Campos esperados no DTO customer:

```text
accessStatus
accessRequestedAt
approvedAt
```

O frontend usa esses campos para redirecionamento e bloqueio visual.

### Checkout

O endpoint de criação de checkout session deve respeitar a política da loja.

Quando a loja exigir aprovação:

```text
POST /api/checkout/sessions
```

deve exigir:

```text
CustomerCookie válido
Customer AccessStatus = Approved
```

Respostas sugeridas:

```text
401 — não logado
403 — customer não aprovado
```

Codes sugeridos:

```text
CUSTOMER_LOGIN_REQUIRED
CUSTOMER_ACCESS_NOT_APPROVED
CUSTOMER_ACCESS_REJECTED
CUSTOMER_ACCESS_SUSPENDED
GUEST_CHECKOUT_DISABLED
```

### Order creation

O endpoint:

```text
POST /api/orders/from-checkout-session
```

também deve garantir que a sessão pertence a um cliente permitido.

Para novos pedidos no modo privado:

```text
Order.CustomerUserId obrigatório na prática
```

A coluna pode continuar nullable para preservar compatibilidade com pedidos antigos.

### Storefront/catalog access

Quando o modo for:

```text
PrivateCatalogApprovedOnly
```

os endpoints públicos de catálogo devem ser protegidos ou retornar somente uma resposta pública mínima, conforme decisão de implementação.

Opções:

```text
A) Proteger endpoints de catálogo com CustomerCookie + Approved.
B) Manter endpoints públicos, mas frontend bloqueia vitrine.
```

Recomendação:

```text
Proteger no backend os endpoints que expõem catálogo completo, preços e SKUs.
```

O frontend não deve ser a única barreira.

## Regras de frontend

### Cadastro

Após cadastro:

```text
Não redirecionar para checkout.
Não mostrar loja como liberada.
Mostrar tela de cadastro enviado para aprovação.
```

Copy sugerida:

```text
Recebemos seu cadastro.
Nossa equipe irá analisar suas informações.
Assim que o acesso for liberado, você receberá um aviso por e-mail.
```

CTAs:

```text
Voltar para login
Falar com vendedor pelo WhatsApp
```

### Pending approval

Criar tela:

```text
/account/pending-approval
```

Texto:

```text
Seu cadastro está em análise.
Assim que for aprovado, você poderá acessar a loja e fazer pedidos.
```

### Rejected

Criar tela ou estado:

```text
/account/access-rejected
```

Texto:

```text
Seu cadastro não foi aprovado neste momento.
Fale com nossa equipe para mais informações.
```

### Suspended

Criar tela ou estado:

```text
/account/access-suspended
```

Texto:

```text
Seu acesso está temporariamente bloqueado.
Fale com nossa equipe para mais informações.
```

### Rotas protegidas

Criar guard de acesso aprovado.

Nome sugerido:

```text
CustomerApprovedRoute
```

No modo `PrivateCatalogApprovedOnly`, proteger:

```text
/
 /product/:slug
 /cart
 /checkout
 /account/orders
 /account/orders/:id
```

Rotas públicas permitidas:

```text
/login
/register
/forgot-password
/reset-password
/account/pending-approval
/account/access-rejected
/account/access-suspended
```

### Guest checkout

Ocultar/remover da UI:

```text
continuar como convidado
criar pedido sem login
criar conta após pedido convidado
vincular pedido convidado
```

Mensagens:

```text
Para comprar, entre com uma conta aprovada.
```

ou:

```text
Solicite seu cadastro para acessar a loja.
```

## Admin approvals

Criar área administrativa para aprovações:

```text
/admin/customers/approvals
```

Funcionalidades MVP:

```text
listar cadastros pendentes
buscar por nome/e-mail/telefone
aprovar cliente
recusar cliente
visualizar data da solicitação
```

Funcionalidades recomendadas para o backend já suportar:

```text
suspender cliente
reativar cliente
registrar admin responsável
registrar data/hora da ação
registrar motivo de recusa/suspensão
```

Notificações no admin MVP:

```text
badge no menu
card no dashboard
lista de pendentes
```

Não criar notification center completo nesta fase.

## E-mails transacionais

Esta decisão impacta Brevo.

Eventos necessários:

### Admin — novo cadastro pendente

Disparado quando novo usuário se cadastra.

Assunto sugerido:

```text
Novo cadastro aguardando aprovação
```

Conteúdo:

```text
Nome
E-mail
Telefone
Data da solicitação
Link para aprovar no admin
```

### Cliente — cadastro recebido

Assunto:

```text
Recebemos sua solicitação de cadastro
```

Conteúdo:

```text
Seu cadastro foi recebido e será analisado pela equipe.
Você receberá um aviso quando o acesso for liberado.
```

### Cliente — aprovado

Assunto:

```text
Seu acesso foi aprovado
```

Conteúdo:

```text
Seu acesso à loja foi liberado.
Agora você já pode entrar e realizar pedidos.
```

### Cliente — recusado

Assunto:

```text
Atualização sobre seu cadastro
```

Conteúdo:

```text
Seu cadastro não foi aprovado neste momento.
Entre em contato com a equipe para mais informações.
```

### Cliente — suspenso

Opcional nesta fase.

## Configurações sugeridas

Backend:

```env
StoreAccess__Mode=PrivateCatalogApprovedOnly
CustomerAccess__RequireApproval=true
Checkout__AllowGuestCheckout=false
```

Possível forma mais granular futura:

```env
StorefrontAccess__RequireLoginForCatalog=true
StorefrontAccess__RequireApprovalForCatalog=true
StorefrontAccess__RequireApprovalForCheckout=true
Checkout__AllowGuestCheckout=false
```

Recomendação para esta fase:

```env
StoreAccess__Mode=PrivateCatalogApprovedOnly
Checkout__AllowGuestCheckout=false
CustomerAccess__RequireApproval=true
```

## Compatibilidade com funcionalidades existentes

### Orders

Novos pedidos devem ter `CustomerUserId`.

Pedidos antigos guest continuam existindo para histórico/compatibilidade.

### Guest tracking

Não remover neste momento.

Classificação:

```text
legacy compatibility
```

Pode permanecer para pedidos antigos.

### Guest access token

Manter infraestrutura atual, mas não gerar para novos pedidos quando guest checkout estiver desligado.

### DeliveryBatch

Fica mais confiável, pois pedidos novos usam `CustomerUserId`.

Fallback por guest email+phone pode permanecer para legado.

### WhatsApp

Continua útil para:

```text
usuário pendente falar com vendedor
cliente aprovado falar sobre pedido
cliente recusado/suspenso entrar em contato
```

### R2

Sem impacto.

### Brevo

Passa a ser requisito para aprovação de cadastro e comunicação com admin/cliente.

## Não objetivos desta ADR

Não implementar agora:

```text
unificação total de admin/customer identity
remoção completa de /admin/*
remoção completa de guest tracking
notification center completo
multi-tenant SaaS completo
permissões avançadas por equipe
roles complexas
chat nativo
WhatsApp Business API
```

## Riscos

### Risco 1 — quebrar checkout existente

Mitigação:

```text
usar feature flags
manter compatibilidade com pedidos antigos
testar checkout aprovado/não aprovado/guest
```

### Risco 2 — vazamento de catálogo privado

Mitigação:

```text
bloquear no backend endpoints que expõem catálogo completo quando StoreAccessMode exigir aprovação
não depender apenas do frontend
```

### Risco 3 — refatoração grande de auth

Mitigação:

```text
não fundir cookies/policies nesta fase
unificar só a UI de login
manter Backoffice e Customer separados internamente
```

### Risco 4 — admin não perceber novo cadastro

Mitigação:

```text
badge no menu
card no dashboard
lista de aprovações
e-mail para admin via Brevo
```

## Plano de implementação

### Fase 1 — Backend Customer Approval

```text
StoreAccessMode config
CustomerAccessStatus
migration
register cria PendingApproval
login/me retorna status
checkout exige Approved conforme config
order creation reforça customer aprovado
admin endpoints approve/reject/suspend/reactivate
pending approvals count/list
ProblemDetails/codes
tests
docs
```

### Fase 2 — Frontend Customer Approval

```text
login único
/admin/login redirect
register success pending
pending/rejected/suspended pages
CustomerApprovedRoute
bloqueio de catálogo/cart/checkout conforme mode
admin approvals page
badge/dashboard
esconder guest checkout
tests
docs
```

### Fase 3 — Brevo Approval Emails

```text
admin novo cadastro
cliente cadastro recebido
cliente aprovado
cliente recusado
integração com outbox/eventos
templates PT-BR
tests
docs
```

### Fase 4 — QA/Smoke

```text
cadastro novo PendingApproval
admin vê notificação
admin recebe e-mail
pendente não acessa/compra
admin aprova
aprovado compra
guest não compra
rejected/suspended não compra
rotas admin protegidas
catálogo privado protegido
```

## Critérios de aceite

A feature será considerada pronta quando:

```text
1. StoreAccessMode existe e controla a política da loja.
2. Novo cadastro entra como PendingApproval.
3. Admin vê cadastros pendentes.
4. Admin consegue aprovar cliente.
5. Admin consegue recusar cliente.
6. Cliente pendente não compra.
7. Cliente recusado não compra.
8. Cliente suspenso não compra.
9. Cliente aprovado compra.
10. Checkout convidado fica bloqueado quando AllowGuestCheckout=false.
11. Novos pedidos no modo privado possuem CustomerUserId.
12. Catálogo privado não vaza via API quando StoreAccessMode exigir aprovação.
13. Login visual é unificado.
14. /admin/login redireciona para login único.
15. /admin/* continua protegido por Backoffice.
16. Customer/Backoffice continuam seguros e isolados internamente.
17. E-mail/notificação de aprovação fica planejado para Brevo.
18. Testes backend/frontend cobrem os fluxos críticos.
```

## Decisão final

O Shopflow adotará uma política configurável de acesso à loja.

Para o cliente atual, a loja operará como B2B privada com aprovação manual:

```text
StoreAccess__Mode=PrivateCatalogApprovedOnly
CustomerAccess__RequireApproval=true
Checkout__AllowGuestCheckout=false
```

O login visual será unificado, mas a arquitetura interna de admin e customer permanecerá separada nesta fase.

As rotas `/admin/*` serão mantidas para segurança e organização.

O checkout convidado será desabilitado para novos pedidos, mas a infraestrutura de guest tracking poderá permanecer para compatibilidade com pedidos legados.

A aprovação administrativa de clientes será implementada antes da versão de produção.
