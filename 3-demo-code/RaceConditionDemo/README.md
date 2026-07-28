# Race Conditions in EF Core + SQL Server — live demo

A .NET 8 Minimal API that reproduces a lost-update race condition on a wallet
balance, then fixes it four different ways. Built for a 30-minute sharing session.

---

## Run it

```bash
# 1. SQL Server (skip if you already have one)
docker compose up -d

# 2. The API — creates the schema and seeds two wallets on first run
dotnet run

# 3. Swagger UI
open http://localhost:5080/swagger
```

Then run the demo script one block at a time:

```bash
chmod +x scripts/demo.sh
./scripts/demo.sh          # bash + curl + jq
# or
pwsh scripts/demo.ps1      # PowerShell
```

---

## The one call that tells the whole story

```bash
curl -X POST http://localhost:5080/api/demo/run \
  -H 'Content-Type: application/json' \
  -d '{"mode":"vulnerable","concurrency":20,"amount":100,"startingBalance":1000}'
```

Change `mode` to `optimistic`, `optimistic-retry`, `pessimistic`, or `atomic`
and run the identical command. Nothing else changes.

Typical output for `vulnerable`:

```json
{
  "mode": "vulnerable",
  "startingBalance": 1000.00,
  "expectedBalance": -1000.00,
  "actualBalance": 900.00,
  "http200_Succeeded": 20,
  "moneyCreatedFromThinAir": 1900.00,
  "verdict": "BROKEN - API confirmed 20 withdrawals (2000.00) but only 100.00 left the wallet. 1900.00 created from thin air."
}
```

Twenty HTTP 200s. One hundred dollars actually debited. No exception anywhere.

---

## Project layout

| File | What it demonstrates |
|---|---|
| `Models/Wallet.cs` | One entity, one table. Includes the `byte[] RowVersion`. |
| `Data/UnsafeDbContext.cs` | Ignores `RowVersion`. Emits `WHERE Id = @id`. |
| `Data/SafeDbContext.cs` | `.IsRowVersion()`. Emits `WHERE Id = @id AND RowVersion = @rv`. |
| `Endpoints/VulnerableEndpoints.cs` | **§3** The flawed read-modify-write, plus the Starbucks transfer. |
| `Endpoints/OptimisticEndpoints.cs` | **§4** `DbUpdateConcurrencyException`, fail-fast and auto-retry. |
| `Endpoints/PessimisticEndpoints.cs` | **§5** `FromSqlInterpolated` + `WITH (UPDLOCK, ROWLOCK)`. |
| `Endpoints/AtomicEndpoints.cs` | Bonus: `ExecuteUpdateAsync` — one statement, no window. |
| `Endpoints/DemoEndpoints.cs` | The driver that fires N parallel requests and scores the result. |
| `sql/inspect.sql` | Show the lock live via `sys.dm_exec_requests`. |

**The teaching trick:** `SafeDbContext` and `UnsafeDbContext` map the *same*
`Wallet` class to the *same* `dbo.Wallets` table. The only difference is one
line of configuration. That is how you make the point that EF Core is not
"safe by default" — you have to opt in.

---

## The four fixes at a glance

| | Mechanism | Cost | Use when |
|---|---|---|---|
| **Optimistic** | `rowversion` in the `WHERE` clause | Nothing until a collision; then a wasted round trip | Contention is rare. Human-paced edits, admin screens, CRUD. |
| **Optimistic + retry** | Same, plus bounded re-read loop | Extra round trips under load | You need it to just work, and re-running the op is safe. |
| **Pessimistic** | `SELECT ... WITH (UPDLOCK)` in a transaction | Serialises every request on the row | Contention is high, or a retry is expensive/illegal. |
| **Atomic UPDATE** | `SET Balance = Balance - @x WHERE Balance >= @x` | Cheapest of all | The new value is a pure function of the old one. |

If the atomic version can express your operation, use it. It is the only one
with no window at all.

---

## Things that do NOT fix this

- `[ConcurrencyCheck]` on `Balance` alone — helps, but only detects changes to
  that one column, and is fragile once more columns join the operation.
- Wrapping the read and the write in `BeginTransaction()` **without a lock hint**.
  A transaction at READ COMMITTED gives you atomicity, not isolation from a
  concurrent reader. Both requests still read `1000`. This is the single most
  common false fix in code review.
- A C# `lock` / `SemaphoreSlim` — works on one process, silently stops working
  the moment you scale to two pods. Flexcoin-class bug in a container era.
- Retrying on `DbUpdateConcurrencyException` without re-reading and
  re-validating. That rebuilds the original bug with extra steps.
- Setting `IsolationLevel.Serializable` everywhere. It does fix it, and it will
  also flood your logs with deadlocks. Reach for it deliberately, not by default.

---

## Notes for presenting

- `thinkTimeMs` widens the race window so it reproduces every time on a laptop.
  It does **not** create the bug. Say this out loud — someone will ask.
  Set it to `0` and raise `concurrency` to 200 and you will still see failures.
- SQL logging is on (`LogTo(Console.WriteLine)`). Keep the terminal visible: the
  audience can watch the `WHERE` clause change between modes.
- `EnableSensitiveDataLogging()` is on for the demo. Never in production.
- Turn the `Max Pool Size=200` in the connection string down and you will start
  seeing pool-exhaustion timeouts under `concurrency=50` — an accidental but
  useful second lesson.
