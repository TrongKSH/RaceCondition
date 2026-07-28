# RaceDemo.Console — the simple version

A console app that shows the same four concurrency strategies as the API project,
with none of the HTTP, DI, Swagger or routing in the way.

**Why a console app for a talk:** the two requests are two `DbContext` objects you
create by hand, on two visible lines:

```csharp
using var requestA = new UnsafeDb();
using var requestB = new UnsafeDb();
```

That is the whole reason each request ends up with its own stale copy. In a web
app the same thing happens, but it is hidden inside DI scoping — invisible
exactly where the audience needs to see it.

---

## Run it

```bash
docker compose up -d     # skip if the API demo's SQL Server is already running
dotnet run
```

```
    1   Broken            no lock, no concurrency token
    2   Optimistic        RowVersion token, fail fast
    3   Optimistic+retry  re-read and re-decide
    4   Pessimistic       WITH (UPDLOCK, ROWLOCK)
    a   run all four in order
    s   show the SQL EF Core really sends
    q   quit
```

Or jump straight to one, which is what you want on stage:

```bash
dotnet run -- 1
dotnet run -- all
```

Every scenario resets Alice's wallet to **$100** first, then has two requests each
try to withdraw **$100**. Each one ends on the same scoreboard, so the four are
directly comparable.

---

## The files, in the order you should show them

| File | Lines | What it's for |
|---|---|---|
| `AppDb.cs` | ~90 | **Show this first.** Two contexts, same entity, same table, one line different. |
| `Scenarios/Vulnerable.cs` | ~85 | The bug. Reads top to bottom as a six-step timeline. |
| `Scenarios/Optimistic.cs` | ~90 | Identical six steps, `SafeDb` instead of `UnsafeDb`. |
| `Scenarios/OptimisticRetry.cs` | ~110 | Catch the conflict, re-read, re-decide. |
| `Scenarios/Pessimistic.cs` | ~110 | Take the lock. The only scenario that uses a second thread. |
| `Wallet.cs`, `Demo.cs`, `Show.cs` | small | Entity, connection string, console formatting. Nothing to teach. |

Each scenario is one method you can put on screen whole. Don't scroll during the
talk — that is the main reason this version exists.

---

## How the timing works, and why

**Scenarios 1, 2 and 3 have no threads at all.** They are straight-line code: A
reads, B reads, A checks, B checks, A writes, B writes. No `Task.Run`, no
`Task.Delay`, no barriers.

That is deliberate. The interleaving shown is a real one that happens in
production; we just choose it explicitly instead of hoping the thread scheduler
produces it while forty people watch. It reproduces byte-identically every run,
and the code reads exactly like the sequence diagram in the simulator.

**Scenario 4 does use a second thread**, because it has to — the whole point is
that Request B genuinely blocks. You will watch it hang for two seconds and then
unblock the instant A commits. That wait is real; it is SQL Server holding the
door shut.

If someone asks whether the scripted ordering is cheating: it isn't, and the
honest answer is good material. Say *"this is one specific interleaving out of
several possible ones — I picked the one that loses money so we can all see it.
Tab 4 of the simulator fires twenty real parallel requests and gets there by
accident."*

---

## What to say at each scenario

**1 · Broken.** Pause after step 2, when both requests have read $100:

> "Both of them are holding the same hundred dollars, and neither can tell.
> This is where the bug is created — not at the write."

Then at step 5, read the UPDATE literally:

> "Set the balance to zero. Not *subtract a hundred*, and not *only if it's still
> a hundred*. An absolute number, worked out in C# from a copy."

Finish on the scoreboard: $200 handed over, $100 in the wallet, **money from
nowhere: $100** — and no exception anywhere.

**2 · Optimistic.** Show `AppDb.cs` side by side with scenario 1. Same six steps,
one line different. Read the generated WHERE clause aloud:

> "Update this row only if it is still the version I read."

Say what it does *not* do — it never stops B reading stale data. It guarantees B
finds out.

**3 · Optimistic + retry.** The rule is on the screen and it is the thing people
get wrong:

> "Retry means re-read and re-decide. If you catch the exception and just call
> SaveChanges again, you've rebuilt the original bug with extra steps."

B's first read says $100, its second says $0, and it declines honestly.

**4 · Pessimistic.** Let the two-second wait play out in silence. It is
uncomfortable, and that discomfort is the lesson:

> "B isn't slow. B is stopped. It hasn't read anything, so it can't act on
> anything stale. And that pause is what correctness costs you here."

---

## If it won't connect

The app prints the fix itself. Check the container is up:

```bash
docker compose up -d
docker ps
```

Connection string lives in one place — `Demo.ConnectionString` at the top of
`Demo.cs`. Database name is `RaceDemoConsole`, separate from the API project's
`RaceDemo`, so the two demos never fight over the same row.

---

## Not covered here, on purpose

The atomic single-statement fix (`ExecuteUpdateAsync` with the business rule in
the WHERE clause) is in the API project, not this one. If you have time for it,
show it from there — but this project is deliberately the four you asked for and
nothing else.
