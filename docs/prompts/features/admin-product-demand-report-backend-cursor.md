# Implementar relatório administrativo de demanda por produto e pedidos aguardando pagamento — Shopflow

## Papel

Atue como engenheiro backend sênior responsável pelo projeto Shopflow.

Antes de implementar, inspecione o código existente e respeite a arquitetura, as convenções, os contratos de API, as entidades, os enums e os padrões já adotados no projeto.

Não presuma que nomes de entidades, propriedades, endpoints ou status sejam iguais aos descritos neste prompt. Confirme tudo no código antes de utilizá-los.

---

## 1. Contexto do projeto

O Shopflow é uma plataforma de e-commerce com:

- Backend em .NET 10.
- ASP.NET Core Minimal APIs.
- Clean Architecture.
- DDD.
- CQRS/MediatR.
- Entity Framework Core.
- PostgreSQL.
- Módulos existentes de catálogo, produtos, SKUs, inventário, carrinho, checkout, pedidos e pagamentos.
- Área administrativa com autenticação e autorização existentes.

O frontend é um repositório separado. Esta tarefa contempla exclusivamente o backend.

Já existem regras de negócio para pedidos, pagamentos e estoque. Elas devem ser reutilizadas, sem criar um segundo fluxo paralelo.

## 2. Objetivo

Implementar consultas administrativas para ajudar a operação da loja a:

1. Identificar a demanda confirmada de produtos, agrupada por produto e SKU.
2. Consultar os pedidos que compõem essa demanda, identificando quem comprou cada item.
3. Identificar pedidos ativos que ainda aguardam pagamento.
4. Consultar os dados de contato do comprador para que o administrador possa entrar em contato manualmente.
5. Disponibilizar filtros e paginação para que o frontend construa uma tela administrativa utilizável.

O relatório deve permitir, por exemplo, que a equipe consulte a demanda confirmada de determinado SKU e veja os pedidos, compradores e quantidades que formam aquele total.

Pedidos aguardando pagamento devem aparecer separadamente, sem serem contabilizados como vendas confirmadas.

## 3. Antes de implementar: auditar o projeto

Inspecione, no mínimo:

- Entidades de Product, SKU, ProductVariant ou equivalentes.
- Entidades de Order e OrderItem.
- Entidades ou estruturas de Payment.
- Relacionamento entre pedidos, itens, produtos e SKUs.
- Snapshots de nome, SKU, variante, preço e dados do comprador armazenados no pedido.
- Status de pedido, pagamento, cancelamento, expiração e reembolso, caso existam.
- Endpoints administrativos de pedidos.
- Autorização administrativa e políticas existentes.
- Organização atual das Features, Queries, Handlers, DTOs e endpoints.
- Padrões de filtros, paginação, ordenação e respostas da API.
- Testes existentes relacionados a pedidos e pagamentos.
- Documentação atual dos módulos.

Antes de alterar código, apresente um diagnóstico breve com:

- Entidades e relacionamentos relevantes.
- Status reais encontrados e seus significados.
- Endpoints e convenções existentes que devem ser reutilizados.
- Estratégia de implementação proposta.
- Riscos de compatibilidade identificados.

Depois, prossiga com a implementação. Não pare apenas no diagnóstico.

## 4. Relatório de demanda confirmada

Criar uma consulta administrativa que agregue a quantidade vendida por produto e SKU.

### 4.1. Regras de negócio

- Considerar apenas pedidos com pagamento efetivamente confirmado, conforme as regras e os status reais do domínio.
- Excluir pedidos cancelados ou invalidados de acordo com as regras existentes.
- Considerar regras de reembolso ou devolução somente se já existirem no domínio; não inventar novos estados.
- Não considerar pedidos aguardando pagamento como vendas confirmadas.
- Não alterar o status de nenhum pedido ou pagamento.
- Não modificar estoque, reservas ou movimentações de inventário.
- Utilizar as quantidades registradas nos itens dos pedidos.
- Não multiplicar automaticamente a quantidade pelo tamanho de pacote ou lote (`packageSize`). A quantidade do item representa unidades do SKU vendido, conforme as regras atuais do Shopflow.
- Preservar as informações históricas dos pedidos. Quando houver snapshots do produto, SKU, variante e preço no item do pedido, priorizá-los para exibição histórica.
- Não depender exclusivamente do estado atual do cadastro do produto para reconstruir vendas antigas.

