#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"

echo "Health..."
curl -fsS "$BASE_URL/api/health" | grep -q '"status":"ok\|"status": "ok"'

echo "Accounts..."
ACCOUNTS=$(curl -fsS "$BASE_URL/api/v1/accounts")
echo "$ACCOUNTS" | grep -q Checking
CHECKING=$(python3 -c 'import json,sys; d=json.load(sys.stdin); print(next(a["id"] for a in d["accounts"] if a["label"]=="Checking"))' <<<"$ACCOUNTS")
SAVINGS=$(python3 -c 'import json,sys; d=json.load(sys.stdin); print(next(a["id"] for a in d["accounts"] if a["label"]=="Savings"))' <<<"$ACCOUNTS")

echo "Deposit..."
curl -fsS -X POST "$BASE_URL/api/v1/deposit" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: smoke-dep-$(date +%s)" \
  -d "{\"to\":\"$CHECKING\",\"amount\":100}" | grep -q transaction

echo "Withdraw..."
curl -fsS -X POST "$BASE_URL/api/v1/withdraw" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: smoke-wd-$(date +%s)" \
  -d "{\"from\":\"$CHECKING\",\"amount\":50}" | grep -q transaction

echo "Transfer..."
curl -fsS -X POST "$BASE_URL/api/v1/transfer" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: smoke-tr-$(date +%s)" \
  -d "{\"from\":\"$CHECKING\",\"to\":\"$SAVINGS\",\"amount\":25}" | grep -q from

KEY="smoke-idem-$(date +%s)"
curl -fsS -X POST "$BASE_URL/api/v1/deposit" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $KEY" \
  -d "{\"to\":\"$CHECKING\",\"amount\":10}" >/tmp/smoke1.json
curl -fsS -X POST "$BASE_URL/api/v1/deposit" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $KEY" \
  -d "{\"to\":\"$CHECKING\",\"amount\":10}" >/tmp/smoke2.json
python3 -c 'import json; a=json.load(open("/tmp/smoke1.json")); b=json.load(open("/tmp/smoke2.json")); assert a["to"]["balance"]==b["to"]["balance"]'

echo "Swagger..."
curl -fsS -o /dev/null -w "%{http_code}\n" "$BASE_URL/swagger/v1/swagger.json" | grep -q 200

echo "Smoke OK"
