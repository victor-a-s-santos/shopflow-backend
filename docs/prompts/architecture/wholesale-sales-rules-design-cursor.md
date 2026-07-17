Você está atuando como arquiteto de software sênior do projeto Shopflow, especialista em e-commerce, atacado/B2B, DDD, Clean Architecture, .NET 10, PostgreSQL, React, catálogo, estoque, carrinho, checkout e regras comerciais.

Este prompt é de DESIGN TÉCNICO / ARQUITETURA.

NÃO implementar código agora.
NÃO criar migrations agora.
NÃO alterar frontend agora.
NÃO alterar backend agora.
NÃO criar endpoints agora.

Objetivo:
Analisar e desenhar o modelo de regras de venda para atacado/pacotes/múltiplos no Shopflow, considerando impacto em catálogo, SKUs, estoque, vitrine, carrinho, checkout, admin product form e validações backend.

Contexto atual do Shopflow:
O Shopflow já possui:

- Catálogo com produtos, SKUs/variações, atributos, imagens.
- Admin de produtos com variantes, imagens, atributos, preços e status.
- Estoque por SKU.
- Reservas de estoque no checkout.
- Carrinho frontend baseado em skuId.
- Checkout guest e checkout logado.
- Orders e PaymentsPix.
- Admin Orders.
- Customer Orders.
- Worker de reconciliação Mercado Pago.
- Validações recentes de:
  - SKU Code;
  - preço;
  - atributos custom;
  - imagens;
  - estoque disponível;
  - baixa de estoque.

Novo requisito de negócio:
O e-commerce também será usado para vendas de atacado.

Precisamos suportar cenários como:

1. Venda unitária normal:
   - cliente pode comprar 1, 2, 3, 4...

2. Venda a partir de quantidade mínima:
   - exemplo: só vende a partir de 3 peças;
   - cliente pode comprar 3, 4, 5, 6...

3. Venda por múltiplos:
   - exemplo: só vende de 3 em 3;
   - cliente pode comprar 3, 6, 9, 12...
   - ou múltiplos de 4, 6, 12 etc.

4. Pacote fechado:
   - exemplo: pacote com 3 peças;
   - cliente compra 1 pacote, 2 pacotes, 3 pacotes...
   - cada pacote representa X peças;
   - pode não vender unidade individual.

5. Pacote sortido:
   - exemplo: pacote sortido com 6 ou 12 peças;
   - cores podem ser sortidas;
   - cliente não escolhe unidade/cor individualmente;
   - compra o pacote.

6. Grade fechada:
   - exemplo: pacote com composição fixa:
     - 2 P, 2 M, 2 G;
     - ou cores/tamanhos variados.
   - Pode ser futuro/pós-MVP se for complexo demais.

A regra precisa aparecer no cadastro/edição do produto e ser aplicada na vitrine, carrinho e checkout.

==================================================
1. PRINCÍPIO DE MODELAGEM
==================================================

Separar claramente:

Estoque:
- quantas unidades físicas ou pacotes existem?

Regra de venda:
- como o cliente pode comprar?

Exemplo:
- estoque físico: 120 peças.
- regra comercial: vender apenas em múltiplos de 3.
- cliente pode comprar 3, 6, 9, 12...

Ou:
- estoque: 10 pacotes.
- regra comercial: pacote sortido com 6 peças.
- cliente compra 1 pacote, 2 pacotes...
- backend baixa 1 ou 2 unidades do SKU pacote.

Avaliar se o sistema deve tratar pacote como:
A) SKU normal que representa pacote;
B) composição de SKUs filhos;
C) regra comercial aplicada sobre SKU unitário;
D) outro modelo.

==================================================
2. OBJETIVO DO DESIGN
==================================================

Produzir uma proposta técnica para implementar MVP de atacado sem comprometer o fluxo atual de varejo.

O design deve responder:

1. Onde ficam as regras de venda?
   - Product?
   - SKU?
   - Product com override por SKU?
   - entidade separada?

2. Quais sales modes existirão?
   Exemplos:
   - Unit
   - MinimumQuantity
   - MultipleQuantity
   - FixedPackage
   - AssortedPackage
   - ClosedGrid, talvez pós-MVP

