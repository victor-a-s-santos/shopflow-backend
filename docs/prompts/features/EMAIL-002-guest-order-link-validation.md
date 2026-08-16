nvestigue o bug do link de acompanhamento enviado no e-mail OrderCreated.

Cenário real em TESTE:

pedido: 10022
OrderId: localize pelo número do pedido no banco/código, se necessário
e-mail foi enviado pela nova arquitetura orders.email_intents → notifications.email_outbox → Brevo
URL recebida tem formato /pedido/10022?t=<guestAccessToken>
ao abrir, o frontend exibe: “Não foi possível abrir este pedido. O link pode ter expirado ou ser inválido.”

Não faça deploy e não altere a VPS.

Rastreie ponta a ponta:
CreateOrderFromCheckoutSessionCommandHandler
→ geração do guest access token
→ hash armazenado no Order
→ OrderEmailIntent.PayloadJson
→ dispatcher
→ EmailOutbox
→ template OrderCreated
→ URL final
→ endpoint público usado pelo frontend (GET /api/orders/public/{orderNumber} e X-ORDER-ACCESS, ou contrato atual equivalente).

Verifique principalmente:

se o raw token colocado no PayloadJson é exatamente o token correspondente ao hash persistido no pedido;
se existe geração de um segundo token em algum ponto;
se o token é truncado, serializado, escapado, URL-encoded ou URL-decoded incorretamente;
se o template adiciona/remove caracteres;
se o frontend lê ?t= corretamente e envia exatamente esse valor em X-ORDER-ACCESS;
se a validação backend aplica o mesmo algoritmo/hash usado na criação;
se a introdução de orders.email_intents alterou a ordem de geração/persistência do token;
se o problema ocorre apenas no link do e-mail ou também no link retornado imediatamente após o checkout;
se expiração do token pode explicar o caso;
se caracteres como +, /, =, _ ou - podem estar sendo modificados na URL.

Segurança: não imprima o token completo em logs ou no relatório. Se precisar comparar valores, compare hashes/fingerprints seguros.

Crie testes de regressão que provem:

token gerado no checkout → acesso público funciona;
o mesmo token persistido na intent → dispatcher → outbox/template → extraído da URL → acesso público funciona;
serialização/URL encoding preserva o token;
token diferente continua retornando acesso negado.

Corrija somente se encontrar a causa com evidência.

Ao final, informe:

causa raiz;
ponto exato onde o token divergia;
arquivos alterados;
testes adicionados;
resultado de build/test;
se pedidos/e-mails já emitidos permanecem inválidos ou continuam recuperáveis;
procedimento seguro para validar com um novo pedido em TESTE.

Documente diagnóstico e correção em docs/features/EMAIL-002-guest-order-link-validation.md.