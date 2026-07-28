# Interactive simulator

Open `race-condition-simulator.html` in any browser. No server, no build step, no internet.
Single self-contained file — safe to email or drop in Teams.

## How to read the screen

Tab 1 is a **sequence diagram that draws itself one step at a time**. Three vertical lanes:

```
Request A          SQL Server          Request B
(Alice's phone)    the one true row    (Alice's laptop)
     |                   |                   |
     |<---- gets $100 ---|                   |     time
     |                   |--- gets $100 ---->|      runs
     |---- sets $0 ----->|                   |     down
     |                   |<---- sets $0 -----|      ↓
```

Arrows point **in the direction the data travels**. Completed steps stay on screen, so the
whole flow accumulates — you can always see how you got here.

Three things update as you step:

- **The narration box** (big text at the top) — one plain sentence for what just happened,
  then a dimmer line for why it matters.
- **The scoreboard** — started with / handed to Alice / balance in the database / **money from
  nowhere**. That last tile turns red the moment the system invents money. It is the punchline,
  visible while it happens rather than only at the end.
- **The lane headers** — what each request is holding right now. When A commits, B's header
  flips to `$100 STALE COPY` in red. That is the entire bug in one badge.

## Driving it on stage

| Key / button | Does |
|---|---|
| `Space` or **Next step** | Advance one step |
| `R` or **Start over** | Restart the current mode |
| **Auto-play** | Advances every 3.4s — good for the opening hook, manual is better elsewhere |
| **Show SQL** | Off by default. Turn on for the senior half of the room, off for the juniors |
| `C` | Hidden toggle for the presenter cue line — a purple *"Say this →"* prompt written for you to read aloud. The button is hidden so nothing presenter-only shows on the projector; press `C` to bring the cues back, `C` again to hide them. To restore the visible button, set `SHOW_CUE_BUTTON = true` near the top of the script |

Mode dropdown switches between the four strategies. Step counts differ on purpose —
broken is 7 steps, optimistic 5, pessimistic 6, atomic 3. Fewer steps is the point.

## The four tabs

1. **Watch it happen** — the sequence diagram above.
2. **Real incidents** — Starbucks 2015 and Flexcoin 2014, **both animated** with the same
   engine as tab 1, each with its own step controls and scoreboard.
3. **Which fix?** — decision table plus the "looks like a fix and isn't" list.
4. **Scale it up** — a real discrete-event simulation, not an animation. Each request gets a
   random read time and write time; who wins depends on who committed in between. Re-run it and
   the numbers move, the verdict doesn't. Push the count to 100 to show optimistic retry
   degrading under contention.

### Tab 2 — the incident animations

**Starbucks (6 steps).** Lanes are Browser 1 / the two card balances / Browser 2. Two cards at
$5 each, $10 in the system, two concurrent $5 transfers. The scoreboard tracks card 1, card 2,
total in the system, and money from nowhere. **Step 4 is the one to pause on** — browser 2
commits, card 2 climbs to $15 while card 1 stays at zero, and the last tile goes red:

> The credit is *relative* (`+= 5` applies every time it runs). The debit is *absolute*
> (`= 0` is the same answer however often it runs). Two credits, one debit — that asymmetry
> is the exploit.

**Flexcoin (4 steps).** Deliberately a different visual: a **swarm** of 40 squares standing in
for the flood of simultaneous requests, moving through three phases — all read (blue), all pass
the balance check (amber), all commit (red). Good candidate for Auto-play while you talk over it.
Ends on 4,000 BTC credited out of a 100 BTC balance, then the real numbers: 896 BTC, ~$600k,
company gone in two days.

## Four moments worth pausing on

- **Broken, step 2** — the second SELECT. This is where the bug is created, *not* at the write.
  Both requests now hold the same $100 and neither can tell. Most people miss this; say it aloud.
- **Broken, step 6** — watch the "money from nowhere" tile go red as B's write lands.
- **Optimistic, step 3** — read the `WHERE Id = 1 AND RowVersion = 0x…07D1` clause out loud.
  That sentence is the entire fix. Also say what it does *not* do: it never prevents the stale
  read. Watch B's header at the same moment — its RowVersion stays at `0x…07D1` and turns red
  while the row moves to `0x…07D2`.
- **Atomic, the end** — both side lanes read *"never held a copy"*. No copy, no stale copy,
  nothing to race over.

## Fallback

If the live .NET demo fails on stage, switch here and keep talking. It makes every point the
terminal does. Don't debug in front of the room.
