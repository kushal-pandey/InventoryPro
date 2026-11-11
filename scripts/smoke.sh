#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${API_BASE_URL:-http://localhost:5080}"
API_BASE_URL="${API_BASE_URL%/}"
ADMIN_EMAIL="${INVENTORYPRO_ADMIN_EMAIL:-}"
ADMIN_PASSWORD="${INVENTORYPRO_ADMIN_PASSWORD:-}"
TOKEN=""
RESPONSE_BODY=""
HTTP_STATUS=""

fail() {
  printf 'FAIL %s\n' "$1" >&2
  exit 1
}

pass() {
  printf 'PASS %s\n' "$1"
}

if ! command -v curl >/dev/null 2>&1; then
  fail "curl is required"
fi
if ! command -v node >/dev/null 2>&1; then
  fail "node is required to parse API responses"
fi
if [[ -z "$ADMIN_EMAIL" || -z "$ADMIN_PASSWORD" ]]; then
  fail "Set INVENTORYPRO_ADMIN_EMAIL and INVENTORYPRO_ADMIN_PASSWORD"
fi

request() {
  local method="$1"
  local path="$2"
  local payload="${3:-}"
  local args=(-sS -X "$method" "$API_BASE_URL$path" -H "Accept: application/json" -w $'\n%{http_code}')
  if [[ -n "$TOKEN" ]]; then
    args+=(-H "Authorization: Bearer $TOKEN")
  fi
  if [[ -n "$payload" ]]; then
    args+=(-H "Content-Type: application/json" --data "$payload")
  fi

  local raw
  raw="$(curl "${args[@]}")" || fail "$method $path request failed"
  HTTP_STATUS="${raw##*$'\n'}"
  RESPONSE_BODY="${raw%$'\n'*}"
}

expect_status() {
  local expected="$1"
  local label="$2"
  if [[ "$HTTP_STATUS" != "$expected" ]]; then
    fail "$label (expected HTTP $expected, got $HTTP_STATUS)"
  fi
  pass "$label"
}

json_value() {
  node -e 'let value=JSON.parse(require("fs").readFileSync(0,"utf8"));
for (const part of process.argv[1].split(".")) value=Array.isArray(value)?value[Number(part)]:value[part];
console.log(value)' "$1" <<<"$RESPONSE_BODY"
}

assert_json_value() {
  local key="$1"
  local expected="$2"
  local label="$3"
  local actual
  actual="$(json_value "$key")" || fail "$label (invalid JSON response)"
  [[ "$actual" == "$expected" ]] || fail "$label (expected $expected, got $actual)"
  pass "$label"
}

RUN_ID="$(node -e 'console.log(require("crypto").randomUUID().replace(/-/g,"").slice(0,12))')"

request POST /api/auth/login "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}"
expect_status 200 "Admin login"
TOKEN="$(json_value token)" || fail "Could not read login token"

request GET /health/ready
expect_status 200 "API readiness"

request POST /api/categories "{\"name\":\"Smoke $RUN_ID\",\"description\":\"API smoke test\"}"
expect_status 201 "Create category"
CATEGORY_ID="$(json_value id)" || fail "Could not read category id"

SKU="SMOKE-$RUN_ID"
request POST /api/products "{\"name\":\"Smoke product $RUN_ID\",\"description\":\"API smoke test\",\"sku\":\"$SKU\",\"price\":10,\"stockQuantity\":10,\"reorderLevel\":2,\"categoryId\":$CATEGORY_ID}"
expect_status 201 "Create product"
PRODUCT_ID="$(json_value id)" || fail "Could not read product id"

request POST /api/customers "{\"fullName\":\"Smoke Customer $RUN_ID\",\"email\":\"smoke-$RUN_ID@example.test\",\"phone\":\"555-0100\",\"address\":\"Smoke test\"}"
expect_status 201 "Create customer"
CUSTOMER_ID="$(json_value id)" || fail "Could not read customer id"

request POST /api/orders "{\"customerId\":$CUSTOMER_ID,\"items\":[{\"productId\":$PRODUCT_ID,\"quantity\":8}]}"
expect_status 201 "Place order"
ORDER_ID="$(json_value id)" || fail "Could not read order id"

request GET "/api/products/$PRODUCT_ID"
expect_status 200 "Read product after order"
assert_json_value stockQuantity 2 "Order deducts eight units"

request GET "/api/insights/reorder-suggestions?limit=200&includeHealthy=true"
expect_status 200 "Read reorder suggestions"
if ! node -e 'const sku=process.argv[1]; const rows=JSON.parse(require("fs").readFileSync(0,"utf8")); process.exit(rows.some(row=>row.sku===sku)?0:1)' "$SKU" <<<"$RESPONSE_BODY"; then
  fail "Reorder suggestions include the new product"
fi
pass "Reorder suggestions include the new product"

request GET /api/insights/summary
expect_status 200 "Read inventory summary"

request PATCH "/api/orders/$ORDER_ID/status" '{"status":4}'
expect_status 200 "Cancel order"

request GET "/api/products/$PRODUCT_ID"
expect_status 200 "Read product after cancellation"
assert_json_value stockQuantity 10 "Cancellation restores all stock"

request GET /api/insights/sales-trend?days=30
expect_status 200 "Read sales trend"

printf 'Smoke test complete against %s\n' "$API_BASE_URL"