3. Quais campos são necessários?
   Exemplos:
   - salesMode
   - minimumQuantity
   - quantityStep
   - packageSize
   - packageLabel
   - packageDescription
   - allowCustomerToChooseVariants
   - unitLabel
   - packageUnitLabel
   - enforceMultiple
   - showTotalPieces
   - isWholesaleOnly

4. Como isso afeta:
   - Admin Product Form;
   - Product detail storefront;
   - Cart;
   - Checkout;
   - CheckoutSession;
   - Inventory reservation;
   - Orders;
   - OrderItems;
   - Payments;
   - Admin Orders;
   - Customer Orders.

5. Quais validações são obrigatórias no backend?

6. Quais validações devem existir no frontend apenas como UX, mas sem ser fonte da verdade?

7. Como manter compatibilidade com produtos atuais?

8. Como migrar produtos existentes?

9. Qual é o MVP recomendado e o que deve ficar para pós-MVP?

==================================================
3. CENÁRIOS DE NEGÓCIO A MODELAR
==================================================

Analisar e propor comportamento para estes casos:

Cenário A — Produto unitário atual
- Produto: Camiseta básica.
- Cliente escolhe cor/tamanho.
- Compra 1, 2, 3...
- Deve continuar funcionando sem mudança perceptível.

Cenário B — Produto só a partir de 3 peças
- Produto: Conjunto Flores.
- Cliente pode escolher variação.
- Quantidade mínima: 3.
- Pode comprar 3, 4, 5...
- Carrinho e checkout devem bloquear 1 ou 2.

Cenário C — Produto só em múltiplos de 3
- Produto: Conjunto Flores.
- Cliente pode escolher variação.
- Pode comprar 3, 6, 9, 12...
- Não pode comprar 4, 5, 7...
- Mensagem:
  “Este produto é vendido em múltiplos de 3.”

Cenário D — Pacote fechado com 6 peças
- Produto: Kit Camisetas.
- Cliente compra quantidade de pacotes.
- 1 pacote = 6 peças.
- Quantidade exibida pode ser:
  - 1 pacote;
  - 2 pacotes;
  - total: 12 peças.
- Preço pode ser por pacote.
- Não deve permitir unidade avulsa.

Cenário E — Pacote sortido com 12 peças
- Produto: Pacote sortido.
- Cliente não escolhe cor individual.
- Texto:
  “Cores sortidas conforme disponibilidade.”
- Compra 1, 2, 3 pacotes.
- Estoque pode ser controlado como SKU pacote.

Cenário F — Grade fechada futura
- Produto: Kit grade P/M/G.
- Pacote contém:
  - 2 P;
  - 2 M;
  - 2 G.
- Avaliar se entra no MVP ou fica pós-MVP.

==================================================
4. DECISÃO SOBRE PACOTE COMO SKU
==================================================

Analisar especialmente esta hipótese recomendada para MVP:

Pacote fechado/sortido deve ser cadastrado como SKU próprio.

Exemplo:
Produto: Conjunto Flores
SKU 1: Rosa M — venda unitária/múltiplo
SKU 2: Azul M — venda unitária/múltiplo
SKU 3: Pacote sortido 6 peças — estoque em pacotes

Vantagens:
- simples;
- compatível com estoque atual por SKU;
- checkout não precisa baixar múltiplos SKUs;
- reduz risco para MVP.

Desvantagens:
- não controla composição real por cor/tamanho;
- depende do lojista separar estoque operacionalmente.

Comparar com alternativa avançada:
- pacote composto por SKUs filhos;
- venda de 1 pacote baixa automaticamente várias unidades de SKUs distintos.

Definir recomendação clara:
- MVP;
- pós-MVP.

==================================================
5. ADMIN PRODUCT FORM
==================================================

Desenhar como a regra aparecerá no admin.

Sugestão:
Adicionar seção:

“Configuração de venda”

Campos:
- Como esse produto/SKU será vendido?
  - Unidade
  - Quantidade mínima
  - Múltiplos
  - Pacote fechado
  - Pacote sortido

Para Unit:
- mínimo = 1
- incremento = 1

Para MinimumQuantity:
- quantidade mínima: [3]
- incremento: [1]

Para MultipleQuantity:
- quantidade mínima: [3]
- vender em múltiplos de: [3]

