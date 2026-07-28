using Microsoft.EntityFrameworkCore;

namespace RaceDemo.Scenarios;

// =============================================================================
//  1 · BROKEN
//
//  Alice has $100. Two requests each try to withdraw $100.
//  Read this method top to bottom — it is the sequence diagram, as code.
// =============================================================================

public static class Vulnerable
{
    public static async Task RunAsync()
    {
        Show.Title("1 · BROKEN  —  no lock, no concurrency token");
        Show.Intro("Alice has $100. She taps Withdraw on her phone, and her laptop " +
                   "sends the\n  same request 8 ms later. Two requests, one row.");

        await Demo.ResetAsync();

        // ---------------------------------------------------------------------
        // Two requests = two DbContexts.
        //
        // In a web app you never see this: DI hands each HTTP request its own
        // scoped DbContext. Here we create them by hand so it is visible.
        // THIS is why each request ends up with its own private copy.
        // ---------------------------------------------------------------------
        using var requestA = new UnsafeDb();
        using var requestB = new UnsafeDb();

        var paidOut = 0m;

        // ---- step 1 ---------------------------------------------------------
        Show.Step(1, "REQUEST A", "reads the balance");
        var walletA = await requestA.Wallets.FirstAsync(w => w.Id == 1);
        Show.Sql("SELECT TOP(1) [Id], [Owner], [Balance] FROM [Wallets] WHERE [Id] = 1");
        Show.Result($"A now holds its own copy: {walletA.Balance:C}");

        // ---- step 2 ---------------------------------------------------------
        Show.Step(2, "REQUEST B", "reads the balance");
        var walletB = await requestB.Wallets.FirstAsync(w => w.Id == 1);
        Show.Sql("SELECT TOP(1) [Id], [Owner], [Balance] FROM [Wallets] WHERE [Id] = 1");
        Show.Result($"B holds its own copy too: {walletB.Balance:C}");
        Show.Note("Both requests now believe they own the same $100. " +
                  "Nothing written yet,\n            so nobody is wrong — and nobody can tell.");

        // ---- step 3 ---------------------------------------------------------
        Show.Step(3, "REQUEST A", "checks whether there is enough money");
        if (walletA.Balance < Demo.Withdrawal) { Show.Bad("declined"); return; }
        Show.Sql("(no SQL — this runs in C#, against A's copy)");
        Show.Result($"{walletA.Balance:C} >= {Demo.Withdrawal:C}  ->  allowed");

        // ---- step 4 ---------------------------------------------------------
        Show.Step(4, "REQUEST B", "checks whether there is enough money");
        if (walletB.Balance < Demo.Withdrawal) { Show.Bad("declined"); return; }
        Show.Sql("(no SQL — this runs in C#, against B's copy)");
        Show.Result($"{walletB.Balance:C} >= {Demo.Withdrawal:C}  ->  allowed as well");
        Show.Note("The insufficient-funds guard just passed TWICE for one $100.");

        // ---- step 5 ---------------------------------------------------------
        Show.Step(5, "REQUEST A", "pays out and writes the new balance");
        walletA.Balance -= Demo.Withdrawal;
        await requestA.SaveChangesAsync();
        paidOut += Demo.Withdrawal;
        Show.Sql($"UPDATE [Wallets] SET [Balance] = {walletA.Balance} WHERE [Id] = 1");
        Show.Good($"1 row affected. {Demo.Withdrawal:C} handed over.");
        Show.Note("Read that UPDATE literally: \"set the balance to zero\".\n" +
                  "            Not \"subtract 100\". Not \"only if it is still 100\".");

        // ---- step 6 ---------------------------------------------------------
        Show.Step(6, "REQUEST B", "pays out and writes the new balance");
        walletB.Balance -= Demo.Withdrawal;   // computed from B's stale copy
        await requestB.SaveChangesAsync();
        paidOut += Demo.Withdrawal;
        Show.Sql($"UPDATE [Wallets] SET [Balance] = {walletB.Balance} WHERE [Id] = 1");
        Show.Bad($"1 row affected. Another {Demo.Withdrawal:C} handed over.");
        Show.Note("No exception. No warning. The database is perfectly happy —\n" +
                  "            it was asked to set a number, and it set it.");

        Show.Scoreboard(Demo.StartingBalance, paidOut, await Demo.BalanceAsync());
    }
}
