Você está atuando como engenheiro fullstack sênior do projeto Shopflow, especialista em .NET, React, TypeScript, Clean Architecture, DDD, e-commerce, Backoffice, logística/fulfillment e UX operacional para assessoria de compras.

Objetivo:
Organizar e preparar a próxima evolução do Shopflow após validação com cliente.

Contexto de negócio:
Shopflow está sendo usado para uma assessoria de compras, onde a venda é feita principalmente para lojistas e revendedores.

O cliente pode fazer várias compras intercaladas e depois desejar que os pedidos sejam enviados juntos. O admin precisa ter controle operacional sobre pedidos pagos, envio, entrega, observações e preferências de entrega.

Pontos levantados pelo cliente:
1. Tipos de entrega disponíveis:
   - Transportadora
   - Ônibus de excursão
   - Correios

2. Cliente pode marcar preferência de dia de entrega:
   - mínimo 2 dias úteis após a compra.

3. Área administrativa precisa movimentar pedido:
   - marcar pedido como enviado;
   - marcar pedido como entregue;
   - avaliar se precisamos de outros status.

4. Admin precisa agrupar pedidos:
   - selecionar pedidos pendentes de envio de um mesmo cliente;
   - marcar vários pedidos como enviados ou entregues;
   - melhorar controle de clientes que compram várias vezes e recebem junto.

5. Criar campo de observação no pedido:
   - avaliar melhor local e tipo de observação.

6. Criar chat com vendedor:
   - avaliar se agora deve ser chat real ou WhatsApp/contato com vendedor.

7. Bugs/ajustes identificados:
   - erro ao remover item do estoque;
   - endereço está sem formatação de CEP;
   - categoria não carrega na edição do produto;
   - busca CEP e preenchimento automático de endereço.

Decisão de produto:
Não implementar o módulo completo de entrega/fulfillment de uma vez.
Primeiro:
- corrigir bugs imediatos;
- criar documento técnico de arquitetura/roadmap para Delivery/Fulfillment;
- separar claramente status do pedido, status do pagamento e status da entrega.

Não misturar status de pagamento com status de entrega.
Não usar dados fake.
Não criar chat nativo agora sem decisão explícita.
Não quebrar checkout, Pix, Orders, Inventory, Admin Products ou Storefront.

==================================================
1. ESCOPO DESTE PROMPT
==================================================

Este prompt tem dois objetivos:

A) Corrigir ou diagnosticar os bugs imediatos:
1. Erro ao remover item do estoque.
2. Categoria não carrega na edição do produto.
3. CEP sem formatação.
4. Preparar ou implementar busca CEP, se for simples e segura.

B) Criar documentação técnica para o próximo módulo:
Delivery/Fulfillment + agrupamento de pedidos.

Se algum bug exigir mudança backend maior, documentar e não criar solução improvisada.

Se alguma feature logística exigir migration/backend/contrato novo, documentar primeiro e não implementar ainda.

==================================================
2. PRIORIDADES
==================================================

Prioridade Alta / P0-P1:
1. Erro ao remover item do estoque.
2. Categoria não carrega na edição do produto.
3. CEP sem formatação visual.
4. Documento técnico Delivery/Fulfillment.

Prioridade Média:
5. Busca CEP e autopreenchimento.
6. Tipos de entrega no checkout/pedido.
7. Preferência de data mínima 2 dias úteis.
8. Observação do cliente no pedido.
9. Observação interna admin.

Prioridade Próxima etapa:
10. FulfillmentStatus:
    - Aguardando envio
    - Enviado
    - Entregue

11. Admin actions:
    - Marcar como enviado
    - Marcar como entregue

12. Agrupamento:
    - pedidos pagos e pendentes de envio do mesmo cliente;
    - criar entrega/remessa agrupada;
    - marcar grupo como enviado/entregue.

Prioridade futura:
13. WhatsApp/falar com vendedor.
14. Chat nativo.

==================================================
3. BUG 1 — ERRO AO REMOVER ITEM DO ESTOQUE
==================================================

Auditar o fluxo de remover estoque no Admin Inventory.

Verificar:
- componente/form usado para remoção;
- service/hook de inventory;
- payload enviado;
- endpoint chamado;
- se CSRF está sendo enviado corretamente;
- se reason/motivo obrigatório está sendo enviado;
- se quantidade enviada é número válido;
- se o backend exige não remover mais que disponível;
- se o erro aparece como ProblemDetails e está sendo mapeado.

Contexto técnico relevante:
Anteriormente, o backend passou a exigir motivo/reason para remoção de estoque e validar saldo disponível.

Corrigir se for frontend:
- garantir que payload usa o contrato correto;
- garantir que reason é obrigatório e enviado;
- garantir que quantidade é number;
- mapear erro ProblemDetails para mensagem amigável;
- invalidar adminInventoryKeys.skus após sucesso;
- não quebrar add/reserve/confirm/cancel.

Se o erro for backend:
- documentar endpoint, payload, resposta e hipótese;
- criar pendência backend em docs.

