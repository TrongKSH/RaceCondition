using Microsoft.EntityFrameworkCore;

namespace RaceDemo;

/// <summary>Settings and small database helpers. Nothing to teach here.</summary>
public static class Demo
{
    /// <summary>
    /// Points at the same Docker SQL Server as the API demo, but its own database
    /// so the two projects never fight over the same row.
    ///   docker compose up -d      (compose file sits next to this project)
    /// </summary>
    public const string ConnectionString =
        "Server=localhost,1433;Database=RaceDemoConsole;User Id=sa;" +
        "Password=Your_Strong!Passw0rd;TrustServerCertificate=True;Encrypt=False";

    /// <summary>How much Alice has before each scenario starts.</summary>
    public const decimal StartingBalance = 100m;

    /// <summary>How much each of the two requests tries to withdraw.</summary>
    public const decimal Withdrawal = 100m;

    /// <summary>Toggled with "s" in the menu — echoes the SQL EF Core really sent.</summary>
    public static bool EchoSql;

    /// <summary>Create the table if needed and put the wallet back to a known balance.</summary>
    public static async Task ResetAsync()
    {
        // SafeDb owns the schema because it is the context that knows about
        // the rowversion column.
        using var db = new SafeDb();
        await db.Database.EnsureCreatedAsync();

        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == 1);
        if (wallet is null)
        {
            db.Wallets.Add(new Wallet { Owner = "Alice", Balance = StartingBalance });
            await db.SaveChangesAsync();
        }
        else
        {
            await db.Wallets
                .Where(w => w.Id == 1)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, StartingBalance));
        }
    }

    /// <summary>Read the balance straight from the database, bypassing any cache.</summary>
    public static async Task<decimal> BalanceAsync()
    {
        using var db = new SafeDb();
        return await db.Wallets.AsNoTracking()
            .Where(w => w.Id == 1)
            .Select(w => w.Balance)
            .FirstAsync();
    }
}
