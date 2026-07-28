using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Data;
using RaceConditionDemo.Models;

namespace RaceConditionDemo.Endpoints;

// =============================================================================
//  STEP 4 OF THE TALK - THE OPTIMISTIC FIX (RowVersion)
// =============================================================================
//  Assumption: collisions are RARE. So don't pay for a lock on every request.
//  Let everyone through, and detect the collision at write time.
//
//  Mechanism: a `rowversion` column that SQL Server bumps on every UPDATE.
//  EF Core puts the value it originally read into the WHERE clause.
//  Row changed underneath you  ->  0 rows affected  ->  DbUpdateConcurrencyException.
// =============================================================================

public static class OptimisticEndpoints
{
    public static void MapOptimisticEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/optimistic").WithTags("2. Optimistic (RowVersion)");

        // ---------------------------------------------------------------------
        // 4a. FAIL FAST - surface the collision to the caller as HTTP 409.
        //
        // SQL that EF Core 8 emits for SaveChanges():
        //
        //   UPDATE [Wallets] SET [Balance] = @p0
        //   OUTPUT INSERTED.[RowVersion]
        //   WHERE [Id] = @p1 AND [RowVersion] = @p2;
        //
        // Read that WHERE clause out loud to the room. THAT is the fix.
        // "Update this row only if it is still the version I read."
        //
        // If a competing request already committed, @@ROWCOUNT = 0, EF Core
        // notices the mismatch between rows-expected (1) and rows-affected (0)
        // and throws DbUpdateConcurrencyException.
        //
        // Use this variant when the user must be told, e.g. an edit form:
        // "Someone else changed this record, here is their version."
        // ---------------------------------------------------------------------
        g.MapPost("/withdraw", async (WithdrawRequest req, SafeDbContext db) =>
        {
            var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == req.WalletId);
            if (wallet is null) return Results.NotFound();

            if (wallet.Balance < req.Amount)
                return Results.BadRequest(new { error = "Insufficient funds", wallet.Balance });

            if (req.ThinkTimeMs > 0)
                await Task.Delay(req.ThinkTimeMs);

            wallet.Balance -= req.Amount;

            db.Ledger.Add(new LedgerEntry
            {
                WalletId = wallet.Id,
                Operation = "optimistic-withdraw",
                Amount = req.Amount,
                BalanceAfter = wallet.Balance
            });

            try
            {
                await db.SaveChangesAsync();
                return Results.Ok(new { wallet.Id, wallet.Balance });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // ex.Entries tells you exactly which entities lost the race.
                var entry = ex.Entries.First(e => e.Entity is Wallet);

                // GetDatabaseValues() re-reads the row as it is RIGHT NOW.
                var current = await entry.GetDatabaseValuesAsync();
                var currentBalance = current?.GetValue<decimal>(nameof(Wallet.Balance));

                return Results.Conflict(new
                {
                    error = "The wallet was modified by another request. Please retry.",
                    yourStaleBalance = wallet.Balance + req.Amount,
                    actualBalanceNow = currentBalance
                });
            }
        })
        .WithSummary("Safe: RowVersion token, 409 on collision");

        // ---------------------------------------------------------------------
        // 4b. AUTO-RETRY - the version you actually ship for money movements.
        //
        // A 409 on a "withdraw" button is a bad user experience. For an
        // operation that is safe to recompute, re-read the fresh state and
        // redo the work. Bounded attempts + jitter so a burst does not
        // synchronise into a retry storm.
        //
        // IMPORTANT: retry means RE-READ AND RE-DECIDE, not "call SaveChanges
        // again". If you retry without re-running the `Balance < Amount` check,
        // you have rebuilt the original bug with extra steps.
        // ---------------------------------------------------------------------
        g.MapPost("/withdraw-with-retry", async (WithdrawRequest req, SafeDbContext db) =>
        {
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Drop everything the previous attempt tracked, so we get a
                // genuinely fresh read instead of the cached entity.
                db.ChangeTracker.Clear();

                var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == req.WalletId);
                if (wallet is null) return Results.NotFound();

                // Re-evaluate the business rule against the FRESH value.
                if (wallet.Balance < req.Amount)
                    return Results.BadRequest(new { error = "Insufficient funds", wallet.Balance, attempt });

                if (req.ThinkTimeMs > 0 && attempt == 1)
                    await Task.Delay(req.ThinkTimeMs);

                wallet.Balance -= req.Amount;

                db.Ledger.Add(new LedgerEntry
                {
                    WalletId = wallet.Id,
                    Operation = $"optimistic-retry-withdraw(attempt {attempt})",
                    Amount = req.Amount,
                    BalanceAfter = wallet.Balance
                });

                try
                {
                    await db.SaveChangesAsync();
                    return Results.Ok(new { wallet.Id, wallet.Balance, attempts = attempt });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (attempt == maxAttempts)
                        return Results.Conflict(new { error = "Too much contention, give up", attempts = attempt });

                    // Exponential backoff with jitter: 2^n * 10ms +/- randomness.
                    var delay = (int)(Math.Pow(2, attempt) * 10) + Random.Shared.Next(0, 25);
                    await Task.Delay(delay);
                }
            }

            return Results.Conflict(new { error = "Too much contention" });
        })
        .WithSummary("Safe: RowVersion + bounded retry with jittered backoff");
    }
}
