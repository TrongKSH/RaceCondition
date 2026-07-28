# =============================================================================
#  Live-demo run sheet (PowerShell). Run one block at a time.
# =============================================================================
$Base = if ($env:BASE) { $env:BASE } else { "http://localhost:5080" }

function Show($title) { Write-Host "`n----- $title -----" -ForegroundColor Cyan }

function Invoke-Demo($body) {
    Invoke-RestMethod -Method Post -Uri "$Base/api/demo/run" `
        -ContentType 'application/json' -Body ($body | ConvertTo-Json) |
        Format-List
}

Show "0. Sanity check"
Invoke-RestMethod "$Base/api/demo/wallets" | Format-Table

Show "1. VULNERABLE - 20 concurrent withdrawals of 100 from 1000"
Invoke-Demo @{ mode='vulnerable'; concurrency=20; amount=100; startingBalance=1000; thinkTimeMs=150 }

Show "2. VULNERABLE, harder - drain below zero"
Invoke-Demo @{ mode='vulnerable'; concurrency=50; amount=100; startingBalance=500; thinkTimeMs=200 }

Show "3. OPTIMISTIC (RowVersion) - fail fast, expect 409s"
Invoke-Demo @{ mode='optimistic'; concurrency=20; amount=100; startingBalance=1000; thinkTimeMs=150 }

Show "4. OPTIMISTIC + RETRY"
Invoke-Demo @{ mode='optimistic-retry'; concurrency=20; amount=100; startingBalance=1000; thinkTimeMs=150 }

Show "5. PESSIMISTIC (UPDLOCK)"
Invoke-Demo @{ mode='pessimistic'; concurrency=20; amount=100; startingBalance=1000; thinkTimeMs=150 }

Show "6. ATOMIC single UPDATE"
Invoke-Demo @{ mode='atomic'; concurrency=20; amount=100; startingBalance=1000; thinkTimeMs=0 }

Show "7. STARBUCKS 2015 - broken"
Invoke-RestMethod -Method Post -Uri "$Base/api/demo/starbucks?mode=vulnerable&amount=5&thinkTimeMs=150" | Format-List

Show "8. STARBUCKS - fixed"
Invoke-RestMethod -Method Post -Uri "$Base/api/demo/starbucks?mode=pessimistic&amount=5&thinkTimeMs=150" | Format-List
