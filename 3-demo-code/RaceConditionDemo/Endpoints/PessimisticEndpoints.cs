using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Data;
using RaceConditionDemo.Models;

namespace RaceConditionDemo.Endpoints;

// =============================================================================
//  STEP 5 OF THE TALK - THE PESSIMISTIC FIX (WITH (UPDLOCK, ROWLOCK))
// =============================================================================
//  Assumption: collisions are COMMON, or a retry is unacceptable / expensive.
//  So don't detect the collision - PREVENT it. Serialise at the database.
//
//  UPDLOCK  = take an Update lock at SELECT time instead of a Shared lock.
//             Update locks are not compatible with each other, so a second
//             reader BLOCKS instead of reading a stale value.
//  ROWLOCK  = hint the engine to stay at row granularity (avoid page/table
//             lock escalation blocking unrelated wallets).
//  HOLDLOCK = add this if you also need to block INSERTs into the range
//             (protects against phantoms; not needed for a single-row read).
//
//  Two hard requirements, both easy to get wrong:
//    1. The SELECT ... UPDLOCK must be INSIDE an explicit transaction.
//       Without a transaction the lock is released the instant the statement
//       finishes, and you are back to square one.
//    2. Nothing slow may run between the lock and the COMMIT. Every millisecond
//       there is a millisecond every other request is blocked.
// =============================================================================

public static class PessimisticEndpoints
{
    public static void MapPessimisticEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/pessimistic").WithTags("3. Pessimistic (UPDLOCK)");

        // ---------------------------------------------------------------------
        // Note this uses UnsafeDbContext on purpose: there is NO RowVersion
        // token in play. The lock alone is doing all the work.
        // ---------------------------------------------------------------------
        g.MapPost("/withdraw", async (WithdrawRequest req, UnsafeDbContext db) =>
        {
            // 1. Open the transaction FIRST. The lock lives and dies with it.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                // 2. SELECT with the lock hint.
                //    FromSqlInterpolated parameterises {req.WalletId} - it becomes
                //    @p0, NOT string concatenation. No SQL injection here.
                //
                //    We materialise with ToListAsync() rather than composing
                //    .FirstOrDefaultAsync(), so EF sends our SQL verbatim instead
                //    of wrapping it in a derived table. Keeps the emitted SQL
                //    identical to what you show on the slide.
                //
                //    The column list must cover every property the context maps
                //    (UnsafeDbContext ignores RowVersion, so it is not listed).
                var wallet = (await db.Wallets
                        .FromSqlInterpolated($@"
                            SELECT [Id], [Owner], [Balance]
                            FROM [Wallets] WITH (UPDLOCK, ROWLOCK)
                            WHERE [Id] = {req.WalletId}")
                        .ToListAsync())
                    .FirstOrDefault();

                // ---- FROM HERE UNTIL COMMIT, THIS ROW IS OURS ALONE ----------
                // A second request executing the same SELECT blocks right here.

                if (wallet is null)
                {
                    await tx.RollbackAsync();
                    return Results.NotFound();
                }

                // 3. Now the guard is trustworthy: nobody can change the row
                //    between this check and our UPDATE.
                if (wallet.Balance < req.Amount)
                {
                    await tx.RollbackAsync();
                    return Results.BadRequest(new { error = "Insufficient funds", wallet.Balance });
                }

                // Deliberately kept here to show blocking during the demo.
                // In production: do NOT hold a lock across an await like this.
                if (req.ThinkTimeMs > 0)
                    await Task.Delay(req.ThinkTimeMs);

                wallet.Balance -= req.Amount;

                db.Ledger.Add(new LedgerEntry
                {
                    WalletId = wallet.Id,
                    Operation = "pessimistic-withdraw",
                    Amount = req.Amount,
                    BalanceAfter = wallet.Balance
                });

                await db.SaveChangesAsync();

                // 4. COMMIT releases the lock. The next request wakes up and
                //    reads the NEW balance - which is the whole point.
                await tx.CommitAsync();

                return Results.Ok(new { wallet.Id, wallet.Balance });
            }
            // A deadlock raised during SaveChanges arrives wrapped in a
            // DbUpdateException, so unwrap before inspecting the error number.
            catch (Exception ex) when (Unwrap(ex) is SqlException sql && sql.Number is 1205 or -2)
            {
                // 1205 = chosen as deadlock victim -> the whole transaction was
                //        rolled back by SQL Server; retrying is safe and correct.
                //   -2 = lock request timed out (SET LOCK_TIMEOUT / command timeout)
                //        -> the row is hot; shed load rather than pile on.
                await tx.RollbackAsync();
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        })
        .WithSummary("Safe: SELECT ... WITH (UPDLOCK, ROWLOCK) inside a transaction");

        // ---------------------------------------------------------------------
        // Transfer with DETERMINISTIC LOCK ORDERING.
        //
        // The senior-engineer footnote: pessimistic locking on two rows will
        // deadlock if request A locks (1 then 2) while request B locks (2 then 1).
        // SQL Server will pick a victim and throw error 1205.
        //
        // Fix: always acquire locks in the same global order. Sorting by primary
        // key is the cheapest total order available.
        // ---------------------------------------------------------------------
        g.MapPost("/transfer", async (TransferRequest req, UnsafeDbContext db) =>
        {
            if (req.FromWalletId == req.ToWalletId)
                return Results.BadRequest(new { error = "Same wallet" });

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            var first = Math.Min(req.FromWalletId, req.ToWalletId);
            var second = Math.Max(req.FromWalletId, req.ToWalletId);

            // Single statement, both rows, ORDER BY Id -> consistent lock order.
            var wallets = await db.Wallets
                .FromSqlInterpolated($@"
                    SELECT [Id], [Owner], [Balance]
                    FROM [Wallets] WITH (UPDLOCK, ROWLOCK)
                    WHERE [Id] IN ({first}, {second})
                    ORDER BY [Id]")
                .ToListAsync();

            var from = wallets.FirstOrDefault(w => w.Id == req.FromWalletId);
            var to = wallets.FirstOrDefault(w => w.Id == req.ToWalletId);

            if (from is null || to is null)
            {
                await tx.RollbackAsync();
                return Results.NotFound();
            }

            if (from.Balance < req.Amount)
            {
                await tx.RollbackAsync();
                return Results.BadRequest(new { error = "Insufficient funds" });
            }

            if (req.ThinkTimeMs > 0)
                await Task.Delay(req.ThinkTimeMs);

            from.Balance -= req.Amount;
            to.Balance += req.Amount;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            db.ChangeTracker.Clear();

            return Results.Ok(new
            {
                from = new { from.Id, from.Balance },
                to = new { to.Id, to.Balance },
                systemTotal = from.Balance + to.Balance
            });
        })
        .WithSummary("Safe: locked transfer with deterministic lock ordering");
    }

    /// <summary>Walk the InnerException chain to find the provider exception.</summary>
    private static Exception Unwrap(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null && current is not SqlException)
            current = current.InnerException;
        return current;
    }
}