Para FixedPackage:
- nome do pacote: [Pacote com 6 peças]
- peças por pacote: [6]
- preço por pacote?
- mostrar total de peças? sim/não
- cliente escolhe variações? sim/não

Para AssortedPackage:
- nome do pacote: [Pacote sortido com 12 peças]
- peças por pacote: [12]
- observação: [Cores sortidas conforme disponibilidade]
- cliente escolhe variações? normalmente não

Decidir:
- regra fica no produto e todas as variantes herdam?
- ou regra fica por SKU?
- ou produto tem default e SKU pode sobrescrever?

Recomendação a avaliar:
- MVP: regra no produto com possibilidade futura de override por SKU.
- Se pacote como SKU próprio for necessário, talvez regra no SKU seja mais simples.

O design precisa tomar uma posição.

==================================================
6. STOREFRONT / PRODUCT DETAIL
==================================================

Desenhar UX para cliente.

Produto unitário:
- quantidade normal.

Quantidade mínima:
- mensagem:
  “Compra mínima: 3 peças.”
- contador inicia em 3.

Múltiplos:
- mensagem:
  “Vendido em múltiplos de 3.”
- opções: 3, 6, 9, 12...
- se digitar manualmente, bloquear inválido.

Pacote fechado:
- mostrar:
  “Pacote com 6 peças.”
  “Quantidade de pacotes”
  “Total de peças”
- exemplo:
  2 pacotes = 12 peças.

Pacote sortido:
- mostrar:
  “Pacote sortido com 12 peças.”
  “As cores podem variar conforme disponibilidade.”
- cliente não escolhe cor individual se regra determinar.

Definir:
- se o selector de SKU deve desaparecer em pacote sortido;
- se pacote sortido será um SKU selecionável;
- como explicar para cliente.

==================================================
7. CART
==================================================

Desenhar impacto no carrinho:

- item precisa carregar sales rule snapshot?
- quantidade no carrinho deve respeitar minimumQuantity/quantityStep;
- se produto mudar regra depois que está no carrinho, ao abrir carrinho deve revalidar;
- mensagens de erro no carrinho:
  - mínimo;
  - múltiplo;
  - pacote;
  - estoque insuficiente.

Definir se cart item deve armazenar:
- skuId;
- quantity;
- salesMode snapshot;
- packageSize snapshot;
- packageLabel snapshot;
- totalPieces display;
- unitPrice ou packagePrice.

Hoje o carrinho é localStorage por skuId.
Analisar como adaptar sem quebrar carrinho atual.

==================================================
8. CHECKOUT / BACKEND ENFORCEMENT
==================================================

O backend deve ser fonte da verdade.

No checkout/session creation:
- carregar SKU/Product atual;
- validar quantidade;
- validar mínimo;
- validar múltiplo;
- validar pacote;
- validar se SKU pode ser vendido individualmente;
- validar estoque disponível.

Exemplos:
- produto múltiplo de 3 e payload quantity=4 → 400 ProblemDetails.
- pacote com 6 peças, quantity=2 pacotes → reserva 2 unidades do SKU pacote, não 12, se modelo pacote-as-SKU.
- se modelo peça unitária com regra múltiplo, quantity=6 reserva 6 unidades do SKU.

Definir:
- quantity representa peças ou pacotes?
- para FixedPackage, quantity representa pacotes?
- para MultipleQuantity, quantity representa peças?
- como OrderItem deve exibir isso depois.

Recomendação:
- quantity sempre representa unidade do SKU vendido.
- se SKU é pacote, 1 = 1 pacote.
- packageSize serve para exibição “1 pacote = X peças”.
- Isso mantém Inventory simples.

Avaliar e documentar.

==================================================
9. ORDERS / ORDER ITEMS
==================================================

Definir se OrderItem precisa armazenar snapshot de regra de venda para histórico.

Possíveis campos:
- salesMode;
- packageSize;
- packageLabel;
- quantityUnitLabel;
- totalPieces;
- unitPriceLabel;
- saleRuleDescription.

Por quê?
Se o produto mudar depois, o pedido antigo precisa continuar dizendo:
- “2 pacotes de 6 peças”;
- não apenas “quantidade 2”.

