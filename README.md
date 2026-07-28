# Race Conditions in EF Core + SQL Server

A complete 30-minute sharing session: slides, an interactive simulator, two runnable
.NET 8 demo projects, and a speaker guide.

> Alice has exactly $100. She taps **Withdraw $100** on her phone, and her laptop —
> still logged in — sends the same request 8 milliseconds later.
>
> She gets $200. Two HTTP 200s, zero exceptions, zero log lines.

That is a **lost update**, and READ COMMITTED explicitly permits it. This repo shows
how it happens, what it cost two real companies, and the four ways to fix it.

---

## What's here

| Folder | What it is |
|---|---|
| **[`1-slides/`](1-slides/)** | 17-slide deck (`.pptx`), JPG previews of every slide, and `build_deck.js` to regenerate it |
| **[`2-simulator/`](2-simulator/)** | Single-file HTML simulator — no server, no build, no internet |
| **[`3-demo-code/`](3-demo-code/)** | Two runnable .NET 8 + EF Core 8 + SQL Server projects |
| **[`4-speaker-guide/`](4-speaker-guide/)** | Minute-by-minute run sheet, Q&A prep, code-review checklist |

---

## Quick start

**The simulator** — open `2-simulator/race-condition-simulator.html` in any browser.
Four tabs: a sequence diagram that draws itself step by step, animated walkthroughs of
the Starbucks and Flexcoin incidents, a decision table, and a 20-request load simulation.

**The console demo** — the one to present from:

```bash
cd 3-demo-code/RaceDemo.Console
docker compose up -d      # SQL Server 2022
dotnet run
```

```
    1   Broken            no lock, no concurrency token
    2   Optimistic        RowVersion token, fail fast
    3   Optimistic+retry  re-read and re-decide
    4   Pessimistic       WITH (UPDLOCK, ROWLOCK)
```

Every scenario resets the wallet to $100, has two requests each try to withdraw $100,
and ends on the same scoreboard — so the four are directly comparable.

---

## The idea the whole session hangs on

Two `DbContext` objects. Same entity. Same table. **One line of configuration different.**

```csharp
// UnsafeDb — what most codebases ship
b.Entity<Wallet>().Ignore(w => w.RowVersion);

// SafeDb — the fix
b.Entity<Wallet>().Property(w => w.RowVersion).IsRowVersion();
```

That single line changes the UPDATE EF Core emits from

```sql
UPDATE Wallets SET Balance = 0 WHERE Id = 1;
```

to

```sql
UPDATE Wallets SET Balance = 0 WHERE Id = 1 AND RowVersion = 0x...07D1;
```

*"Update this row only if it is still the version I read."* Row already changed →
0 rows affected → `DbUpdateConcurrencyException`.

See [`3-demo-code/RaceDemo.Console/AppDb.cs`](3-demo-code/RaceDemo.Console/AppDb.cs).

---

## The four fixes

| | Mechanism | Cost | Use when |
|---|---|---|---|
| **Optimistic** | `rowversion` in the `WHERE` clause | Free until a collision | Contention is rare — CRUD, admin screens, human-paced edits |
| **Optimistic + retry** | Same, plus a bounded re-read loop | Extra round trips under load | A 409 would be noise and the work is safe to recompute |
| **Pessimistic** | `SELECT ... WITH (UPDLOCK)` in a transaction | Serialises the row | Contention is high, or retrying is illegal |
| **Atomic UPDATE** | `SET Balance = Balance - @x WHERE Balance >= @x` | Cheapest — one round trip | The new value is a pure function of the old one |

**If the atomic version fits your operation, use it.** It is the only one with no window
at all. (It lives in the API project, not the console one.)

### Things that look like fixes and aren't

- **`BeginTransaction()` with no lock hint** — atomicity, not isolation. Both requests
  still read the old value. The most common false fix in code review.
- **C# `lock` / `SemaphoreSlim`** — works on one process, silently stops working the day
  you scale to two pods.
- **Retrying without re-reading** — rebuilds the original bug with extra steps.
- **"It's never happened in prod"** — a lost update writes no log. Absence of alerts is
  not evidence of absence.

---

## Two real incidents

**Starbucks gift cards, May 2015.** Three $5 cards and two browser windows. Concurrent
transfers between cards, both reading the same balance. The credit was relative
(`+= 5`, applies every time); the debit was absolute (`= 0`, same answer however often
it runs). Two credits, one debit. $15 paid became a $20 balance.

**Flexcoin, 2 March 2014.** Same bug, industrial scale. Thousands of simultaneous
transfers all read the balance before any of them wrote, all passed the "do you have
enough?" check. 896 BTC drained — roughly $600,000 at the time. The company had no
capital to absorb it and closed permanently two days later.

Both animated in the simulator, tab 2.

---

## Requirements

- .NET 8 SDK
- Docker (for SQL Server 2022) — or point the connection string at any SQL Server
- A browser, for the simulator

The two demo projects use **separate databases** (`RaceDemoConsole` and `RaceDemo`), so
both can run at once without fighting over the same row.
