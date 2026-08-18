Você está atuando como engenheiro fullstack sênior do projeto Shopflow, especialista em .NET, React, TypeScript, integrações externas, Clean Architecture, API pública segura, checkout UX e e-commerce brasileiro.

Objetivo:
Corrigir a arquitetura da busca de CEP para que o frontend não chame ViaCEP diretamente no browser. A busca deve passar por uma API de integração do backend Shopflow.

Contexto:
Na última rodada, foi implementada busca CEP ViaCEP direto no checkout/frontend.

Isso funciona, mas contraria a decisão arquitetural do projeto:
- busca CEP deve ser via API de integração do Shopflow;
- frontend não deve conhecer ViaCEP/BrasilAPI/Correios;
- provider externo deve ser detalhe de infraestrutura backend;
- frontend deve chamar apenas API própria do Shopflow.

Decisão:
Implementar endpoint backend:

GET /api/integrations/postal-code/br/{cep}

O frontend deve substituir a chamada direta ao ViaCEP por chamada a esse endpoint.

Não implementar base manual de CEP.
Não manter provider externo acoplado ao frontend.
Não bloquear checkout se a consulta falhar.
Não alterar Delivery/Fulfillment neste prompt.
Não alterar Orders/Checkout além do service de CEP.
Não alterar pagamento/Pix.
Não alterar admin.

==================================================
1. BACKEND — ENDPOINT
==================================================

Criar endpoint público:

GET /api/integrations/postal-code/br/{cep}

Características:
- público, pois checkout convidado precisa usar;
- GET;
- sem CSRF;
- com rate limit;
- valida CEP antes de chamar provider;
- retorna resposta normalizada;
- não retorna payload bruto do provider.

Entrada:
- aceitar CEP com ou sem máscara:
  - 02310000
  - 02310-000

Validação:
- remover caracteres não numéricos;
- exigir exatamente 8 dígitos;
- se inválido, retornar ProblemDetails 400;
- não chamar provider se inválido.

Resposta encontrada:

{
  "postalCode": "02310-000",
  "street": "Rua Exemplo",
  "neighborhood": "Santana",
  "city": "São Paulo",
  "state": "SP",
  "country": "BR",
  "found": true,
  "source": "ViaCep"
}

Resposta não encontrada:
Preferência:
200 com:

{
  "postalCode": "02310-000",
  "found": false
}

Se o projeto preferir 404, documentar e tratar no frontend.

==================================================
2. BACKEND — SERVICE / PROVIDER
==================================================

Criar interface de aplicação:

IPostalCodeLookupService

Método:
LookupBrazilPostalCodeAsync(string cep, CancellationToken cancellationToken)

Criar implementação em infraestrutura:

ViaCepPostalCodeLookupService

ou nome equivalente.

Regras:
- provider é configurável;
- Application/API não devem depender diretamente de ViaCEP;
- usar HttpClientFactory;
- timeout curto;
- tratar falhas de rede;
- tratar CEP não encontrado;
- normalizar resposta;
- não vazar payload bruto.

Config sugerida:

PostalCodeLookup:
  Enabled: true
  Provider: "ViaCep"
  BaseUrl: "https://viacep.com.br"
  TimeoutSeconds: 5
  RateLimitPerMinute: 60

Se o projeto usa Options pattern, seguir o padrão existente.

==================================================
3. BACKEND — RATE LIMIT / SEGURANÇA
==================================================

Aplicar rate limit no endpoint.

Sugestão:
- 60 por minuto por IP, ou seguir padrão já usado no projeto.

Não exigir autenticação.

Logs:
- logar falhas de provider de forma controlada;
- não logar dados sensíveis em excesso;
- CEP não é PII crítica, mas tratar com cuidado.

==================================================
4. FRONTEND — REMOVER VIACEP DIRETO
==================================================

Auditar:

- cepLookup.ts
- Checkout.tsx
- services de endereço
- testes de CEP
- docs

Remover qualquer chamada direta a:
- viacep.com.br
- brasilapi.com.br
- provider externo no browser.

Frontend deve chamar somente:

GET /api/integrations/postal-code/br/{cep}

Criar/ajustar service:

postalCodeService.lookupBrazilPostalCode(cep)

ou adaptar `cepLookup.ts` para usar a API do Shopflow.

