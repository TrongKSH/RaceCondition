using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Models;

namespace RaceConditionDemo.Data;

/// <summary>
/// The OPTIMISTIC-CONCURRENCY context.
///
/// Identical to UnsafeDbContext except for ONE line:
///     .IsRowVersion()
///
/// That single line changes the UPDATE statement EF Core emits from
///     UPDATE Wallets SET Balance = @b WHERE Id = @id
/// to
///     UPDATE Wallets SET Balance = @b WHERE Id = @id AND RowVersion = @rv
///
/// If another request already changed the row, RowVersion no longer matches,
/// @@ROWCOUNT is 0, and EF Core throws DbUpdateConcurrencyException.
///
/// This context also owns the schema (EnsureCreated), because it is the one
/// that knows about the rowversion column.
/// </summary>
public class SafeDbContext : DbContext
{
    public SafeDbContext(DbContextOptions<SafeDbContext> options) : base(options) { }

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

            // ---------------------------------------------------------------
            // THE ONE LINE THAT MATTERS.
            // IsRowVersion() == IsConcurrencyToken() + ValueGeneratedOnAddOrUpdate()
            // and maps the property to the SQL Server `rowversion` type.
            // Equivalent data-annotation: [Timestamp] on the property.
            // ---------------------------------------------------------------
            e.Property(w => w.RowVersion).IsRowVersion();
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
