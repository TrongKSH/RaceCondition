using Microsoft.EntityFrameworkCore;

namespace RaceDemo.Scenarios;

// =============================================================================
//  3 · OPTIMISTIC + RETRY
//
//  A 409 on a "withdraw" button is a poor answer for a user. If the operation
//  is safe to recompute, catch the conflict and redo the work against fresh
//  state.
//
//  THE RULE, and the thing people get wrong:
//      Retry means RE-READ and RE-DECIDE.
//      Catching the exception and calling SaveChanges() again just rebuilds
//      the original bug with extra steps.
//
//  Watch the order below: B reads FIRST, A commits in the middle, and B's save
//  lands on a row that has moved. That is a real collision, not a staged one.
// =============================================================================

public static class OptimisticRetry
{
    private const int MaxAttempts = 3;

    public static async Task RunAsync()
    {
        Show.Title("3 · OPTIMISTIC + RETRY  —  re-read, re-decide");
        Show.Intro("Same collision as scenario 2. This time Request B doesn't just fail —\n" +
                   "  it goes back to the database and works out the right answer.");

        await Demo.ResetAsync();

        using var requestA = new SafeDb();
        using var requestB = new SafeDb();
        var paidOut = 0m;

        // ---- step 1: B reads first ------------------------------------------
        Show.Step(1, "REQUEST B", "reads the balance and starts its work");
        var walletB = await requestB.Wallets.FirstAsync(w => w.Id == 1);
        Show.Sql("SELECT TOP(1) [Id], [Owner], [Balance], [RowVersion] FROM [Wallets] WHERE [Id] = 1");
        Show.Result($"B holds a copy: {walletB.Balance:C}");

        // ---- step 2: A overtakes it -----------------------------------------
        Show.Step(2, "REQUEST A", "reads, checks and commits — it gets there first");
        var walletA = await requestA.Wallets.FirstAsync(w => w.Id == 1);
        walletA.Balance -= Demo.Withdrawal;
        await requestA.SaveChangesAsync();
        paidOut += Demo.Withdrawal;
        Show.Good($"{Demo.Withdrawal:C} handed over. The row is now {walletA.Balance:C}, " +
                  "on a new RowVersion.");
        Show.Note("B is still holding its copy from step 1. It has no idea.");

        // ---- step 3+: B tries to save, collides, and recovers ----------------
        walletB.Balance -= Demo.Withdrawal;   // the work B started in step 1

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            Show.Step(2 + attempt, "REQUEST B", $"save attempt {attempt} of {MaxAttempts}");

            try
            {
                await requestB.SaveChangesAsync();
                paidOut += Demo.Withdrawal;
                Show.Good($"committed on attempt {attempt}. {Demo.Withdrawal:C} handed over.");
                break;
            }
            catch (DbUpdateConcurrencyException)
            {
                Show.Sql("UPDATE [Wallets] SET [Balance] = ...\n" +
                         "WHERE [Id] = 1 AND [RowVersion] = <the value B is holding>\n" +
                         "-- 0 rows affected");
                Show.Warn("DbUpdateConcurrencyException — the row moved underneath B.");

                if (attempt == MaxAttempts)
                {
                    Show.Bad("out of attempts. Return 409 and let the caller decide.");
                    break;
                }

                // Exponential backoff with jitter, so a burst of requests does
                // not synchronise into a retry storm.
                var wait = (int)(Math.Pow(2, attempt) * 25) + Random.Shared.Next(0, 25);
                Show.Result($"backing off {wait} ms");
                await Task.Delay(wait);

                // -------------------------------------------------------------
                // RE-READ. Clear() throws away everything the failed attempt
                // cached, so this query really does hit the database instead of
                // handing back the stale entity we already have.
                // -------------------------------------------------------------
                requestB.ChangeTracker.Clear();
                walletB = await requestB.Wallets.FirstAsync(w => w.Id == 1);
                Show.Result($"re-read from the database: {walletB.Balance:C}");

                // -------------------------------------------------------------
                // RE-DECIDE. Run the business rule against the value we just
                // read. Skipping this step is how people reintroduce the bug.
                // -------------------------------------------------------------
                if (walletB.Balance < Demo.Withdrawal)
                {
                    Show.Warn($"{walletB.Balance:C} < {Demo.Withdrawal:C}  ->  insufficient funds");
                    Show.Result("HTTP 400. B never double-pays, and the user gets a true answer.");
                    break;
                }

                walletB.Balance -= Demo.Withdrawal;   // redo the work on fresh state
            }
        }

        Show.Note("B's first read said $100. Its second said $0. Same request,\n" +
                  "            different answer — because it asked again instead of assuming.");

        Show.Scoreboard(Demo.StartingBalance, paidOut, await Demo.BalanceAsync());
    }
}