### 4.2. Dados do resumo

Cada agrupamento deve retornar, quando disponível no modelo existente:

- Identificador do produto.
- Nome do produto.
- Identificador do SKU.
- Código do SKU.
- Variante ou atributos relevantes.
- Quantidade total vendida.
- Quantidade de pedidos distintos que contribuíram para o total.
- Informações de preço ou valor agregado, somente quando houver significado consistente e os dados necessários estiverem disponíveis.

O agrupamento precisa distinguir SKUs diferentes do mesmo produto.

Não misturar variantes diferentes em um único total sem uma identificação explícita de SKU.

### 4.3. Detalhamento por SKU

Implementar uma consulta para obter os pedidos que compõem a demanda de um produto/SKU selecionado.

Para cada pedido, retornar os dados necessários para identificar a compra, respeitando os dados realmente existentes:

- Identificador e número público do pedido, quando houver.
- Data de criação ou confirmação do pedido.
- Nome do comprador.
- E-mail do comprador, quando disponível.
- Telefone do comprador, quando disponível.
- Quantidade do SKU naquele pedido.
- Nome e identificação histórica do item.
- Status relevante do pedido e do pagamento.
- Preço unitário e total da linha, se disponíveis.

O detalhamento deve permitir que o frontend exiba uma lista de compradores e pedidos associada a cada SKU.

Se os dados de contato estiverem armazenados como snapshot no pedido, utilizar esses dados históricos. Se houver necessidade de consultar o cadastro atual do cliente como fallback, fazê-lo somente quando isso estiver de acordo com o modelo e com as regras atuais do projeto.

Pedidos de convidados existentes também devem ser considerados quando cumprirem as regras de confirmação de pagamento e possuírem os dados necessários.

Não exigir que o comprador tenha uma conta cadastrada para aparecer no relatório.

## 5. Relatório de pedidos aguardando pagamento

Criar uma consulta administrativa separada para listar pedidos ativos que ainda aguardam pagamento.

### 5.1. Regras de negócio

- Utilizar os status reais de pagamento e pedido existentes.
- Considerar somente pedidos ativos e elegíveis para acompanhamento de pagamento.
- Excluir pedidos cancelados ou expirados, conforme as regras atuais do domínio.
- Não tratar qualquer status diferente de `Paid` automaticamente como aguardando pagamento. Validar os estados existentes e definir explicitamente quais são elegíveis.
- Não incluir pedidos já pagos.
- Não alterar o estado dos pedidos.
- Não disparar cobranças, mensagens ou notificações.
- Não consultar diretamente o provedor de pagamento para cada linha do relatório.
- Utilizar os dados persistidos no Shopflow e respeitar o processo existente de confirmação por webhook ou reconciliação.

### 5.2. Dados retornados

Para cada pedido, retornar quando disponível:

- Identificador e número público do pedido.
- Data de criação.
- Nome do comprador.
- E-mail e telefone para contato.
- Valor total do pedido.
- Status do pedido.
- Status do pagamento.
- Identificador do pagamento, se necessário e seguro para uso administrativo.
- Itens do pedido, contendo:
  - Nome do produto.
  - Código e identificador do SKU.
  - Variante ou atributos.
  - Quantidade.
  - Preço unitário e total da linha, se disponíveis.

A resposta deve permitir identificar rapidamente quem comprou o quê, mesmo quando um pedido contém vários produtos.

### 5.3. Contato via WhatsApp

O frontend utilizará os dados retornados para abrir uma conversa manualmente com o comprador.

- Retornar o telefone disponível de maneira consistente com os contratos existentes.
- Se for simples e seguro, disponibilizar também uma representação normalizada do telefone, contendo apenas os dígitos necessários para formar um link de WhatsApp.
- Reutilizar utilitários de normalização já existentes, se houver.
- Não inventar códigos de país ou números ausentes.
- Não gerar links para telefones inválidos.
- Não enviar mensagens automaticamente.
- Não criar integração de WhatsApp, serviço de mensageria ou automação de cobrança nesta tarefa.

A ação de contato e a mensagem pré-preenchida serão responsabilidade do frontend.

## 6. Endpoints e contrato da API

Siga as convenções reais do projeto, principalmente os padrões dos endpoints administrativos de pedidos.

Como proposta inicial, avaliar os seguintes contratos:

### 6.1. Resumo da demanda

`GET /api/admin/reports/product-demand`

