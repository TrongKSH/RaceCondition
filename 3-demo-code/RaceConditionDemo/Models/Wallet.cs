namespace RaceConditionDemo.Models;

/// <summary>
/// ONE entity class, ONE physical table (dbo.Wallets).
///
/// The interesting part of this demo is that the C# class is identical for the
/// vulnerable and the safe path. What changes is how the DbContext *configures*
/// it (see Data/UnsafeDbContext.cs vs Data/SafeDbContext.cs).
///
/// That is the single most important takeaway of the session:
///   EF Core will not protect you from a lost update unless you explicitly
///   tell it to. There is no "safe by default".
/// </summary>
public class Wallet
{
    public int Id { get; set; }

    public string Owner { get; set; } = string.Empty;

    /// <summary>Money, in whole currency units. decimal(18,2) in SQL Server.</summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Maps to a SQL Server `rowversion` (a.k.a. `timestamp`) column.
    /// SQL Server auto-increments this value on EVERY update of the row.
    /// It is 8 bytes, database-wide monotonic, and you never set it yourself.
    ///
    /// SafeDbContext marks it as a concurrency token -> EF appends it to the
    /// WHERE clause of UPDATE/DELETE.
    /// UnsafeDbContext ignores it completely -> EF never looks at it.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Append-only audit trail. Useful in the demo to prove that N withdrawals
/// "succeeded" while the balance only moved once.
/// </summary>
public class LedgerEntry
{
    public long Id { get; set; }
    public int WalletId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
