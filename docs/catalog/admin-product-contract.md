# Admin Product — contrato oficial (backend)

> Última atualização: 2026-07-26. Código em `apps/api` prevalece se divergir.
> Frontend: `apps/web/docs/admin-product-form.md` — pendência enviar/hidratar `description` + `isActive` no create.

## Modelo de persistência

O admin **não** tem um único endpoint “salvar produto completo”. O fluxo oficial é sequencial:

| Passo | Método | Rota | Auth |
|-------|--------|------|------|
| 1. Criar produto (shell) | `POST` | `/api/catalog/products/variant` | Backoffice |
| 2. Dados básicos | `PUT` | `/api/catalog/products/{id}` | Backoffice |
| 3. Adicionar variante | `POST` | `/api/catalog/products/{productId}/variants` | Backoffice |
| 4. Atualizar variante | `PUT` | `/api/catalog/products/{productId}/variants/{skuId}` | Backoffice |
| 5. Inativar variante | `PUT` … variants … (`active: false`) | Backoffice |
| 6. Excluir variante* | `DELETE` | `/api/catalog/products/{productId}/variants/{skuId}` | Backoffice |
| 7. Upload imagem | `POST` | `/api/catalog/products/{id}/images` (multipart) | Backoffice |
| 8. Remover imagem | `DELETE` | `/api/catalog/products/{productId}/images/{imageId}` | Backoffice |
| 9. Definir principal | `POST` | `/api/catalog/products/{productId}/images/{imageId}/primary` | Backoffice |

\*Hard delete só se a SKU **não** tiver estoque/reserva/movimentação/pedido. Caso contrário → **409** pedindo inativação.

### Risco de persistência parcial

Cada passo grava na própria transação. Se o passo 3 falhar após o 1, o produto shell permanece. Validações críticas (preço, atributo, código) ocorrem **antes** do `SaveChanges` de cada comando. Retry de upload pode criar imagem extra — o frontend deve evitar reenviar o mesmo arquivo sem checar o detalhe do produto.

## Produto — body

| Endpoint | Campos | Notas |
|----------|--------|-------|
| `POST .../products/variant` | `name`, `slug?`, `categoryId?`, `description?`, `isActive?`, `isFeatured?`, `displayOrder?` | `isActive` omitido → **true**; `false` explícito persiste inativo. `description` vazia → `null` (máx. 4000). |
| `PUT .../products/{id}` | `name`, `slug?`, `categoryId?`, `isActive`, `description?`, `display?` | `isActive` é obrigatório no PUT (não ignora `false`). `description` omitida/`null` → **preserva**; `""` → limpa; texto → salva. `display` omitido → preserva featured/order. |

Detail (`GET /api/catalog/products/{id}` e by-slug) retorna `description` + `isActive` em `ProductDetailedDto`. Listagens (pública/admin) **não** incluem description no card/tabela.

Produto inativo: some da listagem/by-slug públicos; permanece na listagem admin. Inativar também via `POST .../deactivate`.

## Variante (SKU) — body

```json
{
  "code": "CONJUNTO-FLORES-ROSA-M",
  "regularPrice": 199.90,
  "promotionalPrice": 149.90,
  "attributes": [ /* ver product-attributes-contract.md */ ],
  "active": true,
  "salesRule": { /* opcional — ver sales-rules-contract.md; ausente = Unit */ }
}
```

- Wire write: **`active`** (não `isActive`). Read DTO: `isActive`.
- Sem campo `description` na SKU.
- `code` vazio/null/whitespace → backend gera código único (ver `sku-code-rules.md`).
- `code` informado → normalizado (maiúsculas, hífens) + unicidade **por produto**.
- Preços: decimal invariável, máx. 2 casas; promo **estritamente menor** que regular se informada.
- `salesRule` ausente → `Unit` (min=1, step=1) **no create**. No **update**, ausente **preserva** a regra existente; para resetar, envie `salesMode: Unit` explicitamente (`docs/catalog/sales-rules-contract.md`).
- Leitura sempre devolve `salesRule` normalizada; `salesRuleDisplay` só em Fixed/Assorted.

## Erros (ProblemDetails)

HTTP 400 exemplo:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/catalog/products/.../variants",
  "errors": {
    "regularPrice": ["O preço regular deve ser maior que zero."],
    "attributes[0].customName": ["Informe attributeValueDefinitionId ... ou customName ..."]
  },
  "traceId": "00-..."
}
```

HTTP 409 (código duplicado / SKU protegida):

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "O código “X” já está sendo usado por outra variação deste produto.",
  "errorCode": "SKU_CODE_DUPLICATE",
  "errors": { "code": ["..."] },
  "traceId": "..."
}
```

500 inesperado: título genérico + `traceId`, **sem** stack trace no body.
