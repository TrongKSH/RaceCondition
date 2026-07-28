# Race Conditions in EF Core + SQL Server — session kit

Everything for the 30-minute sharing session, split by what you'll actually be doing with it.

```
1-slides/          the deck you present from
2-simulator/       the interactive visual you drive on stage
3-demo-code/       the .NET project you run live
4-speaker-guide/   what to say, when, and what to do if it breaks
_scratch/          build leftovers — ignore
```

---

## 1-slides/

| File | What it is |
|---|---|
| `race-conditions-ef-core.pptx` | **The deck.** 17 slides, dark theme, speaker notes on the slides that need them. |
| `previews/slide-NN.jpg` | Rendered preview of every slide — flip through these to re-plan the order without opening PowerPoint. |
| `build_deck.js` | The generator. Edit and re-run to regenerate the deck instead of hand-editing slides. |

Regenerate after editing:

```bash
node build_deck.js race-conditions-ef-core.pptx
```

**Slide order:** title → the $100 question → the answer → Starbucks → Flexcoin → bridge →
flawed code → interleaving → the false fix → optimistic → handling it → pessimistic →
what the lock costs → atomic → decision → not-fixes → checklist.

---

## 2-simulator/

`race-condition-simulator.html` — open it in any browser, no server, no internet needed.

Four tabs:

1. **Simulator** — step two concurrent requests through one row. Switch between vulnerable / optimistic / pessimistic / atomic and the SQL, the in-memory copies, the lock state and the verdict all change. `Space` = next step, `R` = reset.
2. **Real incidents** — the Starbucks and Flexcoin walkthroughs with the code shapes that caused them.
3. **Scale it up** — a genuine discrete-event simulation of N concurrent requests. Re-run it live; the counts change, the verdict doesn't.
4. **Which fix?** — the decision table and the list of things that look like fixes and aren't.

This is also your **fallback if the live demo dies** — it makes every point the terminal does.

---

## 3-demo-code/

**Two projects. Present from the console one.**

### `RaceDemo.Console/` — use this on stage

The simple version: four scenarios, no HTTP, no DI, no Swagger. Each scenario is
one method you can put on screen whole and read top to bottom.

```bash
cd 3-demo-code/RaceDemo.Console
docker compose up -d      # skip if the API demo's SQL Server is already up
dotnet run                # menu
dotnet run -- 1           # jump straight to a scenario
dotnet run -- all         # all four in order
```

```
    1   Broken            no lock, no concurrency token
    2   Optimistic        RowVersion token, fail fast
    3   Optimistic+retry  re-read and re-decide
    4   Pessimistic       WITH (UPDLOCK, ROWLOCK)
    s   show the SQL EF Core really sends
```

**Show `AppDb.cs` first.** Two DbContexts, same entity, same table, one line
different — that is the whole talk in ninety lines. Then each scenario in turn;
every one ends on the same scoreboard so they're directly comparable.

Scenarios 1–3 have **no threads at all** — straight-line code that reproduces
identically every run. Scenario 4 uses a real background task, because you need
to actually watch request B hang for two seconds and unblock on COMMIT.

Its `README.md` has what to say at each scenario.

### `RaceConditionDemo/` — the API version, keep as backup

.NET 8 Minimal API. Adds the **atomic single-UPDATE** fix (not in the console
version) and a load driver that fires N genuinely parallel HTTP requests.
**Confirmed building** — bin/obj present from your Rider build.

```bash
cd 3-demo-code/RaceConditionDemo
docker compose up -d
dotnet run                              # http://localhost:5080/swagger

curl -X POST http://localhost:5080/api/demo/run \
  -H 'Content-Type: application/json' \
  -d '{"mode":"vulnerable","concurrency":20,"amount":100,"startingBalance":1000}'
```

`mode` = `vulnerable` | `optimistic` | `optimistic-retry` | `pessimistic` | `atomic`

Reach for it if someone asks *"does this happen over real HTTP?"* or if you have
time for the atomic fix. `sql/inspect.sql` shows the lock live in SSMS.

The two projects use **separate databases** (`RaceDemoConsole` vs `RaceDemo`), so
you can have both running without them fighting over the same row.

---

## 4-speaker-guide/

`SPEAKER-GUIDE.md` — the run sheet. Minute-by-minute timings, what to say at each beat,
the pre-flight checklist, the questions you'll get with answers worked out, and the
code-review checklist to paste in the team channel afterwards.

**Read the "Setup checklist" section 15 minutes before you present.**

---

## Suggested run order on the day

1. Open `2-simulator/race-condition-simulator.html` in a browser tab, click through once so it's warm.
2. Start SQL Server and `dotnet run`, then run every command in `scripts/demo.sh` once — cold-start JIT makes the first run slow and can mask the effect.
3. Screenshot the vulnerable result now, as insurance.
4. Deck on the main screen, browser and terminal ready to switch to.
