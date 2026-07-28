using RaceDemo;
using RaceDemo.Scenarios;

// =============================================================================
//  Race conditions in EF Core + SQL Server — console demo
//
//    dotnet run          interactive menu
//    dotnet run -- 1     run scenario 1 straight away (handy on stage)
//    dotnet run -- all   run all four, in order
// =============================================================================

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length > 0)
{
    await RunAsync(args[0].Trim().ToLowerInvariant());
    return;
}

while (true)
{
    Menu();

    var key = Console.ReadKey(intercept: true).KeyChar.ToString().ToLowerInvariant();
    Console.WriteLine();

    if (key is "q" or "0") return;

    if (key == "s")
    {
        Demo.EchoSql = !Demo.EchoSql;
        Console.WriteLine($"\n  EF Core SQL logging is now {(Demo.EchoSql ? "ON" : "OFF")}.\n");
        continue;
    }

    await RunAsync(key);

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  press any key for the menu");
    Console.ResetColor();
    Console.ReadKey(intercept: true);
}

static void Menu()
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine();
    Console.WriteLine("  RACE CONDITIONS IN EF CORE + SQL SERVER");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  Alice has $100. Two requests each try to withdraw $100.");
    Console.WriteLine();
    Console.ResetColor();
    Console.WriteLine("    1   Broken            no lock, no concurrency token");
    Console.WriteLine("    2   Optimistic        RowVersion token, fail fast");
    Console.WriteLine("    3   Optimistic+retry  re-read and re-decide");
    Console.WriteLine("    4   Pessimistic       WITH (UPDLOCK, ROWLOCK)");
    Console.WriteLine("    a   run all four in order");
    Console.WriteLine();
    Console.WriteLine($"    s   show the SQL EF Core really sends   [{(Demo.EchoSql ? "ON" : "off")}]");
    Console.WriteLine("    q   quit");
    Console.Write("\n  > ");
}

static async Task RunAsync(string choice)
{
    try
    {
        switch (choice)
        {
            case "1": await Vulnerable.RunAsync();      break;
            case "2": await Optimistic.RunAsync();      break;
            case "3": await OptimisticRetry.RunAsync(); break;
            case "4": await Pessimistic.RunAsync();     break;

            case "a":
            case "all":
                await Vulnerable.RunAsync();
                await Optimistic.RunAsync();
                await OptimisticRetry.RunAsync();
                await Pessimistic.RunAsync();
                break;

            default:
                Console.WriteLine("  pick 1, 2, 3, 4, a, s or q");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine("  Could not reach SQL Server.");
        Console.WriteLine("  Start it with:  docker compose up -d");
        Console.WriteLine();
        Console.WriteLine("  " + ex.GetBaseException().Message);
        Console.ResetColor();
    }
}
