using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RaceDemo;

// =============================================================================
//  THE MOST IMPORTANT FILE IN THE DEMO.
//
//  Two DbContexts. Same Wallet class. Same dbo.Wallets table. Same database.
//  They differ by exactly ONE line, and that one line is the difference
//  between losing money and not.
//
//  Show this file on screen and read both OnModelCreating methods out loud.
// =============================================================================

/// <summary>
/// The context 90% of real codebases ship with. It does not know or care that
/// the RowVersion column exists.
///
/// SaveChanges() emits:
///     UPDATE [Wallets] SET [Balance] = @p0 WHERE [Id] = @p1
///
/// Read that WHERE clause. It matches on the primary key and nothing else.
/// It does NOT say "and only if nobody else changed the row since I read it".
/// </summary>
public class UnsafeDb : Db
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        //  vvv  THE DIFFERENCE  vvv
        b.Entity<Wallet>().Ignore(w => w.RowVersion);   // pretend the column isn't there
    }
}

/// <summary>
/// Identical, except RowVersion is declared as a concurrency token.
///
/// SaveChanges() now emits:
///     UPDATE [Wallets] SET [Balance] = @p0
///     WHERE [Id] = @p1 AND [RowVersion] = @p2
///                       ^^^^^^^^^^^^^^^^^^^^^
/// "Update this row only if it is still the version I read."
///
/// If somebody else already changed the row, that WHERE matches nothing,
/// @@ROWCOUNT is 0, and EF Core throws DbUpdateConcurrencyException.
/// </summary>
public class SafeDb : Db
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        //  vvv  THE DIFFERENCE  vvv
        b.Entity<Wallet>().Property(w => w.RowVersion).IsRowVersion();
    }
}

/// <summary>
/// Shared plumbing. Nothing interesting happens in here — it just points both
/// contexts at the same database and (optionally) echoes the real SQL.
/// </summary>
public abstract class Db : DbContext
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnConfiguring(DbContextOptionsBuilder b)
    {
        b.UseSqlServer(Demo.ConnectionString);

        // Press "s" in the menu to turn this on and prove the SQL below is real.
        b.LogTo(line => { if (Demo.EchoSql) Show.RawSql(line); },
                new[] { DbLoggerCategory.Database.Command.Name },
                LogLevel.Information,
                DbContextLoggerOptions.SingleLine);

        b.EnableSensitiveDataLogging();   // demo only — shows parameter values
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Wallet>(e =>
        {
            e.ToTable("Wallets");
            e.HasKey(w => w.Id);
            e.Property(w => w.Owner).HasMaxLength(100);
            e.Property(w => w.Balance).HasColumnType("decimal(18,2)");
        });
    }
}