Critérios de aceite:
- remover estoque funciona quando payload é válido;
- erro de saldo insuficiente aparece amigável;
- erro de motivo obrigatório aparece amigável;
- listagem de SKUs atualiza após remoção;
- typecheck/build passam.

==================================================
4. BUG 2 — CATEGORIA NÃO CARREGA NA EDIÇÃO DO PRODUTO
==================================================

Auditar o fluxo de edição de produto:

- AdminProductEdit
- AdminProductForm
- category select
- carregamento de categorias
- detail do produto
- mapeamento categoryId/categorySlug/category object
- payload update

Problema:
Ao abrir edição, categoria não está carregando/selecionando corretamente.

Verificar:
1. O detail retorna categoryId, category, categorySlug ou objeto category?
2. O form espera qual formato?
3. As categorias carregam antes/depois do produto?
4. Existe mismatch entre string/Guid/slug?
5. O Select usa value controlado?
6. O valor é setado antes das opções existirem?
7. O update envia categoryId correto?

Corrigir:
- hidratar categoria corretamente no edit;
- manter select controlado;
- aceitar categoryId do detail como fonte principal;
- se só vier category.slug, resolver corretamente usando lista de categorias;
- não salvar categoria vazia por engano;
- não alterar fluxo de criação.

Critérios de aceite:
- produto com categoria abre edit com categoria selecionada;
- produto sem categoria mostra vazio/fallback correto;
- trocar categoria e salvar envia valor correto;
- não quebra create;
- typecheck/build passam.

==================================================
5. BUG 3 — CEP SEM FORMATAÇÃO
==================================================

Auditar exibição de endereço em:

- checkout;
- confirmação;
- pedido público/guest;
- minha conta/pedidos;
- admin order detail;
- admin orders list, se exibir endereço;
- componentes compartilhados de endereço.

Implementar helper, se ainda não existir:

formatCepBR(value):
- "02310000" → "02310-000"
- "02310-000" → "02310-000"
- valores inválidos: retornar original seguro ou string vazia conforme padrão.

Usar apenas na UI.
Não alterar valor salvo no banco neste ajuste.

Critérios de aceite:
- CEP aparece formatado em telas de pedido/endereço;
- não duplica máscara;
- não quebra CEP já formatado;
- testes unitários cobrem helper;
- typecheck/build passam.

==================================================
6. BUSCA CEP / AUTOPREENCHIMENTO
==================================================

Avaliar implementação de busca CEP.

Decisão recomendada para MVP:
- implementar no frontend somente se já houver padrão seguro para chamada externa ou service próprio;
- preferir ViaCEP se projeto aceitar dependência externa no frontend;
- se o projeto evitar chamada externa direta, documentar necessidade de backend proxy.

Comportamento desejado:
- usuário digita CEP;
- ao completar 8 dígitos, buscar endereço;
- preencher rua, bairro, cidade, UF;
- permitir edição manual;
- mostrar erro amigável se CEP não encontrado;
- não travar checkout;
- não enviar request a cada tecla sem debounce/controle.

Atenção:
- não incluir API key;
- não depender de serviço externo para finalizar pedido;
- se falhar, usuário deve preencher manualmente.

Se implementar agora:
- criar helper/service isolado;
- aplicar no checkout e/ou cadastro de endereço, conforme existir;
- testes unitários;
- não quebrar checkout.

Se não implementar agora:
- documentar como P1 em frontend-next-actions.

==================================================
7. DOCUMENTO TÉCNICO DELIVERY/FULFILLMENT
==================================================

Criar documento:

docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md

O documento deve organizar a futura implementação, sem codar ainda.

Conteúdo obrigatório:

1. Contexto de negócio:
- assessoria de compras;
- lojistas/revendedores;
- compras intercaladas;
- pedidos podem ser enviados juntos.

2. Separação de status:
- OrderStatus/payment/customerStatus continuam representando pagamento/comercial;
- Delivery/Fulfillment deve ser dimensão separada.

3. DeliveryMethod:
- Transportadora
- OnibusExcursao
- Correios

4. PreferredDeliveryDate:
- data preferida pelo cliente;
- mínimo 2 dias úteis após compra;
- MVP: dias úteis = segunda a sexta;
- feriados ficam para depois;
- é preferência, não garantia.

5. Observações:
- customerOrderNote:
  visível admin e cliente;
  capturada no checkout.
- internalOrderNote:
  somente admin;
  usada em suporte/operação.
- deliveryNote:
  relacionada ao envio/entrega.
- batchNote:
  relacionada à entrega agrupada/remessa.

6. FulfillmentStatus inicial:
- AwaitingShipment / Aguardando envio
- Shipped / Enviado
- Delivered / Entregue

Avaliar se incluir agora ou deixar futuro:
- Separating / Em separação
- ReadyToShip / Pronto para envio
- DeliveryIssue / Problema na entrega
- Returned / Devolvido

Recomendação:
MVP começa com 3 status:
Aguardando envio → Enviado → Entregue.

7. Admin actions:
- marcar pedido como enviado;
- marcar pedido como entregue;
- registrar shippedAt;
- registrar deliveredAt;
- registrar trackingCode/reference;
- registrar método final;
- registrar observação interna.

