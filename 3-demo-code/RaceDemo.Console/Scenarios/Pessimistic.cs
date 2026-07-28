using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace RaceDemo.Scenarios;

// =============================================================================
//  4 · PESSIMISTIC  —  SELECT ... WITH (UPDLOCK, ROWLOCK)
//
//  The other three scenarios are straight-line code: no threads, no timing,
//  same output every run. This one HAS to use a second thread, because the
//  whole point is that Request B genuinely blocks.
//
//  You will watch B hang for two seconds and then unblock the instant A
//  commits. That wait is real — it is SQL Server holding the door shut.
//
//  Two rules, both easy to get wrong:
//    1. The locking SELECT must be inside an explicit transaction. Without one
//       the lock is released the moment the statement finishes.
//    2. Nothing slow may run before the COMMIT. Every millisecond you hold the
//       lock, every other request on that row is stopped.
// =============================================================================

public static class Pessimistic
{
    public static async Task RunAsync()
    {
        Show.Title("4 · PESSIMISTIC  —  WITH (UPDLOCK, ROWLOCK)");
        Show.Intro("Don't detect the collision. Prevent it. Request A takes a lock as it\n" +
                   "  reads, and Request B is not allowed to read at all until A is done.");

        await Demo.ResetAsync();

        // Note: UnsafeDb on purpose. There is no RowVersion token in play here —
        // the lock is doing all of the work by itself.
        using var requestA = new UnsafeDb();
        var paidOut = 0m;

        // ---- step 1 ---------------------------------------------------------
        Show.Step(1, "REQUEST A", "opens a transaction");
        await using var txA = await requestA.Database.BeginTransactionAsync();
        Show.Sql("BEGIN TRANSACTION;");
        Show.Result("the lock A is about to take will live until this commits");

        // ---- step 2 ---------------------------------------------------------
        Show.Step(2, "REQUEST A", "reads the row AND locks it");
        var walletA = (await requestA.Wallets
            .FromSqlRaw(@"SELECT [Id], [Owner], [Balance]
                          FROM [Wallets] WITH (UPDLOCK, ROWLOCK)
                          WHERE [Id] = 1")
            .ToListAsync()).First();

        Show.Sql("SELECT [Id], [Owner], [Balance]\n" +
                 "FROM [Wallets] WITH (UPDLOCK, ROWLOCK)\n" +
                 "WHERE [Id] = 1");
        Show.Good($"A reads {walletA.Balance:C} and now owns this row");

        // ---- step 3: B starts on another thread and immediately blocks -------
        Show.Step(3, "REQUEST B", "runs the same SELECT — on a second connection");

        var clock = Stopwatch.StartNew();
        var requestBTask = Task.Run(async () =>
        {
            using var requestB = new UnsafeDb();
            await using var txB = await requestB.Database.BeginTransactionAsync();

            // Execution STOPS on this line until A commits. B is not slow.
            // B is suspended by SQL Server.
            var walletB = (await requestB.Wallets
                .FromSqlRaw(@"SELECT [Id], [Owner], [Balance]
                              FROM [Wallets] WITH (UPDLOCK, ROWLOCK)
                              WHERE [Id] = 1")
                .ToListAsync()).First();

            Show.Step(6, "REQUEST B", $"unblocked after {clock.ElapsedMilliseconds:N0} ms");
            Show.Result($"reads {walletB.Balance:C}  —  fresh, not stale");

            if (walletB.Balance < Demo.Withdrawal)
            {
                Show.Warn($"{walletB.Balance:C} < {Demo.Withdrawal:C}  ->  insufficient funds, declined");
                await txB.RollbackAsync();
                return 0m;
            }

            walletB.Balance -= Demo.Withdrawal;
            await requestB.SaveChangesAsync();
            await txB.CommitAsync();
            Show.Good($"{Demo.Withdrawal:C} handed over");
            return Demo.Withdrawal;
        });

        await Task.Delay(2000);
        Show.Note($"{clock.ElapsedMilliseconds:N0} ms have passed and B has STILL not printed\n" +
                  "            anything. It has read nothing, so it cannot act on stale data.");

        // ---- step 4 ---------------------------------------------------------
        Show.Step(4, "REQUEST A", "checks the balance — and this time the check means something");
        Show.Result($"{walletA.Balance:C} >= {Demo.Withdrawal:C}  ->  allowed");
        Show.Result("nobody can change this row between the check and the write");

        // ---- step 5 ---------------------------------------------------------
        Show.Step(5, "REQUEST A", "writes and commits");
        walletA.Balance -= Demo.Withdrawal;
        await requestA.SaveChangesAsync();
        await txA.CommitAsync();
        paidOut += Demo.Withdrawal;
        Show.Sql("UPDATE [Wallets] SET [Balance] = 0 WHERE [Id] = 1;\n" +
                 "COMMIT;   -- releases the lock, wakes B up");
        Show.Good($"{Demo.Withdrawal:C} handed over. Lock released.");

        // B wakes up here and prints step 6 by itself.
        paidOut += await requestBTask;

        Show.Note("B was never able to read stale data, because it was never\n" +
                  "            able to read. The price is the wait you just watched.");

        Show.Scoreboard(Demo.StartingBalance, paidOut, await Demo.BalanceAsync());
    }
}
