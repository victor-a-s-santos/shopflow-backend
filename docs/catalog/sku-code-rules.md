# SKU Code — regras oficiais

## Escopo de unicidade

**Por produto** (`ProductId` + `Code`), índice único `IX_product_skus_ProductId_Code`.  
O mesmo código **pode** existir em produtos diferentes.

## Geração e normalização

| Entrada | Comportamento |
|---------|---------------|
| `null` / `""` / whitespace na **criação** | Gera código a partir do nome do produto + labels dos atributos; se colidir, sufixo `-2`, `-3`…; fallback `SKU-{8 hex}`. |
| `null` / vazio na **atualização** | **Mantém** o código atual (não regenera). |
| Informado | `Trim` → maiúsculas → espaços viram `-` → remove caracteres fora de `[A-Z0-9-]`. |

Exemplos:

- `Conjunto Flores` + Rosa + M → `CONJUNTO-FLORES-ROSA-M`
- Colisão → `CONJUNTO-FLORES-ROSA-M-2`

O nome do produto **não** é copiado cegamente para todas as variantes: cada SKU recebe sufixos dos seus atributos (ou sufixo numérico / guid).

## Alteração de código

Se a SKU tem estoque on-hand/reservado, movimentações de inventário ou itens de pedido → **409** `SKU_CODE_CHANGE_PROTECTED`.  
Inative a variação se precisar “retirar” do catálogo.

## Concorrência

Pré-checagem no handler + índice único no PostgreSQL. Violação de unicidade em corrida → **409** via `DbUpdateException` mapeada no gateway.
