#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${API_BASE_URL:-http://localhost:5080}"
API_BASE_URL="${API_BASE_URL%/}"
ADMIN_EMAIL="${INVENTORYPRO_ADMIN_EMAIL:-}"
ADMIN_PASSWORD="${INVENTORYPRO_ADMIN_PASSWORD:-}"
RUNS="${BENCHMARK_RUNS:-20}"
WARMUPS="${BENCHMARK_WARMUPS:-3}"

fail() {
  printf 'FAIL %s\n' "$1" >&2
  exit 1
}

if ! command -v curl >/dev/null 2>&1 || ! command -v node >/dev/null 2>&1; then
  fail "curl and node are required"
fi
if [[ -z "$ADMIN_EMAIL" || -z "$ADMIN_PASSWORD" ]]; then
  fail "Set INVENTORYPRO_ADMIN_EMAIL and INVENTORYPRO_ADMIN_PASSWORD"
fi
if ! [[ "$RUNS" =~ ^[1-9][0-9]*$ && "$WARMUPS" =~ ^[0-9]+$ ]]; then
  fail "BENCHMARK_RUNS must be positive and BENCHMARK_WARMUPS must be non-negative integers"
fi

WORK_DIR="$(mktemp -d)"
RESULTS="$WORK_DIR/results.tsv"
HEADERS="$WORK_DIR/headers.txt"
LOGIN_BODY="$WORK_DIR/login.json"
trap 'rm -f "$RESULTS" "$HEADERS" "$LOGIN_BODY"; rmdir "$WORK_DIR"' EXIT
: >"$RESULTS"

LOGIN_STATUS="$(curl -sS -o "$LOGIN_BODY" -w '%{http_code}' \
  -H 'Content-Type: application/json' \
  --data "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
  "$API_BASE_URL/api/auth/login")" || fail "Could not reach $API_BASE_URL"
[[ "$LOGIN_STATUS" == "200" ]] || fail "Admin login returned HTTP $LOGIN_STATUS"
TOKEN="$(node -e 'console.log(JSON.parse(require("fs").readFileSync(0,"utf8")).token)' <"$LOGIN_BODY")" \
  || fail "Login response did not contain a token"

measure_endpoint() {
  local label="$1"
  local path="$2"
  local attempt
  local response
  local duration
  local status
  local queries

  for ((attempt = 0; attempt < WARMUPS + RUNS; attempt++)); do
    response="$(curl -sS -D "$HEADERS" -o /dev/null \
      -w '%{time_total}\t%{http_code}' \
      -H "Authorization: Bearer $TOKEN" \
      "$API_BASE_URL$path")" || fail "Request failed: $label"
    duration="${response%%$'\t'*}"
    status="${response##*$'\t'}"
    [[ "$status" == "200" ]] || fail "$label returned HTTP $status"

    queries="$(awk 'tolower($1) == "x-ef-query-count:" { gsub("\r", "", $2); value = $2 } END { if (value != "") print value }' "$HEADERS")"
    [[ "$queries" =~ ^[0-9]+$ ]] || fail "$label has no X-EF-Query-Count header; enable Development benchmark mode"

    if (( attempt >= WARMUPS )); then
      printf '%s\t%s\t%s\n' "$label" "$duration" "$queries" >>"$RESULTS"
    fi
  done
  printf 'Measured %s (%s requests)\n' "$label" "$RUNS"
}

measure_endpoint "products-list" "/api/products?pageNumber=1&pageSize=50"
measure_endpoint "products-search" "/api/products?pageNumber=1&pageSize=50&search=Benchmark%20Product%20000"
measure_endpoint "products-sort" "/api/products?pageNumber=1&pageSize=50&sortBy=price&sortDescending=true"
measure_endpoint "products-low-stock" "/api/products/low-stock"
measure_endpoint "orders-list" "/api/orders?pageNumber=1&pageSize=50"
measure_endpoint "customers-list" "/api/customers?pageNumber=1&pageSize=50"
measure_endpoint "insights-summary" "/api/insights/summary"
measure_endpoint "reorder-suggestions" "/api/insights/reorder-suggestions?limit=200"
measure_endpoint "sales-trend" "/api/insights/sales-trend?days=365"

node - "$RESULTS" "$RUNS" <<'NODE'
const fs = require("node:fs");
const rows = fs.readFileSync(process.argv[2], "utf8").trim().split(/\r?\n/).map(line => {
  const [name, seconds, queries] = line.split("\t");
  return { name, ms: Number(seconds) * 1000, queries: Number(queries) };
});
const runs = Number(process.argv[3]);
const groups = Map.groupBy(rows, row => row.name);
console.log(`\nWarm requests per endpoint: ${runs}`);
console.log("| Endpoint | Median (ms) | P95 (ms) | EF commands (avg) |");
console.log("|---|---:|---:|---:|");
for (const [name, values] of groups) {
  const latencies = values.map(value => value.ms).sort((a, b) => a - b);
  const median = latencies.length % 2
    ? latencies[Math.floor(latencies.length / 2)]
    : (latencies[latencies.length / 2 - 1] + latencies[latencies.length / 2]) / 2;
  const p95 = latencies[Math.ceil(latencies.length * 0.95) - 1];
  const commands = values.reduce((sum, value) => sum + value.queries, 0) / values.length;
  console.log(`| ${name} | ${median.toFixed(1)} | ${p95.toFixed(1)} | ${commands.toFixed(1)} |`);
}
NODE
