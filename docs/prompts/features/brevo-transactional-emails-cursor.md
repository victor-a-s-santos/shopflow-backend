Você está atuando como backend engineer sênior do projeto Shopflow, especialista em .NET, Clean Architecture, DDD, transactional email, Brevo, customer auth, orders, payments, fulfillment, background workers, templates e produção.

Objetivo:
Implementar comunicações transacionais por e-mail usando Brevo.

Contexto:
Antes da versão de produção, o Shopflow precisa enviar e-mails reais para:
- cadastro/confirmar conta;
- esqueci minha senha;
- novo pedido;
- pagamento confirmado;
- pedido enviado;
- pedido entregue;
- movimentações de remessa, quando aplicável.

Decisão:
Usar Brevo como provider de e-mail transacional.

Não implementar marketing automation.
Não implementar newsletter.
Não implementar SMS.
Não implementar WhatsApp Business API.
Não implementar chat.

==================================================
1. ARQUITETURA
==================================================

Criar abstração:

ITransactionalEmailSender

Provider:
BrevoTransactionalEmailSender

Recomendação:
Criar também camada de aplicação/serviço:

INotificationService
ou
IEmailNotificationService

Objetivo:
Domínio não deve depender diretamente de Brevo.
Handlers/application disparam notificações através de abstração.

Preferência de envio:
- assíncrono via outbox/worker, se já existir padrão;
- se não houver outbox, criar estrutura simples e segura;
- evitar bloquear checkout/pedido por falha temporária de e-mail.

Se outbox for grande demais:
- implementar envio direto apenas para auth tokens críticos;
- documentar outbox como P1.
Mas recomendação para produção:
- usar outbox para eventos de pedido/pagamento/fulfillment.

==================================================
2. CONFIGURAÇÃO
==================================================

Adicionar configuração:

Brevo:
  Enabled: true
  BaseUrl: "https://api.brevo.com"
  ApiKey: ""
  SenderName: "Vip Assessoria"
  SenderEmail: "no-reply@seudominio.com.br"
  ReplyToEmail: "atendimento@seudominio.com.br"
  SandboxMode: true
  TimeoutSeconds: 10

Templates:
  ConfirmEmailTemplateId:
  ResetPasswordTemplateId:
  OrderCreatedTemplateId:
  PaymentConfirmedTemplateId:
  OrderShippedTemplateId:
  OrderDeliveredTemplateId:
  DeliveryBatchShippedTemplateId:
  DeliveryBatchDeliveredTemplateId:

Decisão de templates:
Escolher uma abordagem e documentar:

A) Templates HTML no código
- mais rápido para MVP;
- usa htmlContent no envio.

B) Templates no Brevo por templateId
- melhor para operação/marketing;
- exige criar templates no painel Brevo.

Recomendação inicial:
- MVP pode usar templates HTML no código com layout simples.
- Manter suporte opcional a templateId futuro.

==================================================
3. EVENTOS / E-MAILS
==================================================

Implementar e-mails:

1. Cadastro / confirmar conta
Quando cliente se registra:
- enviar link de confirmação, se fluxo já existir.
- assunto: "Confirme seu cadastro"

2. Esqueci minha senha
Quando cliente solicita reset:
- enviar link de redefinição.
- assunto: "Redefinição de senha"

3. Novo pedido
Quando pedido é criado:
- enviar para e-mail do cliente/convidado.
- assunto: "Recebemos seu pedido #{orderNumber}"
- incluir:
  - orderNumber;
  - total;
  - status aguardando pagamento, se Pix pendente;
  - link seguro para acompanhar pedido.

4. Pagamento confirmado
Quando pedido muda para Paid:
- assunto: "Pagamento confirmado — Pedido #{orderNumber}"
- incluir:
  - orderNumber;
  - total;
  - resumo;
  - previsão/preferência de entrega, se houver;
  - link seguro.

5. Pedido enviado
Quando pedido individual vira Shipped:
- assunto: "Seu pedido #{orderNumber} foi enviado"
- incluir:
  - método final;
  - trackingCode/referência, se houver;
  - link seguro.

6. Pedido entregue
Quando pedido individual vira Delivered:
- assunto: "Seu pedido #{orderNumber} foi entregue"

7. Remessa enviada
Quando DeliveryBatch vira Shipped:
- enviar e-mail por pedido ou um consolidado por cliente.
- Recomendação MVP:
  - enviar por pedido, usando orderNumber, para evitar novo contrato customer batch.
  - evitar vazar batch interna se decisão atual é não expor batch ao customer.
- assunto: "Seu pedido #{orderNumber} foi enviado"

8. Remessa entregue
Mesmo padrão:
- "Seu pedido #{orderNumber} foi entregue"

Importante:
- não enviar internalOrderNote.
- não enviar dados internos da remessa.
- não enviar GUID interno.
- não enviar hashes/tokens indevidos em logs.

==================================================
4. LINKS SEGUROS
==================================================

Para customer logado:
- link para /account/orders/{orderId} ou rota segura existente.

Para guest:
- usar mecanismo de guest order access existente.
- Se o backend já gera guestAccessToken no momento da criação do pedido, avaliar como o e-mail consegue montar link seguro.
- Não inventar token novo sem design.
- Não logar token.
- Não colocar token em logs.

