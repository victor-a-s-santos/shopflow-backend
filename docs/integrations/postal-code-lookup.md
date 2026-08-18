# Postal code lookup (Brazil)

## Endpoint

```http
GET /api/integrations/postal-code/br/{cep}
```

- **Auth:** anonymous (guest checkout)
- **CSRF:** not required (GET)
- **Rate limit:** policy `postal-code-lookup` (`PostalCodeLookup:RateLimitPerMinute`, default 60/min/IP; Dev bumps to ≥100)

Accepts CEP with or without mask (`02310000` / `02310-000`). Non-digit characters are stripped; must yield exactly 8 digits.

### 400 — invalid CEP

`ValidationProblemDetails` with field `cep`. Provider is **not** called.

### 200 — found

```json
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
```

### 200 — not found

```json
{
  "postalCode": "02310-000",
  "found": false,
  "country": "BR",
  "source": "ViaCep"
}
```

### 503 — provider unavailable

Timeout, HTTP failure, or `PostalCodeLookup:Enabled=false`. Checkout must allow **manual** address entry.

## Architecture

| Layer | Responsibility |
|-------|----------------|
| HttpApi `IntegrationsEndpoints` | Validate CEP, map HTTP, rate limit |
| Application `IPostalCodeLookupService` | Port |
| Infrastructure `ViaCepPostalCodeLookupService` | ViaCEP HTTP client |

Frontend calls **only** Shopflow (`/api/integrations/postal-code/br/{cep}`). It must not call ViaCEP/BrasilAPI/Correios in the browser.

## Config

```json
"PostalCodeLookup": {
  "Enabled": true,
  "Provider": "ViaCep",
  "BaseUrl": "https://viacep.com.br",
  "TimeoutSeconds": 5,
  "RateLimitPerMinute": 60
}
```

Env: `PostalCodeLookup__Enabled`, `PostalCodeLookup__Provider`, etc.

Provider name is configurable for future swaps; MVP implementation is ViaCEP only.
