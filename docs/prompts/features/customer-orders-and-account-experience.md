# Shopflow — Experiência de pedidos e acompanhamento do cliente

## 1. Contexto

O Shopflow possui atualmente:

* checkout como convidado;
* criação de `CheckoutSession`;
* criação de pedido a partir da sessão;
* pagamento Pix;
* expiração de checkout, pedido e pagamento;
* área visual de cliente no frontend;
* listagem e detalhe de pedidos parcialmente integrados;
* autenticação administrativa separada;
* autenticação real de cliente ainda não implementada.

A experiência atual apresenta alguns problemas:

1. O botão “Acompanhar meu pedido” não abre uma experiência real de acompanhamento.
2. O GUID interno do pedido é apresentado ao consumidor.
3. Pedido e pagamento podem aparecer simultaneamente como “Pago”.
4. A expiração do pagamento continua sendo exibida mesmo depois da aprovação.
5. Campos técnicos, como provider e identificadores internos, aparecem na interface.
6. Perfil e endereços são apenas demonstrativos e ainda não possuem persistência.
7. Os filtros da listagem de pedidos precisam de contratos claros e testes.
8. O acompanhamento de compra convidada precisa ser seguro e não pode depender apenas do ID do pedido.

## 2. Objetivo desta feature

Preparar o backend para oferecer:

* número público amigável do pedido;
* consulta segura de pedido por comprador convidado;
* projeção clara do status geral do pedido;
* separação entre status do pedido e status do pagamento;
* DTOs adequados ao consumidor;
* filtros consistentes para histórico de pedidos;
* base segura para futura vinculação do pedido a um cliente autenticado;
* compatibilidade com o checkout e o fluxo Pix atuais.

A autenticação completa de cliente, o perfil persistido e o CRUD de endereços não fazem parte da implementação imediata desta etapa. Esses elementos devem apenas ser considerados na modelagem para evitar retrabalho.

## 3. Diagnóstico obrigatório antes da implementação

Antes de alterar o código:

1. Localize as entidades, comandos, handlers, queries, endpoints, DTOs e testes relacionados a:

   * `Order`;
   * `CheckoutSession`;
   * `PixPayment`;
   * criação de pedido;
   * consulta de pedido;
   * expiração;
   * filtros e paginação;
   * autenticação ou identidade de cliente, caso já exista alguma base.

2. Identifique:

   * estados atuais de pedido e pagamento;
   * contratos consumidos pelo frontend;
   * possíveis breaking changes;
   * migrations existentes;
   * como o pedido é associado ao e-mail do checkout;
   * quais endpoints estão públicos;
   * quais endpoints expõem GUIDs sem uma credencial adicional;
   * quais testes serão afetados.

3. Apresente um plano curto de alteração antes de editar.

Não presuma nomes de classes ou estruturas. Adapte a solução à arquitetura e às convenções já existentes no projeto.

## 4. Decisões de domínio

### 4.1 Identificador interno

O pedido deve continuar usando um GUID interno:

```text
Order.Id
```

Esse identificador será usado internamente, em relacionamentos, logs, suporte e operações administrativas.

Ele não deve ser a identificação principal mostrada ao consumidor.

### 4.2 Número público do pedido

Adicionar ao pedido um número público amigável e imutável:

```text
Order.OrderNumber
```

Requisitos:

* único;
* imutável;
* indexado;
* atribuído no momento da criação do pedido;
* adequado para exibição ao consumidor;
* gerado de forma segura em cenários concorrentes;
* sem substituir o GUID interno;
* sem expor contagem sensível, caso a estratégia adotada considere isso relevante.

Exibição esperada:

```text
Pedido #10013
```

Analise a melhor estratégia compatível com PostgreSQL e EF Core, como sequence do banco ou outra solução transacional confiável. Evite geração baseada em `Max + 1`.

### 4.3 Acesso público para compra convidada

Adicionar uma credencial segura para acompanhamento do pedido convidado.

O token deve:

* ter entropia criptográfica adequada;
* ser impossível de adivinhar por enumeração;
* estar vinculado a apenas um pedido;
* não depender somente do GUID ou do número público;
* poder ser rotacionado ou revogado futuramente;
* não aparecer em logs;
* não ser retornado em consultas administrativas ou listagens comuns;
* ser entregue somente no fluxo autorizado de criação/recuperação do pedido convidado.