8. Agrupamento:
Conceito recomendado:
DeliveryBatch ou ShipmentBatch.

Finalidade:
- agrupar pedidos pagos e aguardando envio de um mesmo cliente;
- marcar todos como enviados/entregues juntos;
- organizar envio por ônibus, transportadora ou Correios.

Campos sugeridos:
- id
- customerUserId nullable
- customerEmail normalized
- customerPhone normalized
- deliveryMethod
- status
- orderIds
- shippedAt
- deliveredAt
- trackingCode/reference
- internalNote
- createdAt
- createdByAdminId

Critérios para agrupar:
- pedidos pagos/confirmados;
- fulfillmentStatus = AwaitingShipment;
- mesmo customerUserId quando existir;
- para convidado, email normalizado + telefone, com cuidado;
- nunca agrupar só por nome;
- alertar se endereços forem diferentes.

9. UX Admin:
- filtro “Aguardando envio” em pedidos;
- no detalhe do pedido, bloco “Entrega”;
- seção “Outros pedidos pendentes deste cliente”;
- seleção múltipla;
- ação “Criar entrega agrupada”;
- ação “Marcar selecionados como enviados”;
- ação “Marcar selecionados como entregues”.

10. UX Cliente:
- checkout:
  método de entrega preferido;
  data preferida;
  observação.
- pós-compra:
  mostrar preferência escolhida;
  mostrar status de entrega quando existir.
- CTA:
  “Falar com vendedor pelo WhatsApp” antes de chat nativo.

11. WhatsApp vs chat:
- MVP: botão WhatsApp com mensagem pré-preenchida.
- Futuro: chat nativo por pedido/cliente.

12. Roadmap sugerido:
Fase 1:
- bugs imediatos;
- CEP formatado;
- documentação.

Fase 2:
- campos de entrega no checkout/order;
- fulfillmentStatus no pedido;
- admin marcar enviado/entregue.

Fase 3:
- agrupamento de pedidos/remessa.

Fase 4:
- WhatsApp;
- chat nativo, se necessário.

13. Não fazer agora:
- chat real;
- rastreamento automático;
- integração Correios/transportadora;
- cálculo de frete;
- status avançados;
- feriados nacionais/municipais;
- remessa composta complexa sem design.

==================================================
8. DOCUMENTAÇÃO DE ROADMAP
==================================================

Atualizar também:

- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/frontend-next-actions.md
- docs/ai-context/technical-debt.md
- apps/web/docs/ai-context/frontend-next-actions.md, se existir no frontend
- apps/web/docs/ai-context/frontend-technical-debt.md, se existir no frontend

Registrar:
- bugs corrigidos;
- bugs pendentes;
- Delivery/Fulfillment como próximo módulo;
- busca CEP como P1 se não implementada;
- WhatsApp antes de chat nativo;
- chat nativo como futuro.

==================================================
9. TESTES
==================================================

Se corrigir bugs de frontend:
- criar/ajustar testes unitários relevantes;
- criar/ajustar Cypress se já existir spec próxima.

Obrigatórios se houver alteração de código:
1. Remover estoque envia reason/quantity corretamente.
2. Erro de remover estoque aparece amigável.
3. Edit product hidrata categoria.
4. Trocar categoria no edit salva categoria correta.
5. formatCepBR formata CEP.
6. CEP já formatado permanece correto.

Se implementar busca CEP:
7. CEP válido preenche rua/bairro/cidade/UF.
8. CEP inválido mostra erro.
9. Falha da API permite preenchimento manual.

Rodar:
- npm run typecheck
- npm run build
- testes unitários afetados

Se mexer backend:
- dotnet build
- dotnet test afetado

==================================================
10. NÃO FAZER
==================================================

Não implementar agora:
- Delivery/Fulfillment completo;
- migrations de delivery;
- DeliveryBatch;
- chat nativo;
- integração Correios/transportadora;
- cálculo de frete;
- status avançados;
- feriados;
- alteração em pagamento/Pix;
- alteração em checkout além de CEP, se decidido.

Não usar:
- dados fake;
- endpoint público dentro do admin;
- gambiarra de status de pedido para representar entrega.

Não misturar:
- status de pagamento;
- status do pedido;
- status de entrega.

==================================================
11. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Bugs auditados.
2. Bugs corrigidos.
3. Bugs que exigem backend ou decisão adicional.
4. Arquivos alterados.
5. Testes criados/alterados.
6. Resultado npm run typecheck.
7. Resultado npm run build.
8. Se houve backend, resultado dotnet build/test.
9. Documento DELIVERY-FULFILLMENT-DESIGN.md criado.
10. Roadmap final por fases.
11. Próximo prompt recomendado.

Critérios de aceite:
- erro de remover estoque corrigido ou diagnosticado com precisão;
- categoria carrega no edit ou causa documentada;
- CEP formatado na UI;
- busca CEP implementada ou documentada como P1;
- documento Delivery/Fulfillment criado;
- não houve implementação precipitada de entrega/chat;
- typecheck/build passam.