Suportar, quando compatível com os padrões atuais:

- Filtro por período.
- Busca por nome do produto, código do SKU ou identificador disponível.
- Paginação.
- Ordenação estável.

A resposta deve apresentar os agrupamentos por produto/SKU e os metadados de paginação no padrão já utilizado pelo projeto.

### 6.2. Detalhamento por SKU

`GET /api/admin/reports/product-demand/{skuId}/orders`

Suportar:

- Filtro por período, se aplicável.
- Paginação.
- Ordenação estável.

Retornar os pedidos elegíveis que contribuíram para a quantidade agregada daquele SKU.

Se o modelo atual não utilizar um identificador de SKU compatível com essa rota, adaptar o contrato à identidade real do item comercializável. Não criar identificadores artificiais.

### 6.3. Pedidos aguardando pagamento

`GET /api/admin/reports/unpaid-orders`

Suportar, quando compatível com as convenções existentes:

- Filtro por período.
- Busca por número do pedido.
- Busca por nome, e-mail ou telefone do comprador.
- Busca por nome do produto ou código do SKU.
- Paginação.
- Ordenação estável.

A resposta deve conter os dados de contato e os itens necessários para a tela administrativa.

### 6.4. Contrato definitivo

Os caminhos acima são uma proposta, não uma autorização para duplicar funcionalidades existentes.

Antes de criar qualquer rota:

- Verifique se já existe endpoint que atende parte ou toda a necessidade.
- Reutilize consultas e componentes compartilhados quando adequado.
- Evite duplicar regras de autorização ou de status.
- Ajuste os caminhos ao padrão atual do projeto se necessário.
- Documente o contrato final implementado, incluindo parâmetros, filtros, ordenação, paginação e exemplos de resposta.

Não implemente endpoints no frontend nem altere o repositório web.

## 7. Arquitetura e qualidade da implementação

- Respeitar Clean Architecture e as dependências entre camadas.
- Utilizar o padrão CQRS/MediatR adotado pelo projeto.
- Criar Queries, Handlers, DTOs e endpoints seguindo as convenções existentes.
- Manter regras de negócio e consultas na camada apropriada.
- Evitar lógica de domínio dentro dos endpoints HTTP.
- Utilizar projeções do Entity Framework Core para buscar somente os campos necessários.
- Evitar carregar grafos inteiros de entidades quando uma projeção for suficiente.
- Evitar N+1 queries.
- Não fazer uma consulta ao banco por SKU ou por pedido dentro de loops.
- Usar agregações no banco sempre que possível.
- Implementar paginação no servidor.
- Usar ordenação estável, incluindo um critério de desempate.
- Evitar retornar coleções ilimitadas.
- Respeitar os limites de paginação adotados pelo projeto.
- Considerar os índices existentes antes de propor novos.
- Não adicionar dependências sem necessidade.
- Não expor entidades internas diretamente nos contratos HTTP.
- Respeitar cancelamento de requisições com `CancellationToken`, seguindo o padrão do projeto.
- Seguir os padrões atuais de tratamento de erros e validação.

Se houver diferenças entre este prompt e a arquitetura real do repositório, prevalece a arquitetura implementada no projeto.

## 8. Segurança e autorização

Essas consultas contêm dados pessoais de compradores e devem ser acessíveis exclusivamente a administradores autorizados.

- Aplicar o mecanismo de autenticação e autorização já existente.
- Não criar uma nova estratégia de autenticação.
- Não remover ou enfraquecer políticas administrativas.
- Não tornar as rotas públicas.
- Não retornar senhas, tokens, cookies, credenciais ou dados sensíveis de autenticação.
- Retornar somente os dados pessoais necessários para a operação administrativa.
- Seguir os padrões de logs e auditoria já existentes, sem registrar desnecessariamente dados pessoais.
- Não introduzir acesso de clientes às consultas administrativas.

## 9. Testes

Criar ou atualizar testes seguindo os padrões atuais do repositório.

Cobrir, no mínimo:

### Demanda confirmada

1. Pedidos pagos e elegíveis são contabilizados.
2. Pedidos aguardando pagamento não entram na demanda confirmada.
3. Pedidos cancelados ou invalidados são tratados de acordo com as regras existentes.
4. SKUs diferentes são agregados separadamente.
5. As quantidades respeitam a unidade comercial do SKU.
6. O detalhamento apresenta os pedidos que compõem o agrupamento.
7. Pedidos de convidados elegíveis são considerados.
8. Filtros de período funcionam conforme o contrato.
9. Paginação e ordenação funcionam corretamente.

