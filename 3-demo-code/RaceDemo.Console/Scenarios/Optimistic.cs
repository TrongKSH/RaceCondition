using Microsoft.EntityFrameworkCore;

namespace RaceDemo.Scenarios;

// =============================================================================
//  2 · OPTIMISTIC  —  RowVersion
//
//  Exactly the same six steps as scenario 1. The ONLY change is SafeDb
//  instead of UnsafeDb, which adds one line of model configuration:
//
//      builder.Property(w => w.RowVersion).IsRowVersion();
//
//  Run scenario 1 and 2 back to back and diff them on screen.
// =============================================================================

public static class Optimistic
{
    public static async Task RunAsync()
    {
        Show.Title("2 · OPTIMISTIC  —  RowVersion concurrency token");
        Show.Intro("Same story, same six steps. One line of configuration different.\n" +
                   "  The bet: collisions are rare, so don't pay for a lock — just\n" +
                   "  make sure the loser finds out.");

        await Demo.ResetAsync();

        //            vvvvvv   the only change from scenario 1
        using var requestA = new SafeDb();
        using var requestB = new SafeDb();

        var paidOut = 0m;

        // ---- step 1 ---------------------------------------------------------
        Show.Step(1, "REQUEST A", "reads the balance AND the RowVersion");
        var walletA = await requestA.Wallets.FirstAsync(w => w.Id == 1);
        Show.Sql("SELECT TOP(1) [Id], [Owner], [Balance], [RowVersion] FROM [Wallets] WHERE [Id] = 1");
        Show.Result($"A holds {walletA.Balance:C} at RowVersion {Hex(walletA.RowVersion)}");

        // ---- step 2 ---------------------------------------------------------
        Show.Step(2, "REQUEST B", "reads the balance AND the RowVersion");
        var walletB = await requestB.Wallets.FirstAsync(w => w.Id == 1);
        Show.Sql("SELECT TOP(1) [Id], [Owner], [Balance], [RowVersion] FROM [Wallets] WHERE [Id] = 1");
        Show.Result($"B holds {walletB.Balance:C} at RowVersion {Hex(walletB.RowVersion)}");
        Show.Note("Note what did NOT happen: B still read stale data. This strategy\n" +
                  "            never prevents that. It guarantees B will FIND OUT.");

        // ---- steps 3 and 4 --------------------------------------------------
        Show.Step(3, "REQUEST A", "checks the balance  ->  allowed");
        Show.Step(4, "REQUEST B", "checks the balance  ->  allowed");

        // ---- step 5 ---------------------------------------------------------
        Show.Step(5, "REQUEST A", "writes");
        walletA.Balance -= Demo.Withdrawal;
        await requestA.SaveChangesAsync();
        paidOut += Demo.Withdrawal;
        Show.Sql("UPDATE [Wallets] SET [Balance] = 0\n" +
                 "OUTPUT INSERTED.[RowVersion]\n" +
                 "WHERE [Id] = 1 AND [RowVersion] = <the value A read>");
        Show.Good($"RowVersion matched. 1 row affected. {Demo.Withdrawal:C} handed over.");
        Show.Result($"SQL Server bumped the row to RowVersion {Hex(walletA.RowVersion)}");
        Show.Note("That WHERE clause is the entire fix. Read it out loud:\n" +
                  "            \"update this row only if it is still the version I read.\"");

        // ---- step 6 ---------------------------------------------------------
        Show.Step(6, "REQUEST B", "writes — carrying a RowVersion that no longer exists");
        walletB.Balance -= Demo.Withdrawal;
        try
        {
            await requestB.SaveChangesAsync();
            Show.Bad("This line should be unreachable.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Show.Sql("UPDATE [Wallets] SET [Balance] = 0\n" +
                     "WHERE [Id] = 1 AND [RowVersion] = <the value B read>\n" +
                     "-- 0 rows affected");

            // ex.Entries tells you exactly which entities lost the race.
            var entry   = ex.Entries.Single();
            var current = await entry.GetDatabaseValuesAsync();
            var actual  = current?.GetValue<decimal>(nameof(Wallet.Balance));

            Show.Warn("DbUpdateConcurrencyException — EF expected 1 row, got 0.");
            Show.Result($"B thought the balance was {Demo.StartingBalance:C}. It is really {actual:C}.");
            Show.Result("Your API turns this into HTTP 409 Conflict. No money moved.");
        }

        Show.Note("The database never threw. It simply matched no rows.\n" +
                  "            Noticing that is EF Core's job, not SQL Server's.");

        Show.Scoreboard(Demo.StartingBalance, paidOut, await Demo.BalanceAsync());
    }

    /// <summary>Show a rowversion the way SQL Server does: 0x0000000000000929</summary>
    private static string Hex(byte[]? v) => v is null ? "(none)" : "0x" + Convert.ToHexString(v);
}
