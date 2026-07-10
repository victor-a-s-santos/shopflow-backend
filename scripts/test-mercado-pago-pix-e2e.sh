#!/usr/bin/env bash
set -euo pipefail

API="${API_BASE:-http://localhost:5127}"
SKU_ID="${SKU_ID:-19c8e6b9-6ed0-4bc1-9a20-f9516561fad8}"

echo "=== 1. Checkout session ==="
SESSION=$(curl -sf -X POST "$API/api/checkout/sessions" \
  -H "Content-Type: application/json" \
  -d "{
    \"customer\": {
      \"fullName\": \"Victor Teste\",
      \"email\": \"test_user_779596194738373669@testuser.com\",
      \"phone\": \"11999990000\"
    },
    \"address\": {
      \"zipCode\": \"01001000\",
      \"street\": \"Rua Teste\",
      \"number\": \"100\",
      \"complement\": null,
      \"neighborhood\": \"Centro\",
      \"city\": \"São Paulo\",
      \"state\": \"SP\"
    },
    \"items\": [{ \"skuId\": \"$SKU_ID\", \"quantity\": 1 }]
  }")

SESSION_ID=$(echo "$SESSION" | python3 -c "import sys,json; print(json.load(sys.stdin)['checkoutSessionId'])")
echo "checkoutSessionId: $SESSION_ID"

echo ""
echo "=== 2. Order ==="
ORDER=$(curl -sf -X POST "$API/api/orders/from-checkout-session" \
  -H "Content-Type: application/json" \
  -d "{\"checkoutSessionId\": \"$SESSION_ID\"}")

ORDER_ID=$(echo "$ORDER" | python3 -c "import sys,json; print(json.load(sys.stdin)['orderId'])")
ORDER_STATUS=$(echo "$ORDER" | python3 -c "import sys,json; print(json.load(sys.stdin)['status'])")
echo "orderId: $ORDER_ID"
echo "status: $ORDER_STATUS"

echo ""
echo "=== 3. Pix payment (Mercado Pago) ==="
HTTP=$(curl -s -o /tmp/pix-response.json -w "%{http_code}" -X POST "$API/api/payments/pix/orders/$ORDER_ID")
echo "HTTP: $HTTP"
python3 -m json.tool /tmp/pix-response.json

if [ "$HTTP" = "201" ] || [ "$HTTP" = "200" ]; then
  python3 - <<'PY'
import json, os
with open("/tmp/pix-response.json") as f:
    d = json.load(f)
expected = os.environ.get("EXPECTED_PROVIDER", "MercadoPago")
assert d.get("status") == "Pending", d
assert d.get("provider") == expected, d
if expected == "MercadoPago":
    assert d.get("copyPasteCode"), "copyPasteCode missing"
    assert d.get("qrCodeImageUrl"), "qrCodeImageUrl missing"
print(f"\n✓ SUCCESS: Pix criado (provider={d.get('provider')}, status=Pending)")
PY
else
  echo ""
  echo "✗ FAILED: verifique credenciais Mercado Pago (use TEST- para sandbox)"
  exit 1
fi