### Pedidos aguardando pagamento

1. Pedidos ativos elegíveis e não pagos são retornados.
2. Pedidos pagos não são retornados.
3. Pedidos cancelados ou expirados são excluídos conforme as regras do domínio.
4. Pedidos com múltiplos itens retornam todos os itens relevantes.
5. Os dados de contato são retornados quando disponíveis.
6. Compradores sem telefone não provocam erro.
7. Busca e filtros funcionam corretamente.
8. Paginação funciona corretamente.

### Segurança

1. Usuário não autenticado não acessa as rotas.
2. Usuário sem autorização administrativa não acessa as rotas.
3. Administrador autorizado consegue consultar os relatórios.

Utilize os mecanismos de teste já existentes. Não crie mocks que contradigam as regras reais do domínio.

## 10. Compatibilidade e escopo negativo

Esta implementação é somente de leitura.

Não implementar:

- Frontend ou componentes React.
- Alterações no fluxo de checkout.
- Alterações no cadastro de clientes.
- Novos endpoints de autenticação.
- Novos endpoints de pagamento.
- Integração com WhatsApp.
- Envio automático de mensagens ou cobranças.
- Novos estados de pedidos ou pagamentos sem necessidade comprovada.
- Alterações no cálculo de estoque.
- Alterações na confirmação de pagamentos.
- Novas integrações com Mercado Pago.
- Novas migrações de banco sem necessidade comprovada.
- Refatorações globais fora do escopo.
- Mudanças que alterem o comportamento existente de Admin Catalog, Admin Inventory, pedidos ou checkout.

Se alguma informação necessária não existir no banco ou no modelo atual, não invente dados nem implemente uma mudança de domínio ampla. Documente a limitação e proponha a menor solução compatível com a arquitetura.

## 11. Documentação

Atualize somente a documentação pertinente e existente, por exemplo:

- `docs/architecture.md`
- `docs/next-steps.md`
- `docs/testing.md`

Se o projeto possuir documentação específica de APIs administrativas, prefira atualizá-la.

Documentar:

- Objetivo dos relatórios.
- Critérios usados para definir demanda confirmada.
- Critérios usados para definir pedidos aguardando pagamento.
- Endpoints efetivamente implementados.
- Filtros e paginação.
- Campos de resposta.
- Regras para dados históricos e pedidos de convidados.
- Testes executados.
- Limitações ou pendências reais.

Não criar documentação extensa ou duplicada.

## 12. Validação final

Execute os comandos disponíveis no repositório, respeitando os nomes reais dos scripts:

- Build da solução.
- Testes automatizados.
- Testes de integração pertinentes, se houver infraestrutura disponível.
- Análise estática ou lint, se configurados.

Não afirme que um teste passou se ele não foi executado.

Se algum teste falhar, investigue se a falha foi causada pela implementação ou se já existia anteriormente. Corrija os problemas dentro do escopo e documente os bloqueios restantes.

## 13. Relatório obrigatório ao concluir

Retorne um resumo com:

1. Entidades, status e regras de negócio identificados.
2. Endpoints criados ou reutilizados.
3. Contratos de request e response.
4. Arquivos criados e alterados.
5. Critérios de inclusão e exclusão de pedidos.
6. Como a agregação por produto/SKU foi implementada.
7. Como os pedidos aguardando pagamento são identificados.
8. Como os dados de contato são obtidos e normalizados.
9. Testes executados e resultados.
10. Resultado do build.
11. Migrações realizadas, se houver, e justificativa.
12. Limitações ou dívidas técnicas restantes.
13. Confirmação de que checkout, pagamentos, estoque e demais módulos existentes não tiveram seu comportamento alterado intencionalmente.

### Critério de aceite

O backend estará pronto quando o Admin conseguir consultar a demanda confirmada agrupada por SKU, abrir o detalhamento dos pedidos que formam essa demanda e consultar separadamente pedidos ativos aguardando pagamento, incluindo os dados de contato disponíveis, com autorização administrativa, filtros, paginação, testes e contrato documentado.

A implementação não pode contabilizar pedidos pendentes como vendas confirmadas nem alterar o fluxo atual de pedidos, pagamentos ou estoque.