Avalie armazenar somente o hash do token no banco.

O endpoint público deve usar uma rota equivalente a:

```text
GET /api/orders/public/{orderNumber}?token={token}
```

O formato final deve seguir as convenções existentes da API.

Requisitos de segurança:

* pedido inexistente e token inválido não devem facilitar enumeração;
* não permitir consulta pública somente pelo GUID;
* não retornar dados internos ou administrativos;
* aplicar proteção contra abuso conforme os mecanismos disponíveis no projeto;
* nunca permitir acesso a outro pedido por alteração do número na URL;
* não expor o token em logs ou mensagens de erro.

Se o uso de token em query string for mantido no MVP, documentar o risco e as medidas de mitigação. Avaliar também header ou segmento opaco, desde que o fluxo continue utilizável pelo navegador e por links enviados por e-mail.

### 4.4 Futuro cliente autenticado

Preparar a modelagem para que, futuramente, o pedido possa conter:

```text
Order.CustomerId?
```

A implementação completa de `IdentityCustomer` não faz parte desta etapa.

Não criar associação automática de pedidos apenas por coincidência de e-mail. A vinculação futura de compras convidadas deverá exigir validação da titularidade do e-mail.

## 5. Status do pedido e do pagamento

Pedido e pagamento são conceitos distintos.

### 5.1 Status técnico do pedido

Preservar os estados de domínio necessários ao processo atual. Se o domínio ainda não possui estados logísticos, não inventar transições que não possam ser realmente persistidas.

### 5.2 Status técnico do pagamento

O pagamento Pix deve continuar com seu ciclo próprio, por exemplo:

* Pending;
* Paid ou Approved;
* Expired;
* Canceled;
* Refunded, somente se já fizer parte do domínio ou for necessário no fluxo atual.

Não adicionar estados sem comportamento correspondente.

### 5.3 Status geral apresentado ao cliente

Criar no DTO uma projeção de status adequada ao consumidor, sem alterar indevidamente os estados internos.

Exemplos de códigos públicos:

```text
AwaitingPayment
Confirmed
Preparing
Shipped
Delivered
Canceled
```

Mapeamento inicial esperado:

| Condição interna                      | Status público                        |
| ------------------------------------- | ------------------------------------- |
| Pedido pendente e pagamento pendente  | AwaitingPayment                       |
| Pagamento aprovado                    | Confirmed                             |
| Pedido cancelado                      | Canceled                              |
| Pedido expirado ou pagamento expirado | Expired ou código público equivalente |

Estados como `Preparing`, `Shipped` e `Delivered` só devem ser retornados se o domínio realmente suportá-los.

A API deve retornar códigos estáveis. A tradução para português pertence ao frontend.

## 6. Contratos para o frontend

### 6.1 Resumo do pedido

A listagem deve receber somente dados necessários, como:

```json
{
  "id": "guid-interno-se-realmente-necessario",
  "orderNumber": "10013",
  "createdAt": "2026-07-26T12:00:00Z",
  "customerStatus": "Confirmed",
  "paymentStatus": "Approved",
  "total": 179.90,
  "currency": "BRL",
  "itemsCount": 2,
  "previewImageUrl": "https://..."
}
```

Reavaliar se o GUID precisa ser exposto nesse DTO.

### 6.2 Detalhe do pedido

O detalhe voltado ao cliente deve oferecer:

* `orderNumber`;
* data de criação;
* status geral;
* itens;
* produto;
* imagem;
* variação;
* quantidade;
* valor unitário;
* subtotal;
* desconto, quando aplicável;
* frete, quando aplicável;
* total;
* endereço de entrega;
* método de pagamento;
* status do pagamento;
* data de aprovação do pagamento;
* prazo para pagamento apenas se estiver pendente;
* informações de entrega ou rastreamento somente quando existentes;
* linha do tempo baseada em eventos reais disponíveis.

Não expor desnecessariamente:

* nome técnico do provider;
* IDs internos;
* detalhes operacionais;
* tokens;
* hashes;
* erros do gateway;
* campos de infraestrutura.

