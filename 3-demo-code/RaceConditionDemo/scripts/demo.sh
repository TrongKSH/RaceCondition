#!/usr/bin/env bash
# =============================================================================
#  Live-demo run sheet. Run these ONE AT A TIME on stage, in this order.
#  Requires: curl, jq   (brew install jq / apt install jq)
# =============================================================================
set -u
BASE="${BASE:-http://localhost:5080}"

hr() { printf '\n\033[1;36m%s\033[0m\n' "----- $* -----"; }
run() { curl -s -X POST "$BASE/api/demo/run" -H 'Content-Type: application/json' -d "$1" | jq; }

hr "0. Sanity check - are the wallets there?"
curl -s "$BASE/api/demo/wallets" | jq

hr "1. VULNERABLE - 20 concurrent withdrawals of 100 from a balance of 1000"
echo "   Expect: ~20 x HTTP 200, balance nowhere near 1000 - 2000."
run '{"mode":"vulnerable","concurrency":20,"amount":100,"startingBalance":1000,"thinkTimeMs":150}'

hr "2. VULNERABLE, harder - drain it below zero"
echo "   Expect: NEGATIVE balance, from an endpoint that checks for insufficient funds."
run '{"mode":"vulnerable","concurrency":50,"amount":100,"startingBalance":500,"thinkTimeMs":200}'

hr "3. OPTIMISTIC (RowVersion) - same load, fail fast"
echo "   Expect: 1 x HTTP 200, 19 x HTTP 409 Conflict. Money conserved."
run '{"mode":"optimistic","concurrency":20,"amount":100,"startingBalance":1000,"thinkTimeMs":150}'

hr "4. OPTIMISTIC + RETRY - same load, no 409 leaks to the caller"
echo "   Expect: 10 x HTTP 200 (1000/100), rest rejected as insufficient funds. Balance = 0."
run '{"mode":"optimistic-retry","concurrency":20,"amount":100,"startingBalance":1000,"thinkTimeMs":150}'

hr "5. PESSIMISTIC (UPDLOCK) - same load, serialised"
echo "   Expect: 10 x HTTP 200, balance exactly 0, and a visibly LONGER elapsedMs."
run '{"mode":"pessimistic","concurrency":20,"amount":100,"startingBalance":1000,"thinkTimeMs":150}'

hr "6. ATOMIC single UPDATE - same load, no lock held across app code"
echo "   Expect: 10 x HTTP 200, balance exactly 0, and the FASTEST elapsedMs."
run '{"mode":"atomic","concurrency":20,"amount":100,"startingBalance":1000,"thinkTimeMs":0}'

hr "7. STARBUCKS 2015 - two concurrent transfers of the same 5.00"
echo "   Expect: systemTotalAfter = 15.00 from a system that started with 10.00."
curl -s -X POST "$BASE/api/demo/starbucks?mode=vulnerable&amount=5&thinkTimeMs=150" | jq

hr "8. STARBUCKS, fixed"
curl -s -X POST "$BASE/api/demo/starbucks?mode=pessimistic&amount=5&thinkTimeMs=150" | jq

hr "Done"
