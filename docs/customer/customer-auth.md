# Customer auth (resumo)

## Endpoints

| Método | Rota | Notas |
|--------|------|--------|
| POST | `/api/auth/customer/register` | Anônimo; senha forte obrigatória |
| POST | `/api/auth/customer/login` | Cookie `CustomerCookie` |
| POST | `/api/auth/customer/logout` | CSRF |
| GET | `/api/auth/customer/me` | Cookie |
| POST | `/api/auth/customer/forgot-password` | Mensagem genérica |
| POST | `/api/auth/customer/reset-password` | Mesma política de senha do register |
| POST | `/api/auth/customer/confirm-email` | Token |

## Política de senha

Backend é a fonte da verdade (`IdentityOptions` + FluentValidation):

- mín. 8 caracteres
- maiúscula + minúscula + dígito + caractere especial

Códigos úteis: `PASSWORD_TOO_WEAK`, `PASSWORD_TOO_SHORT`, `PASSWORD_REQUIRES_DIGIT`, `PASSWORD_REQUIRES_UPPERCASE`, `PASSWORD_REQUIRES_LOWERCASE`, `PASSWORD_REQUIRES_SPECIAL`.

Dev/test exemplo: `Shopflow@123` (nunca secret de produção).

Aprovação comercial: ver [`customer-approval.md`](./customer-approval.md).
