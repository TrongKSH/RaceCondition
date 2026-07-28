using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Data;
using RaceConditionDemo.Models;

namespace RaceConditionDemo.Endpoints;

// =============================================================================
//  STEP 3 OF THE TALK - THE FLAWED CODE
// =============================================================================
//  This is what a perfectly reasonable code review approves.
//  It has validation. It has an early return. It reads clean.
//  It is also completely broken under concurrency.
// =============================================================================

public static class VulnerableEndpoints
{
    public static void MapVulnerableEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/vulnerable").WithTags("1. Vulnerable");

        // ---------------------------------------------------------------------
        // The classic READ-MODIFY-WRITE (a.k.a. "check-then-act") bug.
        //
        // Timeline with two concurrent requests, balance = 100, amount = 100:
        //
        //   t0  Request A: SELECT Balance -> 100
        //   t1  Request B: SELECT Balance -> 100        <-- B read BEFORE A wrote
        //   t2  Request A: 100 >= 100 -> OK
        //   t3  Request B: 100 >= 100 -> OK             <-- guard passes twice
        //   t4  Request A: UPDATE Balance = 0  WHERE Id = 1
        //   t5  Request B: UPDATE Balance = 0  WHERE Id = 1
        //
        //   Result: 200 withdrawn from a 100 balance. Both requests return 200 OK.
        //   No exception. No log. Nothing in your APM. Just missing money.
        //
        // The guard `if (wallet.Balance < req.Amount)` is worthless, because the
        // value it checks is a STALE COPY held in your app's memory, and nothing
        // stops the row from changing between the SELECT and the UPDATE.
        // ---------------------------------------------------------------------
        g.MapPost("/withdraw", async (WithdrawRequest req, UnsafeDbContext db) =>
        {
            // ---- READ -------------------------------------------------------
            // SELECT TOP(1) [Id], [Owner], [Balance] FROM [Wallets] WHERE [Id] = @id
            var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == req.WalletId);
            if (wallet is null) return Results.NotFound();

            // ---- CHECK (against a stale in-memory value) --------------------
            if (wallet.Balance < req.Amount)
                return Results.BadRequest(new { error = "Insufficient funds", wallet.Balance });

            // ---- THE VULNERABILITY WINDOW -----------------------------------
            // In production this window is filled by: a fraud-check HTTP call,
            // a logging await, GC, thread-pool starvation, a slow network hop.
            // Here we simulate it so the demo is deterministic.
            if (req.ThinkTimeMs > 0)
                await Task.Delay(req.ThinkTimeMs);

            // ---- MODIFY -----------------------------------------------------
            wallet.Balance -= req.Amount;

            db.Ledger.Add(new LedgerEntry
            {
                WalletId = wallet.Id,
                Operation = "vulnerable-withdraw",
                Amount = req.Amount,
                BalanceAfter = wallet.Balance
            });

            // ---- WRITE ------------------------------------------------------
            // UPDATE [Wallets] SET [Balance] = @p0 WHERE [Id] = @p1
            //                                      ^^^^^^^^^^^^^^^
            // "Set the balance to the number I calculated." Last writer wins.
            // Nothing here says "...but only if nobody else touched it."
            await db.SaveChangesAsync();

            return Results.Ok(new { wallet.Id, wallet.Balance, note = "no concurrency control" });
        })
        .WithSummary("Broken: read-modify-write with no lock and no token");

        // ---------------------------------------------------------------------
        // The Starbucks / Flexcoin shape: transfer between two accounts.
        // Same bug, but now it can CREATE money instead of only losing it,
        // because the credit side and the debit side race independently.
        // ---------------------------------------------------------------------
        g.MapPost("/transfer", async (TransferRequest req, UnsafeDbContext db) =>
        {
            var from = await db.Wallets.FirstOrDefaultAsync(w => w.Id == req.FromWalletId);
            var to = await db.Wallets.FirstOrDefaultAsync(w => w.Id == req.ToWalletId);
            if (from is null || to is null) return Results.NotFound();

            if (from.Balance < req.Amount)
                return Results.BadRequest(new { error = "Insufficient funds" });

            if (req.ThinkTimeMs > 0)
                await Task.Delay(req.ThinkTimeMs);

            to.Balance += req.Amount;    // credit first  <- Homakov's Starbucks order
            from.Balance -= req.Amount;  // debit second

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                from = new { from.Id, from.Balance },
                to = new { to.Id, to.Balance },
                systemTotal = from.Balance + to.Balance
            });
        })
        .WithSummary("Broken: the Starbucks gift-card transfer");
    }
}
