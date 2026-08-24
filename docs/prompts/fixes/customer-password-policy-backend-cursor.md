Você está atuando como backend engineer sênior do projeto Shopflow, especialista em .NET, ASP.NET Core Identity, Clean Architecture, segurança de autenticação, validação de cadastro e ambientes TESTE/HML/PROD.

Objetivo:
Revisar e endurecer a política de senha para novos usuários customer no Shopflow, sem quebrar seeds, testes automatizados e usuários já existentes.

Contexto:
O Shopflow agora opera em modo StoreAccess Closed para o cliente atual. Novos usuários se cadastram e ficam PendingApproval até aprovação administrativa.

Foi identificado que o cadastro permite senha fraca na experiência real. Para produção, isso não é aceitável.

Decisão:
Para usuários novos via cadastro público em TESTE/HML/PROD, exigir senha forte.

Política mínima recomendada:
- mínimo 8 caracteres;
- pelo menos 1 letra maiúscula;
- pelo menos 1 letra minúscula;
- pelo menos 1 número;
- pelo menos 1 caractere especial.

Não alterar:
- StoreAccess/CustomerApprovalStatus;
- Brevo/outbox;
- Pix;
- Orders;
- Delivery/Remessas;
- R2;
- cookies/policies admin/customer.

Não fundir login admin/customer.

==================================================
1. AUDITORIA
==================================================

Auditar onde a política de senha é definida:

- ASP.NET Core Identity options;
- Customer register handler;
- Admin seed;
- Customer seed, se houver;
- testes integration;
- reset password;
- change password, se existir.

Verificar se hoje existe diferença entre:
- usuário criado por seed;
- usuário criado por teste;
- usuário criado via cadastro público;
- admin seed.

==================================================
2. POLÍTICA DE SENHA
==================================================

Implementar ou garantir para cadastro customer público:

Password:
- RequiredLength = 8
- RequireDigit = true
- RequireLowercase = true
- RequireUppercase = true
- RequireNonAlphanumeric = true

Se o projeto já usa IdentityOptions global:
- avaliar impacto em admin seed/testes;
- ajustar seeds para senha forte;
- ou permitir exceção somente por caminho de seed/test explicitamente isolado.

Atenção:
- não aceitar senha fraca via API pública.
- não confiar apenas no frontend.
- backend deve ser fonte da verdade.

==================================================
3. SEEDS E TESTES
==================================================

Seeds e testes podem usar senha forte padrão, por exemplo:

Shopflow@123

Regras:
- não commitar senha real de produção.
- seed de admin em ambiente real continua via env/secret.
- se houver senha fraca em testes, atualizar para senha forte.
- se houver necessidade de senha simples em teste unitário, isolar no teste sem afetar runtime real.

==================================================
4. PROBLEMDETAILS
==================================================

Quando senha não atender a política, retornar erro amigável.

Exemplo de mensagem:

"A senha deve ter pelo menos 8 caracteres, incluindo letra maiúscula, letra minúscula, número e caractere especial."

ProblemDetails deve mapear campo:

password

Não retornar erro genérico ruim.
Não retornar stack trace.

Codes sugeridos:
- PASSWORD_TOO_WEAK
- PASSWORD_REQUIRES_DIGIT
- PASSWORD_REQUIRES_UPPERCASE
- PASSWORD_REQUIRES_LOWERCASE
- PASSWORD_REQUIRES_SPECIAL
- PASSWORD_TOO_SHORT

Se o Identity já retorna mensagens separadas, normalizar para PT-BR.

==================================================
5. RESET PASSWORD
==================================================

Garantir que redefinição de senha também respeita a mesma política.

Fluxo:
- forgot password envia e-mail;
- reset password com senha fraca deve falhar com mensagem clara;
- reset password com senha forte deve funcionar.

==================================================
6. TESTES
==================================================

Criar/ajustar testes:

1. register com senha fraca retorna 400.
2. register com senha sem maiúscula retorna erro.
3. register com senha sem minúscula retorna erro.
4. register com senha sem número retorna erro.
5. register com senha sem especial retorna erro.
6. register com senha menor que 8 retorna erro.
7. register com senha forte cria customer Pending em loja Closed.
8. Open mode com senha forte cria conforme regra atual.
9. reset password com senha fraca retorna erro.
10. reset password com senha forte funciona.
11. seeds/test users usam senha compatível.
12. mensagens não vazam detalhe técnico.

==================================================
7. DOCUMENTAÇÃO
==================================================

Atualizar:

- docs/customer/customer-approval.md
- docs/customer/customer-auth.md, se existir
- docs/features/STORE-ACCESS-CUSTOMER-APPROVAL.md
- docs/qa/PRE-PRODUCTION-GO-LIVE-CHECKLIST.md
- .env.example/seeds docs, se houver senha exemplo

Documentar:
- política de senha para cadastro público;
- senha exemplo apenas para dev/test;
- produção deve usar secrets.

==================================================
8. RESULTADO ESPERADO
==================================================

Ao final, retornar:

1. Arquivos alterados.
2. Política aplicada.
3. Como seeds/testes foram ajustados.
4. Como ProblemDetails retorna erro.
5. Testes criados/alterados.
6. Resultado dotnet build.
7. Resultado dotnet test.
8. Docs atualizadas.

Critérios de aceite:
- cadastro público não aceita senha fraca.
- reset password não aceita senha fraca.
- senha forte funciona.
- seeds/testes não quebram.
- mensagens aparecem em PT-BR.