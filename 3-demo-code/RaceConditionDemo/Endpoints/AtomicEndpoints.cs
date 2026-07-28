using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Data;

namespace RaceConditionDemo.Endpoints;

// =============================================================================
//  BONUS - THE FIX NOBODY MENTIONS, AND OFTEN THE BEST ONE
// =============================================================================
//  If the whole operation can be expressed as ONE SQL statement, there is no
//  read-modify-write window to lose a race in. A single UPDATE is atomic:
//  the engine takes the necessary locks for the duration of the statement,
//  and the WHERE clause does the business check.
//
//      UPDATE Wallets
//      SET Balance = Balance - @amount     <-- relative, not absolute
//      WHERE Id = @id AND Balance >= @amount
//
//  Two things to point out on the slide:
//   * `Balance = Balance - @amount` never sends a stale number to the server.
//     The old code sent `Balance = 900` (a value computed in C# from a stale
//     read). This sends "subtract 100 from whatever is there".
//   * `AND Balance >= @amount` moves the business rule INTO the same statement.
//     Rows affected = 0 means "rejected, insufficient funds" - and that answer
//     is authoritative, not a guess based on a value read 40ms ago.
//
//  Cost: zero extra round trips, no retry loop, no lock held across app code.
//  Limit: only works when the new value is a pure function of the old one.
//  Also: EF Core's ExecuteUpdate bypasses the change tracker, so no navigation
//  fix-up, no SaveChanges interceptors, no domain events. Know what you lose.
// =============================================================================

public static class AtomicEndpoints
{
    public static void MapAtomicEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/atomic").WithTags("4. Atomic (single UPDATE)");

        g.MapPost("/withdraw", async (WithdrawRequest req, UnsafeDbContext db) =>
        {
            // EF Core 7+ : ExecuteUpdateAsync compiles straight to one UPDATE.
            var rowsAffected = await db.Wallets
                .Where(w => w.Id == req.WalletId && w.Balance >= req.Amount)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    w => w.Balance,
                    w => w.Balance - req.Amount));

            if (rowsAffected == 0)
            {
                // Either the wallet does not exist, or the guard failed.
                // Distinguish only if you actually need to.
                var exists = await db.Wallets.AnyAsync(w => w.Id == req.WalletId);
                return exists
                    ? Results.BadRequest(new { error = "Insufficient funds" })
                    : Results.NotFound();
            }

            var balance = await db.Wallets
                .Where(w => w.Id == req.WalletId)
                .Select(w => w.Balance)
                .FirstAsync();

            return Results.Ok(new { id = req.WalletId, balance, rowsAffected });
        })
        .WithSummary("Safe: single atomic UPDATE with the guard in the WHERE clause");

        // Same idea written as raw SQL, for teams not yet on EF Core 7+.
        g.MapPost("/withdraw-raw", async (WithdrawRequest req, UnsafeDbContext db) =>
        {
            var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [Wallets]
                SET [Balance] = [Balance] - {req.Amount}
                WHERE [Id] = {req.WalletId} AND [Balance] >= {req.Amount}");

            return rowsAffected == 0
                ? Results.BadRequest(new { error = "Insufficient funds or wallet not found" })
                : Results.Ok(new { id = req.WalletId, rowsAffected });
        })
        .WithSummary("Safe: same thing via ExecuteSqlInterpolated (pre-EF7)");
    }
}
