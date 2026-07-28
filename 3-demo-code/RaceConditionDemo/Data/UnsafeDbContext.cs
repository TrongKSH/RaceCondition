using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Models;

namespace RaceConditionDemo.Data;

/// <summary>
/// The "normal looking" context that 90% of real codebases ship with.
///
/// It maps the SAME dbo.Wallets table, but it does NOT declare RowVersion as a
/// concurrency token - in fact it ignores the property entirely, so EF Core
/// behaves exactly as if the column did not exist.
///
/// UPDATE emitted by SaveChanges():
///     UPDATE [Wallets] SET [Balance] = @p0 WHERE [Id] = @p1
///
/// Note the WHERE clause: it only matches on the primary key. It does NOT say
/// "...and only if the balance is still what I read a moment ago". That is the
/// entire bug. This is a classic LOST UPDATE.
///
/// This context is also used by the PESSIMISTIC demo, to prove that a database
/// lock alone is enough - no version column required.
/// </summary>
public class UnsafeDbContext : DbContext
{
    public UnsafeDbContext(DbContextOptions<UnsafeDbContext> options) : base(options) { }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Wallet>(e =>
        {
            e.ToTable("Wallets");
            e.HasKey(w => w.Id);
            e.Property(w => w.Owner).HasMaxLength(100).IsRequired();
            e.Property(w => w.Balance).HasColumnType("decimal(18,2)");

            // Pretend the column is not there. No concurrency token.
            e.Ignore(w => w.RowVersion);
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.ToTable("Ledger");
            e.HasKey(l => l.Id);
            e.Property(l => l.Operation).HasMaxLength(50);
            e.Property(l => l.Amount).HasColumnType("decimal(18,2)");
            e.Property(l => l.BalanceAfter).HasColumnType("decimal(18,2)");
            e.HasIndex(l => l.WalletId);
        });
    }
}
