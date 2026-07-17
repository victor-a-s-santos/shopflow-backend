# Product attributes — contrato de SKU

> Frontend (`apps/web`) envia este contrato via `normalizeSkuAttributes` / `SkuEditor`. Livre `{ customName, customValue }` sem `attributeDefinitionId` é rejeitado.

## Tipos

| Tipo | Payload | Persistência |
|------|---------|--------------|
| Valor global predefinido | `attributeDefinitionId` + `attributeValueDefinitionId` | FK nas definições; `customName`/`customValue` vazios |
| Valor personalizado da definição | `attributeDefinitionId` + `customName` | Exige `AllowCustomValues=true` na definição; `customName` = texto do valor (ex.: `"Variadas"`) |
| Livre sem definição | **Não aceito** na API admin | Legado interno/seed: `SkuAttribute.FromCustom(name, value)` |

## Exemplos

Predefinido:

```json
{
  "attributeDefinitionId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "attributeValueDefinitionId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
}
```

Personalizado:

```json
{
  "attributeDefinitionId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "customName": "Variadas"
}
```

## Regras

- `attributeDefinitionId` obrigatório e existente.
- XOR: valor predefinido **ou** `customName` — nunca ambos.
- `attributeValueDefinitionId` deve pertencer à definição.
- Uma SKU não pode repetir a mesma `attributeDefinitionId`.
- `customName`: trim, 1…64 chars.
- Campo legado `customValue` no DTO: ignorado no contrato novo; não usar com valor predefinido.

## Leitura (admin detail)

`SkuAttributeDto`: `attributeDefinitionId`, `attributeValueDefinitionId`, `customName`, `customValue`, `definitionName`, `valueName`.

- Predefinido: use `definitionName` + `valueName`.
- Custom de definição: use `definitionName` + `customName`.
