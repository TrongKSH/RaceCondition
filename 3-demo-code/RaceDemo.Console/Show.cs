namespace RaceDemo;

/// <summary>
/// Console output. Purely cosmetic — but the whole demo is read off this screen,
/// so it is worth the few lines.
/// </summary>
public static class Show
{
    private static readonly object Gate = new();   // Pessimistic runs on 2 threads

    private const int Width = 74;

    private static void Write(string text, ConsoleColor colour)
    {
        lock (Gate)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = colour;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }
    }

    public static void Title(string text)
    {
        Write("", ConsoleColor.Gray);
        Write(new string('=', Width), ConsoleColor.DarkCyan);
        Write("  " + text, ConsoleColor.White);
        Write(new string('=', Width), ConsoleColor.DarkCyan);
    }

    public static void Intro(string text)
    {
        Write("", ConsoleColor.Gray);
        Write("  " + text, ConsoleColor.Gray);
        Write("", ConsoleColor.Gray);
    }

    /// <summary>A numbered step in the timeline. `who` is REQUEST A / REQUEST B.</summary>
    public static void Step(int number, string who, string what)
    {
        var colour = who.EndsWith("A") ? ConsoleColor.Cyan : ConsoleColor.Magenta;
        Write("", ConsoleColor.Gray);
        Write($"  [{number}] {who,-10} {what}", colour);
    }

    /// <summary>The SQL that step sends. Hand-written so it reads cleanly on a projector.</summary>
    public static void Sql(string sql)
    {
        foreach (var line in sql.Split('\n'))
            Write("      SQL   " + line.TrimEnd(), ConsoleColor.DarkGray);
    }

    /// <summary>What actually came back.</summary>
    public static void Result(string text)   => Write("      ->    " + text, ConsoleColor.Gray);

    public static void Good(string text)     => Write("      ->    " + text, ConsoleColor.Green);
    public static void Bad(string text)      => Write("      ->    " + text, ConsoleColor.Red);
    public static void Warn(string text)     => Write("      ->    " + text, ConsoleColor.Yellow);

    /// <summary>An aside to the audience, not an action by either request.</summary>
    public static void Note(string text)
    {
        Write("", ConsoleColor.Gray);
        Write("      ..... " + text, ConsoleColor.DarkYellow);
    }

    /// <summary>EF Core's own log, when the "s" toggle is on.</summary>
    public static void RawSql(string line)   => Write("      ef>   " + line.Trim(), ConsoleColor.DarkBlue);

    /// <summary>
    /// The scoreboard. This is the punchline of every scenario — always finish on it.
    /// </summary>
    public static void Scoreboard(decimal started, decimal paidOut, decimal inDatabase)
    {
        var leftTheWallet = started - inDatabase;
        var invented      = paidOut - leftTheWallet;

        Write("", ConsoleColor.Gray);
        Write("  " + new string('-', Width - 2), ConsoleColor.DarkGray);
        Write($"    Wallet started with       {started,10:C}", ConsoleColor.Gray);
        Write($"    Cash handed to Alice      {paidOut,10:C}", ConsoleColor.Gray);
        Write($"    Balance in the database   {inDatabase,10:C}", ConsoleColor.Gray);

        if (invented > 0)
        {
            Write($"    MONEY FROM NOWHERE        {invented,10:C}   <-- !!",
                  ConsoleColor.Red);
            Write("  " + new string('-', Width - 2), ConsoleColor.DarkGray);
            Write("    Both requests returned success. No exception. No log line.",
                  ConsoleColor.Red);
        }
        else
        {
            Write($"    Money from nowhere        {invented,10:C}", ConsoleColor.Green);
            Write("  " + new string('-', Width - 2), ConsoleColor.DarkGray);
            Write("    Every unit accounted for.", ConsoleColor.Green);
        }
        Write("", ConsoleColor.Gray);
    }
}