Analisar se isso entra no MVP ou pode ser derivado do Product/SKU atual.

Recomendação provável:
- salvar pelo menos productName, skuCode, quantity, unitPrice e subtotal já existe.
- adicionar snapshot de saleDisplayLabel pode ser útil.
- avaliar migration.

==================================================
10. INVENTORY
==================================================

Analisar impacto:

Modelo A — unidade/múltiplo:
- quantity = peças;
- baixa/reserva usa quantity diretamente.

Modelo B — pacote como SKU:
- quantity = pacotes;
- estoque é de pacotes;
- packageSize apenas exibição;
- não baixa estoque das peças internas.

Modelo C — pacote composto:
- quantity = pacotes;
- cada pacote baixa múltiplos SKUs filhos;
- pós-MVP.

Definir recomendação para MVP:
- manter estoque simples por SKU vendido.
- pacote é SKU vendido.
- composição fica pós-MVP.

Também avaliar:
- como exibir “disponível”:
  - 10 pacotes disponíveis;
  - ou 60 peças em pacotes?
- se estoque em admin deve ter label “pacotes” em vez de “unidades”.

==================================================
11. DADOS / DOMÍNIO
==================================================

Propor modelo de dados.

Opção possível:

Product:
- SalesMode
- MinimumQuantity
- QuantityStep
- PackageSize
- PackageLabel
- PackageDescription
- AllowCustomerToChooseVariants
- ShowTotalPieces
- IsWholesaleOnly

Ou em SKU:
Sku:
- SalesMode
- MinimumQuantity
- QuantityStep
- PackageSize
- PackageLabel
- PackageDescription
- QuantityUnitLabel
- PiecesPerUnit

Ou entidade:
SalesRule:
- Id
- ProductId
- SkuId nullable
- Mode
- MinQuantity
- StepQuantity
- PackageSize
- Label
- Description

Avaliar prós/contras:
- por produto;
- por SKU;
- entidade separada.

Considerar:
- simplicidade;
- compatibilidade;
- futuro B2B;
- overrides;
- consultas de vitrine;
- validação de checkout;
- migrations.

Entregar recomendação final.

==================================================
12. API CONTRACTS
==================================================

Desenhar contratos esperados para:

Admin product create/update:
- incluir sales rule.

Admin product detail:
- retornar sales rule.

Storefront product detail:
- retornar sales rule necessária para UX.

Cart/checkout validation:
- retornar erro claro se quantity inválida.

OrderItem:
- retornar dados de exibição de pacote se aplicável.

ProblemDetails esperados:
- quantity:
  “Quantidade mínima deste produto é 3.”
- quantity:
  “Este produto é vendido em múltiplos de 3.”
- skuId:
  “Este SKU só pode ser comprado como pacote fechado.”
- package:
  “Pacote inválido para este produto.”

==================================================
13. VALIDAÇÕES
==================================================

Definir validações backend:

Para Unit:
- minimumQuantity = 1;
- quantityStep = 1.

Para MinimumQuantity:
- minimumQuantity > 1;
- quantityStep = 1.

Para MultipleQuantity:
- minimumQuantity >= 1;
- quantityStep > 1;
- minimumQuantity deve ser múltiplo de quantityStep?
  Exemplo:
  - mínimo 3, step 3.
  - mínimo 6, step 3.
  Validar e decidir.

Para FixedPackage:
- packageSize > 1;
- minimumQuantity >= 1;
- quantityStep >= 1;
- packageLabel obrigatório ou gerado;
- allowCustomerToChooseVariants definido.

Para AssortedPackage:
- packageSize > 1;
- packageLabel obrigatório;
- description recomendada;
- allowCustomerToChooseVariants provavelmente false.

Validação de quantidade no checkout:
- quantity >= minimumQuantity;
- (quantity - minimumQuantity) % quantityStep == 0
  ou
- quantity % quantityStep == 0
  dependendo da regra escolhida.

Documentar fórmula escolhida.

==================================================
14. MIGRATION / COMPATIBILIDADE
==================================================

Desenhar migration segura:

Produtos existentes devem virar:
- salesMode = Unit
- minimumQuantity = 1
- quantityStep = 1
- packageSize = null
- packageLabel = null
- allowCustomerToChooseVariants = true

