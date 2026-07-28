using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Data;
using RaceConditionDemo.Models;

namespace RaceConditionDemo.Endpoints;

// =============================================================================
//  THE DEMO DRIVER - one call, one screenshot-able answer.
// =============================================================================
//  POST /api/demo/run { "mode": "vulnerable", "concurrency": 20 }
//
//  It resets the wallet, fires N parallel withdrawals at the endpoint you
//  named, then reports expected vs actual balance.
//
//  On stage you run the exact same command five times, changing only "mode",
//  and let the numbers do the talking. No IDE, no debugger, no tab switching.
// =============================================================================

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/demo").WithTags("0. Demo driver");

        g.MapGet("/wallets", async (SafeDbContext db) =>
            await db.Wallets.AsNoTracking()
                .Select(w => new { w.Id, w.Owner, w.Balance })
                .ToListAsync())
         .WithSummary("Current state of every wallet");

        g.MapPost("/reset", async (SafeDbContext db, decimal balance = 1000m) =>
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM [Ledger]");
            await db.Wallets.ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, balance));
            db.ChangeTracker.Clear();
            return Results.Ok(new { message = "reset", balance });
        })
        .WithSummary("Reset every wallet to a known balance");

        g.MapPost("/run", async (
            DemoRunRequest req,
            HttpContext ctx,
            IHttpClientFactory httpFactory,
            SafeDbContext db) =>
        {
            var path = req.Mode.ToLowerInvariant() switch
            {
                "vulnerable"       => "/api/vulnerable/withdraw",
                "optimistic"       => "/api/optimistic/withdraw",
                "optimistic-retry" => "/api/optimistic/withdraw-with-retry",
                "pessimistic"      => "/api/pessimistic/withdraw",
                "atomic"           => "/api/atomic/withdraw",
                _ => throw new ArgumentException(
                        "mode must be: vulnerable | optimistic | optimistic-retry | pessimistic | atomic")
            };

            // ---- arrange: put the wallet in a known state -------------------
            await db.Database.ExecuteSqlRawAsync("DELETE FROM [Ledger]");
            await db.Wallets
                .Where(w => w.Id == req.WalletId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, req.StartingBalance));
            db.ChangeTracker.Clear();

            // ---- act: N genuinely parallel HTTP requests --------------------
            var client = httpFactory.CreateClient("self");
            client.BaseAddress = new Uri($"{ctx.Request.Scheme}://{ctx.Request.Host}");

            var payload = new WithdrawRequest(req.WalletId, req.Amount, req.ThinkTimeMs);

            var sw = Stopwatch.StartNew();
            var responses = await Task.WhenAll(
                Enumerable.Range(0, req.Concurrency).Select(async _ =>
                {
                    try
                    {
                        var r = await client.PostAsJsonAsync(path, payload);
                        return (int)r.StatusCode;
                    }
                    catch
                    {
                        return 500;
                    }
                }));
            sw.Stop();

            // ---- assert: what does the database actually say? ---------------
            db.ChangeTracker.Clear();
            var actual = await db.Wallets
                .Where(w => w.Id == req.WalletId)
                .Select(w => w.Balance)
                .FirstAsync();

            var ok = responses.Count(c => c == (int)HttpStatusCode.OK);
            var conflict = responses.Count(c => c == (int)HttpStatusCode.Conflict);
            var rejected = responses.Count(c => c == (int)HttpStatusCode.BadRequest);
            var errored = responses.Count(c => c >= 500);

            // What the balance SHOULD be if every 200 OK really moved money.
            var expected = req.StartingBalance - (ok * req.Amount);

            // Money that appeared from nowhere: the gap between the withdrawals
            // the API confirmed and the money the database actually gave up.
            var phantom = actual - expected;

            var verdict = phantom == 0m && actual >= 0m
                ? $"CORRECT - {ok} withdrawals confirmed, {ok * req.Amount:N2} actually debited, balance {actual:N2}."
                : $"BROKEN - API confirmed {ok} withdrawals ({ok * req.Amount:N2}) but only " +
                  $"{req.StartingBalance - actual:N2} left the wallet. {phantom:N2} created from thin air.";

            return Results.Ok(new DemoRunResult(
                Mode: req.Mode,
                Concurrency: req.Concurrency,
                Amount: req.Amount,
                StartingBalance: req.StartingBalance,
                ExpectedBalance: expected,
                ActualBalance: actual,
                Http200_Succeeded: ok,
                Http409_Conflict: conflict,
                Http400_Rejected: rejected,
                Http500_Error: errored,
                MoneyCreatedFromThinAir: phantom,
                ElapsedMs: sw.ElapsedMilliseconds,
                Verdict: verdict));
        })
        .WithSummary("Fire N parallel withdrawals at one mode and report the damage");

        // ---------------------------------------------------------------------
        // The Starbucks scenario, as a one-click reproduction.
        // Two concurrent transfers of the SAME money from card A to card B.
        // Watch `systemTotalAfter` exceed `systemTotalBefore`.
        // ---------------------------------------------------------------------
        g.MapPost("/starbucks", async (
            HttpContext ctx,
            IHttpClientFactory httpFactory,
            SafeDbContext db,
            string mode = "vulnerable",
            decimal amount = 5m,
            int thinkTimeMs = 150) =>
        {
            var path = mode == "vulnerable"
                ? "/api/vulnerable/transfer"
                : "/api/pessimistic/transfer";

            // Card A = 5.00, Card B = 5.00. Total in the system: 10.00
            await db.Wallets.Where(w => w.Id == 1)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, 5m));
            await db.Wallets.Where(w => w.Id == 2)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, 5m));
            db.ChangeTracker.Clear();

            var client = httpFactory.CreateClient("self");
            client.BaseAddress = new Uri($"{ctx.Request.Scheme}://{ctx.Request.Host}");

            var body = new TransferRequest(1, 2, amount, thinkTimeMs);

            // Homakov used two browsers with two session cookies.
            // We use two HTTP requests. Same thing.
            var codes = await Task.WhenAll(
                Enumerable.Range(0, 2).Select(async _ =>
                {
                    var r = await client.PostAsJsonAsync(path, body);
                    return (int)r.StatusCode;
                }));

            db.ChangeTracker.Clear();
            var after = await db.Wallets.AsNoTracking()
                .Where(w => w.Id == 1 || w.Id == 2)
                .Select(w => new { w.Id, w.Owner, w.Balance })
                .ToListAsync();

            var total = after.Sum(w => w.Balance);

            return Results.Ok(new
            {
                mode,
                systemTotalBefore = 10.00m,
                systemTotalAfter = total,
                createdFromThinAir = total - 10.00m,
                cards = after,
                httpStatusCodes = codes,
                verdict = total > 10.00m
                    ? "MONEY PRINTED. This is exactly the 2015 Starbucks gift-card bug."
                    : "Conserved. Total in = total out."
            });
        })
        .WithSummary("Reproduce the 2015 Starbucks gift-card exploit");
    }
}
