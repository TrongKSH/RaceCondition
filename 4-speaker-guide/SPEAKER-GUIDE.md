# Race Conditions in EF Core + SQL Server
## 30-minute sharing session — speaker guide

**Audience:** mixed juniors and seniors, .NET
**Goal:** everyone leaves able to spot a lost update in a code review, and to pick the right fix on purpose rather than by habit.
**Format:** live demo driven, slides as punctuation.

---

## The one sentence

> EF Core will happily let two requests overwrite each other, it will not tell you, and the fix is one line — but *which* line depends on how often it happens.

If someone only hears one thing, make it that.

---

## Timing

| Min | Segment | What's on screen |
|---|---|---|
| 0–3 | **Hook** — the $200 problem | Simulator, vulnerable mode, step 1–2 |
| 3–7 | **Two real incidents** — Starbucks, Flexcoin | Simulator → tab 2 |
| 7–12 | **The flawed code** — walk the interleaving | Simulator, vulnerable mode, full run + `VulnerableEndpoints.cs` |
| 12–14 | **Live: break it for real** | Terminal, `mode=vulnerable` |
| 14–19 | **Fix 1: Optimistic (RowVersion)** | Simulator optimistic mode + `OptimisticEndpoints.cs` + terminal |
| 19–24 | **Fix 2: Pessimistic (UPDLOCK)** | Simulator pessimistic mode + `PessimisticEndpoints.cs` + terminal |
| 24–26 | **Bonus: the atomic UPDATE** | Simulator atomic mode + terminal |
| 26–29 | **Which one do I use?** | Simulator → tab 3 |
| 29–30 | **Close + the code-review checklist** | Last slide |

Realistically you will overrun. **Segments 24–26 and the Flexcoin half of segment 3 are the designated cuts.** Decide that now, not on stage.

---

## Segment-by-segment

### 0–3 · Hook

Open on the simulator, vulnerable mode, step 0. Don't introduce yourself yet.

> "Alice has exactly $100 in her wallet. She taps Withdraw on her phone. Her laptop, still logged in, sends the same request eight milliseconds later. How much money does she get?"

Take a show of hands: $100 or $200. Then step through to the end — **don't narrate the mechanism yet**, just let them watch the two 200 OKs land.

> "Two HTTP 200s. No exception. No log line. Your error rate is zero percent and the money is gone. That last part is why this bug is different from every other bug you ship — it doesn't page you. You find it at month-end close."

**Do not** say "this is a race condition" in the first minute. Let them see the behaviour before they get the label; the term makes people stop looking.

### 3–7 · Two real incidents

Tab 2. **Both incidents are animated**, driven the same way as tab 1 — press **Next step**, or hit **Auto-play** and narrate over the top.

**Starbucks first** — it's funny, it's small, and everyone knows the brand.

> "Three gift cards. Two browser windows. That's the whole attack kit."

Step through it. The frame that matters is **step 4**, where browser 2 commits: card 2 climbs to $15 while card 1 stays at zero, and the "money from nowhere" tile goes red. Say why:

> "Look at why the two halves behave differently. The credit is *relative* — `+= 5` applies every time it runs. The debit is *absolute* — `= 0` is the same answer however often it runs. Two credits, one debit. That asymmetry is the exploit."

Then close it:

> "He turned $15 into $20, walked into a store and spent $16.70 to prove the money was real, then topped the card back up to pay it back. Starbucks called it fraudulent activity."

Land the point: *this was not clever*. It was two clicks at the same time.

Then **Flexcoin**, fast, as the counterweight. Its animation is a **swarm** — 40 squares standing in for the flood of simultaneous requests. Hit Auto-play and let it run while you talk; the three phases (all read → all approved → all committed) tell the story without you narrating each one.

> "Same bug, industrial scale. Forty requests all read the same 100 BTC before any of them wrote. All forty passed the 'do you have enough' check. Forty credits went out, and the source was debited once."

> "896 bitcoin — about $600,000 then. The company had no capital to cover it and shut down permanently two days later. Same week, Poloniex lost 12% of its bitcoin to a withdrawal race."

> "One of these companies lost pocket change. The other one stopped existing. Same bug."