Se não for possível gerar link seguro para guest no momento certo:
- enviar e-mail sem link no primeiro momento;
- documentar pendência.
Mas ideal:
- e-mail do novo pedido contém link seguro para acompanhamento.

==================================================
5. OUTBOX / IDEMPOTÊNCIA
==================================================

Evitar duplicidade de e-mails.

Criar tabela ou mecanismo:

notifications.email_outbox

Campos sugeridos:
- Id
- Type
- RecipientEmail
- RecipientName
- Subject
- PayloadJson
- Status
- Attempts
- LastError
- ProviderMessageId
- CreatedAt
- SentAt
- NextAttemptAt
- IdempotencyKey

IdempotencyKey exemplos:
- order:{orderId}:created
- order:{orderId}:paid
- order:{orderId}:shipped
- order:{orderId}:delivered
- customer:{customerId}:confirm-email:{tokenHash?}
- customer:{customerId}:reset-password:{tokenHash?}

Worker:
- processa pending;
- retry com backoff simples;
- max attempts;
- não trava API se Brevo falhar.

Se criar outbox for muito amplo, justificar.
Mas para produção, outbox é fortemente recomendado.

==================================================
6. BREVO PROVIDER
==================================================

Implementar envio via Brevo Transactional Email API.

Regras:
- usar HttpClientFactory;
- header api-key;
- timeout;
- tratar 4xx/5xx;
- mapear provider messageId;
- nunca logar API key;
- sandbox mode configurável;
- logs controlados.

Payload mínimo:
- sender
- to
- subject
- htmlContent
- textContent opcional
- replyTo opcional
- params opcional se usar templateId

==================================================
7. TEMPLATES / CONTEÚDO
==================================================

Criar templates simples e profissionais.

Padrão:
- logo/nome da loja;
- saudação;
- mensagem objetiva;
- CTA;
- resumo do pedido quando aplicável;
- rodapé com aviso.

E-mails devem estar em PT-BR.

Não incluir:
- nota interna;
- dados técnicos;
- GUID;
- informações operacionais privadas;
- dados de pagamento sensíveis.

==================================================
8. ADMIN / LOGS
==================================================

Nesta fase não precisa criar UI admin para logs de e-mail.

Mas deve existir logging técnico:
- e-mail enfileirado;
- e-mail enviado;
- falha de provider;
- retries.

Não logar:
- API key;
- tokens;
- conteúdo sensível completo.

==================================================
9. TESTES
==================================================

Unit tests:
1. Brevo payload correto.
2. ApiKey não aparece em logs.
3. Confirm email template renderiza link.
4. Reset password template renderiza link.
5. Order created inclui orderNumber.
6. Payment confirmed inclui orderNumber.
7. Shipped inclui tracking quando existe.
8. Delivered renderiza corretamente.
9. InternalOrderNote nunca aparece.
10. DeliveryBatch não vaza dados internos.
11. Outbox cria idempotencyKey correta.
12. Worker não envia duplicado.
13. Provider failure agenda retry.
14. Max attempts marca failed.
15. Brevo disabled não envia e registra skip/pendência conforme decisão.

Integration/HTTP, se houver:
- register dispara e-mail de confirmação.
- forgot password dispara e-mail.
- order created enfileira e-mail.
- paid enfileira e-mail.
- ship/deliver enfileira e-mail.

==================================================
10. DOCUMENTAÇÃO
==================================================

Criar/atualizar:

- docs/integrations/brevo-transactional-emails.md
- docs/orders/order-emails.md
- docs/customer-auth/email-flows.md
- docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md
- deploy/.env.example
- deploy/.env.prod.example, se existir

Documentar:
- env vars;
- sandbox mode;
- sender/reply-to;
- eventos enviados;
- templates;
- outbox/retries;
- segurança;
- checklist de validação em TESTE e produção.

==================================================
11. NÃO FAZER
==================================================

Não implementar:
- marketing campaigns;
- newsletter;
- SMS;
- WhatsApp Business;
- chat;
- editor de template admin;
- tracking UI;
- unsubscribe marketing.

Não alterar:
- Pix provider;
- DeliveryBatch business rules;
- Inventory;
- Catalog;
- R2 images;
- frontend, salvo docs/contrato se necessário.

==================================================
12. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Configuração Brevo criada.
3. Provider Brevo criado.
4. Outbox criada ou justificativa se não criada.
5. Eventos/e-mails implementados.
6. Templates criados.
7. Como links seguros funcionam.
8. Como evita duplicidade.
9. Testes criados/alterados.
10. Resultado dotnet build/test.
11. Docs atualizadas.
12. Pendências para produção.

Critérios de aceite:
- cadastro envia e-mail.
- esqueci senha envia e-mail.
- novo pedido envia e-mail.
- pagamento confirmado envia e-mail.
- pedido enviado/entregue envia e-mail.
- remessa enviada/entregue notifica pedidos envolvidos.
- falha Brevo não quebra checkout/pedido.
- internal notes não vazam.
- API key não vaza.
- build/testes passam.