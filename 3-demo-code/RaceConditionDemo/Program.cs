using Microsoft.EntityFrameworkCore;
using RaceConditionDemo.Data;
using RaceConditionDemo.Endpoints;
using RaceConditionDemo.Models;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("Sql")
         ?? "Server=localhost,1433;Database=RaceDemo;User Id=sa;Password=Your_Strong!Passw0rd;TrustServerCertificate=True;Encrypt=False";

// -----------------------------------------------------------------------------
// Two contexts over ONE table. This is the pedagogical trick of the whole demo:
// the entity class and the table are identical, only the configuration differs.
// -----------------------------------------------------------------------------
builder.Services.AddDbContext<SafeDbContext>(o => o
    .UseSqlServer(cs)
    // Print every SQL statement to the console. During the talk, put the
    // terminal next to the browser: the audience literally reads the WHERE
    // clause change when you switch from vulnerable to optimistic.
    .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information)
    .EnableSensitiveDataLogging());   // demo only - never in production

builder.Services.AddDbContext<UnsafeDbContext>(o => o
    .UseSqlServer(cs)
    .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information)
    .EnableSensitiveDataLogging());

// HttpClient the demo driver uses to call this same app in parallel.
builder.Services.AddHttpClient("self")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Self-signed dev cert; demo only.
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// -----------------------------------------------------------------------------
// Create schema + seed. SafeDbContext owns the schema because it is the context
// that knows about the rowversion column.
// -----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafeDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (!await db.Wallets.AnyAsync())
    {
        // Ids are IDENTITY-generated; on an empty table these become 1 and 2,
        // which is what every demo request below assumes.
        db.Wallets.AddRange(
            new Wallet { Owner = "Alice (card A)", Balance = 1000m },
            new Wallet { Owner = "Bob (card B)", Balance = 1000m });
        await db.SaveChangesAsync();
    }
}

app.UseSwagger();
app.UseSwaggerUI(c => c.DocumentTitle = "Race Conditions in EF Core - live demo");

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapDemoEndpoints();
app.MapVulnerableEndpoints();
app.MapOptimisticEndpoints();
app.MapPessimisticEndpoints();
app.MapAtomicEndpoints();

app.Run();