Comportamento:
- usuário digita CEP;
- ao completar 8 dígitos, chama API Shopflow;
- debounce ou blur conforme já implementado;
- loading discreto;
- se found=true, preencher:
  - rua;
  - bairro;
  - cidade;
  - UF;
- número e complemento continuam manuais;
- usuário pode editar campos preenchidos;
- se found=false:
  “CEP não encontrado. Preencha o endereço manualmente.”
- se API falhar:
  “Não foi possível buscar o CEP agora. Preencha o endereço manualmente.”

Não bloquear checkout em falha de CEP.

==================================================
5. FRONTEND — FORMATAÇÃO
==================================================

Manter formatCepBR na UI.

O valor salvo/enviado deve seguir padrão atual do projeto.

Se hoje o checkout salva com máscara, manter.
Se salva só dígitos, manter.
Não mudar persistência neste prompt sem necessidade.

==================================================
6. TESTES BACKEND
==================================================

Criar/ajustar testes:

1. CEP inválido retorna 400 e não chama provider.
2. CEP com máscara é normalizado.
3. CEP sem máscara é normalizado.
4. Provider encontrado retorna DTO normalizado.
5. Provider não encontrado retorna found=false ou 404 conforme decisão.
6. Falha/timeout do provider retorna erro controlado.
7. Endpoint não exige autenticação.
8. Endpoint aplica rate limit, se houver teste padrão.
9. Não retorna payload bruto do provider.

==================================================
7. TESTES FRONTEND
==================================================

Criar/ajustar testes:

1. lookupBrazilPostalCode chama `/api/integrations/postal-code/br/{cep}`.
2. Não chama ViaCEP direto.
3. CEP válido preenche endereço no checkout.
4. CEP inválido não chama API.
5. found=false mostra mensagem e permite preenchimento manual.
6. Falha da API mostra mensagem e permite preenchimento manual.
7. Número e complemento não são sobrescritos.
8. Usuário pode editar campos preenchidos.
9. formatCepBR continua funcionando.

Atualizar Cypress se houver spec de checkout/CEP:
- interceptar `/api/integrations/postal-code/br/*`;
- validar preenchimento automático;
- validar fallback manual.

==================================================
8. DOCUMENTAÇÃO
==================================================

Atualizar:

Backend:
- docs/integrations/postal-code-lookup.md
- docs/architecture/DELIVERY-FULFILLMENT-DESIGN.md
- docs/ai-context/shopflow-current-state.md
- docs/ai-context/backend-next-actions.md
- docs/ai-context/technical-debt.md

Frontend:
- apps/web/docs/ai-context/api-contracts.md
- apps/web/docs/ai-context/frontend-next-actions.md
- apps/web/docs/ai-context/frontend-technical-debt.md

Documentar:
- frontend não chama provider externo;
- backend centraliza busca CEP;
- provider inicial é detalhe de infraestrutura;
- checkout não bloqueia se busca falhar;
- preenchimento manual continua fallback;
- ViaCEP direto no browser foi removido.

==================================================
9. NÃO FAZER
==================================================

Não implementar:
- Delivery/Fulfillment;
- método de entrega;
- data preferida;
- observação de pedido;
- frete;
- cálculo de entrega;
- tabela manual de CEP;
- cache persistente, salvo se já houver padrão simples;
- múltiplos providers com fallback complexo.

Não alterar:
- Orders;
- PaymentsPix;
- Inventory;
- Product;
- Admin Products;
- Admin Orders.

Não manter:
- chamada direta ViaCEP/BrasilAPI no frontend.

==================================================
10. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Endpoint criado.
2. Arquivos backend alterados.
3. Service/provider criado.
4. Como valida CEP.
5. Como trata CEP não encontrado.
6. Como trata falha do provider.
7. Arquivos frontend alterados.
8. Confirmação de que ViaCEP direto saiu do browser.
9. Testes backend criados/alterados.
10. Testes frontend criados/alterados.
11. Resultado dotnet build/test.
12. Resultado npm run typecheck/build.
13. Cypress criado/alterado e resultado, se executado.
14. Pendências restantes.

Critérios de aceite:
- frontend chama apenas API Shopflow para CEP;
- backend consulta provider externo;
- CEP inválido não chama provider;
- resposta é normalizada;
- falha de busca não bloqueia checkout;
- preenchimento manual continua possível;
- testes passam.