*(If you're running long, cut Flexcoin to those two sentences and move on.)*

### 7–12 · The flawed code

Switch to `Endpoints/VulnerableEndpoints.cs`. Read the method out loud. Then ask the room:

> "What's wrong with this code?"

Someone will say "it needs a transaction." **This is the most valuable moment in the talk — do not skip past it.** Say:

> "That's the answer I'd have given too, and it's wrong. A transaction gives you atomicity — all or nothing. It does not give you isolation from a concurrent reader. At READ COMMITTED, that SELECT takes a shared lock and drops it immediately. Wrap this whole method in `BeginTransaction()` and both requests still read $100. You've changed nothing."

Now go back to the simulator and step the interleaving frame by frame, using **Space**. Pause on step 3 (the second SELECT):

> "This is the moment the bug happens. Not at the write — here. Two requests now hold the same stale truth, and neither can tell."

Pause on step 7 and read the SQL literally:

> "`SET Balance = 0 WHERE Id = 1`. Set it to *zero*. Not 'subtract a hundred', and not 'only if it's still a hundred'. An absolute value, computed in C# from a number that was true a moment ago."

**For the juniors:** the concept is *read → check → write*, and anything can happen in the gaps. Say the phrase "check-then-act" once and move on.
**For the seniors:** the term is *lost update*, it's ANSI-defined, and READ COMMITTED explicitly permits it.

### 12–14 · Break it for real

Terminal. One command, big font.

```bash
curl -X POST http://localhost:5080/api/demo/run \
  -H 'Content-Type: application/json' \
  -d '{"mode":"vulnerable","concurrency":20,"amount":100,"startingBalance":1000}'
```

Point at `http200_Succeeded: 20` and `moneyCreatedFromThinAir`.

Then the one that always gets a reaction:

```bash
# 50 concurrent, starting from 500
-d '{"mode":"vulnerable","concurrency":50,"amount":100,"startingBalance":500}'
```

> "Negative balance. From an endpoint that checks for insufficient funds on line one."

**Pre-empt the objection before it's raised** — someone always raises it:

> "Yes, there's an artificial delay in there. It widens the window so this reproduces every time on my laptop instead of once every ten thousand requests in production. It does not create the bug. Set it to zero, raise the concurrency, and you'll still see it — that's the second command in the script."

### 14–19 · Fix 1 — Optimistic (RowVersion)

Show `Models/Wallet.cs` and both DbContexts side by side:

> "Same class. Same table. The only difference between safe and broken is this one line."

```csharp
builder.Property(w => w.RowVersion).IsRowVersion();
// or: [Timestamp] public byte[] RowVersion { get; set; }
```

Simulator → optimistic mode → step to frame 4 and **read the WHERE clause out loud**:

```sql
UPDATE Wallets SET Balance = 0
WHERE Id = 1 AND RowVersion = 0x...07D1
```

> "'Update this row only if it is still the version I read.' That sentence is the entire fix."

The nuance to state explicitly, because it's what people get wrong:

> "This does not *prevent* the collision. Both requests still read stale data. What it guarantees is that the loser **finds out**. Silent corruption becomes a loud exception. That's the trade."

Then the terminal, same command, `mode=optimistic` → 1× 200, 19× 409.

Then `mode=optimistic-retry`, and show the retry loop. Emphasise:

> "Retry means re-read and re-decide. If you catch `DbUpdateConcurrencyException` and just call SaveChanges again, you have rebuilt the original bug with extra steps."

Also mention `rowversion` is maintained by SQL Server, is 8 bytes, is database-wide monotonic, and you never assign it.

### 19–24 · Fix 2 — Pessimistic (UPDLOCK)

> "Optimistic assumes collisions are rare. What if they're not — flash sale, last seat, hot account? Then don't detect the collision. Prevent it."

Show the code, then the simulator in pessimistic mode. The frame that sells it is the one where **Request B blocks**:

> "B isn't slow here. B is *stopped*. It hasn't read anything, so it can't read anything stale."

Say the two hard requirements plainly:

1. The locking SELECT **must** be inside an explicit transaction. Without one, the lock is released the instant the statement finishes and you're back to square one.
2. Nothing slow may run between the lock and the COMMIT. Every millisecond you hold it, everyone else on that row is stopped.

Terminal: `mode=pessimistic` → correct result, and point at `elapsedMs` being visibly higher than optimistic. That number is the price.

**Optional, if you have SSMS on a second screen** — this is the strongest visual in the talk if it works. Run the pessimistic demo with a high think time, then run query 3 from `sql/inspect.sql`:

```sql
SELECT session_id, status, blocking_session_id, wait_type FROM sys.dm_exec_requests WHERE session_id > 50;
```

> "`suspended`. `blocking_session_id = 57`. That's UPDLOCK doing its job, from the database's own point of view."

Close the segment with the senior-level footnote:

> "Two rows locked in inconsistent order will deadlock — SQL Server picks a victim and throws 1205. Always acquire locks in the same order; sorting by primary key is the cheapest total order you have."

### 24–26 · Bonus — the atomic UPDATE

> "Before you reach for either of those, ask whether you need the read at all."

```csharp
await db.Wallets
    .Where(w => w.Id == id && w.Balance >= amount)
    .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance - amount));
```

Two things to point at:

- `Balance - amount` is **relative**. No stale number ever leaves your process.
- `Balance >= amount` puts the business rule **in the same statement**, so `rowsAffected == 0` is an authoritative "rejected", not a guess based on a value read 40ms ago.

> "One round trip. No retry loop. No lock held across your C#. If your operation fits this shape, this is the right default — and most balance, counter, stock and quota operations do."

What you give up: change tracker, interceptors, domain events. Say it, don't dwell.

### 26–29 · Which one do I use?

Tab 3 of the simulator. Give them the three questions, in order:

1. Can I express it as **one UPDATE** whose WHERE clause carries the business rule? → do that.
2. If not — is a **retry acceptable** here? → optimistic + RowVersion.
3. If not — **take the lock**, keep the transaction short, and measure the throughput cost.

Then spend the remaining time on the **"looks like a fix and isn't"** list. This is what actually changes behaviour on Monday:

- `BeginTransaction()` with no lock hint
- C# `lock` / `SemaphoreSlim` (dies the day you scale to two pods)
- retrying without re-reading
- "it's never happened in prod" — a lost update writes no log; absence of alerts is not evidence of absence

### 29–30 · Close

> "Here's what I'd like you to take back to your next code review. When you see a read, then a check, then a write, on data more than one request can touch — stop and ask one question: **what happens if two of these run at once?** If the answer isn't in the WHERE clause, it isn't handled."

---

## Setup checklist — do this 15 minutes before

- [ ] `docker compose up -d` and confirm SQL Server is actually healthy (`docker ps`)
- [ ] `dotnet run`, hit `/api/demo/wallets`, confirm you get two wallets
- [ ] Run **every** command from `scripts/demo.sh` once. Cold-start JIT makes the first run slow and can mask the effect
- [ ] Terminal font ≥ 18pt, `jq` installed
- [ ] Editor font ≥ 16pt, minimap and sidebars off
- [ ] Open `race-condition-simulator.html` in a browser and switch tabs once so it's warm
- [ ] Screenshot the vulnerable result **now**, as your fallback if the live demo fails
- [ ] Silence Slack/Teams; disable notifications
- [ ] Have `sql/inspect.sql` open in SSMS only if you have a second screen — otherwise skip it

**If the live demo dies:** don't debug on stage. Switch to the HTML simulator and keep talking; it makes every point the terminal does. Fix it in the break.

---

## Questions you will get, with answers

**"Doesn't a transaction fix this?"**
No — and this is the most common misconception. A transaction gives atomicity, not isolation from a concurrent reader. At READ COMMITTED both requests still read the old value. You need the token in the WHERE clause, or a lock hint, or a single-statement update.

**"What about Serializable isolation?"**
It does fix it, by taking range locks and forcing serial execution. It also produces deadlocks under load that you now have to catch and retry. It's a deliberate choice for a specific operation, not a global default. If you set it globally you've chosen pessimistic locking for your entire application without telling anyone.

**"Why not just `lock` in C#?"**
Works on one process. The day you run two pods, two instances, or a blue/green deploy, it silently stops working — and it stops working *silently*, which is the worst property a concurrency control can have. If you need a cross-process lock, that's a distributed lock (Redis/Redlock, `sp_getapplock`), and it comes with its own failure modes.

**"Isn't the RowVersion check a performance hit?"**
Effectively no. The column is 8 bytes and the WHERE clause is on the same clustered index seek you were already doing. You pay only when a collision actually happens.

**"We use `[ConcurrencyCheck]` on the amount column instead."**
That works for a single column and gets fragile fast — the moment the operation touches a second field, or a nullable one, you're maintaining a hand-rolled token. `rowversion` covers the whole row and the database maintains it.

**"Does this apply to PostgreSQL / MySQL?"**
The concept is identical. The optimistic fix is the same idea with a different column type (`xmin` on Postgres, a manual `int` version elsewhere). The pessimistic fix is `SELECT ... FOR UPDATE` instead of `WITH (UPDLOCK)`.

**"How do I find these in an existing codebase?"**
Grep for a read followed by a write on the same entity in one method — especially `FirstOrDefaultAsync` followed by `SaveChangesAsync` on money, stock, counters, quotas or status transitions. Then check whether the entity has a concurrency token at all. In most codebases the honest answer is: none of them do.

**"How do I write a test for this?"**
`Task.WhenAll` over N parallel calls against a real SQL Server, then assert the invariant (balance never negative, sum of ledger equals the delta). It won't fail deterministically without a widened window — inject a delay behind a test-only hook, exactly like `thinkTimeMs` in the demo. Note that an in-memory provider will *not* reproduce this; you need the real database.

---

## Code-review checklist to leave them with

Put this on the last slide and paste it in the team channel afterwards.

```
Concurrency review — ask these five:

1. Does this method read a row, decide something, then write that row?
2. Can two requests hit it at the same time? (Retry policies and
   double-clicks count. So does a message queue with at-least-once delivery.)
3. Is the business rule in the WHERE clause, or only in C#?
4. Does the entity have a concurrency token — and does this path use it?
5. If it uses a lock: is it inside a transaction, and is the transaction short?

If 1 and 2 are yes and 3, 4 and 5 are all no — you have a lost update.
It will not throw. It will not log. Fix it now.
```