Sem quebrar:
- produtos atuais;
- carrinho atual;
- checkout atual;
- orders atuais.

Avaliar se será necessário:
- preencher nullable primeiro;
- depois tornar required;
- default no banco;
- default na aplicação.

==================================================
15. IMPLEMENTAÇÃO EM FASES
==================================================

Entregar plano em fases.

Fase 0 — design e docs:
- este prompt.

Fase 1 — backend domínio + validação:
- sales rule no produto/SKU;
- contracts;
- checkout enforcement;
- tests.

Fase 2 — frontend admin:
- seção Configuração de venda;
- validação;
- salvar/editar.

Fase 3 — storefront/cart:
- exibição;
- quantity selector;
- cart revalidation.

Fase 4 — checkout/orders:
- mensagens;
- snapshot order item, se aprovado.

Fase 5 — pós-MVP:
- composição de pacote;
- tabela de preço atacado;
- regras por cliente;
- pedido mínimo global;
- aprovação B2B;
- frete por volume.

==================================================
16. RISCOS
==================================================

Classificar riscos:

- confundir peças vs pacotes;
- estoque incorreto;
- checkout aceitar quantidade inválida;
- pedido antigo mudar exibição após alteração de regra;
- admin cadastrar regra incoerente;
- cliente comprar unidade quando deveria comprar pacote;
- pacote sortido exigir composição real;
- carrinho localStorage ficar stale;
- migration quebrar produtos existentes.

Classificar em:
- BLOCKER;
- HIGH;
- MEDIUM;
- LOW.

==================================================
17. DOCUMENTAÇÃO A CRIAR
==================================================

Criar documento de design:

docs/architecture/WHOLESALE-SALES-RULES-DESIGN.md

Esse documento deve conter:

1. Resumo executivo.
2. Requisito de negócio.
3. Cenários de venda.
4. Decisão MVP.
5. O que fica pós-MVP.
6. Modelo de domínio recomendado.
7. Modelo de dados proposto.
8. Contratos de API propostos.
9. Impacto no admin.
10. Impacto na vitrine.
11. Impacto no carrinho.
12. Impacto no checkout.
13. Impacto no estoque.
14. Impacto em orders.
15. Regras de validação.
16. Estratégia de migration.
17. Plano de implementação por fases.
18. Riscos.
19. Perguntas em aberto para o negócio.
20. Critérios de aceite para implementação futura.

Também atualizar, se fizer sentido:
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Mas não criar código de feature.

==================================================
18. PERGUNTAS DE NEGÓCIO A RESPONDER
==================================================

Se não for possível decidir, listar perguntas para o usuário:

1. Um produto pode ter venda unitária e pacote ao mesmo tempo?
   Exemplo: vender unidade e pacote com desconto.

2. Pacote sortido terá estoque próprio ou consome estoque de cores/tamanhos individuais?

3. Preço do pacote é por pacote ou por peça?

4. O cliente atacado pode misturar variações para atingir o mínimo?
   Exemplo: 1 rosa + 1 azul + 1 preta = mínimo 3?

5. O mínimo é por SKU, por produto ou por carrinho?

6. Será necessário preço por faixa?
   Exemplo:
   - 3 peças: R$ 50 cada
   - 12 peças: R$ 42 cada

7. Atacado será público ou somente para clientes aprovados/logados?

8. Existem produtos que só aparecem para atacado?

9. É necessário pedido mínimo global?
   Exemplo: pedido mínimo R$ 500.

10. O pacote fechado precisa mostrar composição exata?

==================================================
19. RESULTADO ESPERADO NO CHAT
==================================================

Ao final, retornar:

1. Arquivo de design criado.
2. Recomendação principal.
3. Modelo recomendado:
   - Product;
   - SKU;
   - SalesRule separada.
4. Decisão sobre pacote como SKU.
5. Fases de implementação.
6. Riscos principais.
7. Perguntas em aberto para o negócio.
8. Próximo prompt recomendado para implementação backend.

Critérios de aceite:
- Nenhuma feature implementada.
- Design document criado.
- MVP e pós-MVP claramente separados.
- Impactos em catálogo/carrinho/checkout/estoque/orders descritos.
- Riscos classificados.
- Perguntas de negócio listadas.