### 6.3 Regras do pagamento

* Se estiver pendente, retornar o prazo de pagamento.
* Se estiver aprovado, retornar a data de aprovação e não apresentar a expiração como informação ativa.
* Se estiver expirado, retornar o estado expirado.
* O método deve ser apresentado como Pix, sem exigir que o frontend interprete o provider técnico.

## 7. Filtros e paginação

Analisar e padronizar os filtros disponíveis para a futura listagem de pedidos do cliente:

* status geral do pedido;
* status do pagamento;
* data inicial;
* data final;
* página;
* tamanho da página;
* ordenação decrescente por data como padrão.

Requisitos:

* validar intervalo de datas;
* documentar inclusão dos limites;
* preservar paginação consistente;
* não retornar pedidos de outro cliente quando a autenticação real for implementada;
* retornar metadados de paginação;
* aceitar combinação de filtros;
* rejeitar valores inválidos com `ProblemDetails`;
* usar códigos públicos, e não traduções em português, nos parâmetros.

Se ainda não existir autenticação real de cliente, não criar um endpoint de listagem pública por e-mail. A listagem completa deverá continuar indisponível até existir uma identidade autenticada e autorização por proprietário.

## 8. Migração e compatibilidade

A implementação deve:

* criar migration para `OrderNumber` e dados relacionados ao token;
* considerar pedidos já existentes;
* definir uma estratégia segura de backfill;
* criar índices e restrições únicas;
* preservar o fluxo atual de checkout;
* preservar a criação de pedido;
* preservar o Pix Fake e a futura integração com Mercado Pago;
* preservar o worker de expiração;
* evitar alterações incompatíveis sem justificativa;
* atualizar OpenAPI e documentação relevante.

Se o backfill exigir uma etapa especial, documentar exatamente como executar.

## 9. Testes obrigatórios

Criar ou atualizar testes unitários, integração e segurança cobrindo:

1. criação de pedido com `OrderNumber`;
2. unicidade em criações concorrentes;
3. geração do token público;
4. token não armazenado em texto puro, se essa estratégia for adotada;
5. consulta pública com token válido;
6. rejeição de token inválido;
7. rejeição de token pertencente a outro pedido;
8. resposta indistinguível para pedido inexistente e token inválido, quando aplicável;
9. ausência do token nos DTOs comuns;
10. projeção do status público;
11. pagamento pendente com prazo;
12. pagamento aprovado sem expiração ativa;
13. pagamento expirado;
14. filtros combinados;
15. paginação;
16. intervalo de datas inválido;
17. compatibilidade com criação de pedido e checkout;
18. compatibilidade com o worker de expiração.

## 10. Fora do escopo imediato

Não implementar nesta etapa:

* cadastro e login reais do cliente;
* edição de perfil;
* CRUD de endereços;
* associação automática por e-mail;
* logística completa;
* integração com transportadora;
* rastreamento real;
* reembolso, se ainda não existir no domínio;
* notificações por e-mail;
* mudanças visuais no frontend.

Entretanto, registrar dependências e decisões que afetarão essas funcionalidades futuras.

## 11. Critérios de aceite

A feature estará concluída quando:

* todo novo pedido possuir número público amigável;
* o número for único e seguro em concorrência;
* o comprador convidado conseguir acessar somente seu pedido usando uma credencial segura;
* o GUID sozinho não permitir acesso público;
* status do pedido e pagamento estiverem separados nos contratos;
* o prazo de pagamento aparecer somente quando fizer sentido;
* os DTOs não vazarem detalhes internos;
* migrations estiverem criadas e validadas;
* OpenAPI estiver atualizado;
* todos os testes relevantes passarem;
* o checkout atual continuar funcionando;
* as decisões futuras sobre cliente autenticado estiverem documentadas.

## 12. Entrega esperada do Cursor

Ao concluir, informar:

1. diagnóstico do código encontrado;
2. decisões técnicas tomadas;
3. arquivos criados e alterados;
4. migrations adicionadas;
5. endpoints criados ou modificados;
6. exemplos de request e response;
7. riscos ou pendências;
8. testes executados e resultados;
9. alterações necessárias no frontend;
10. itens que permaneceram fora do escopo.
