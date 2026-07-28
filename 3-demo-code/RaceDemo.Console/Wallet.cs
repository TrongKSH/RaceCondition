namespace RaceDemo;

/// <summary>
/// One wallet. One row in dbo.Wallets. That is the entire data model.
/// </summary>
public class Wallet
{
    public int Id { get; set; }

    public string Owner { get; set; } = "";

    public decimal Balance { get; set; }

    /// <summary>
    /// Maps to SQL Server's `rowversion` type.
    ///
    /// SQL Server sets this itself and bumps it on EVERY update of the row.
    /// You never assign it. It is 8 bytes and database-wide monotonic.
    ///
    /// The column exists in the table for all four scenarios. What differs is
    /// whether the DbContext is told to CARE about it — see AppDb.cs.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
