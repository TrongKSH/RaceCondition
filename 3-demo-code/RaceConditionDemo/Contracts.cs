namespace RaceConditionDemo;

/// <param name="WalletId">Wallet to withdraw from.</param>
/// <param name="Amount">How much to take out.</param>
/// <param name="ThinkTimeMs">
/// Artificial delay injected between READ and WRITE.
/// This does NOT create the bug - it only widens the window so the bug is
/// reproducible on a laptop instead of once every 10,000 production requests.
/// Set it to 0 and hammer with 200 threads and you will still see it.
/// </param>
public record WithdrawRequest(int WalletId, decimal Amount, int ThinkTimeMs = 150);

public record TransferRequest(int FromWalletId, int ToWalletId, decimal Amount, int ThinkTimeMs = 150);

/// <param name="Mode">vulnerable | optimistic | optimistic-retry | pessimistic | atomic</param>
public record DemoRunRequest(
    string Mode = "vulnerable",
    int WalletId = 1,
    decimal StartingBalance = 1000m,
    decimal Amount = 100m,
    int Concurrency = 20,
    int ThinkTimeMs = 150);

public record DemoRunResult(
    string Mode,
    int Concurrency,
    decimal Amount,
    decimal StartingBalance,
    decimal ExpectedBalance,
    decimal ActualBalance,
    int Http200_Succeeded,
    int Http409_Conflict,
    int Http400_Rejected,
    int Http500_Error,
    decimal MoneyCreatedFromThinAir,
    long ElapsedMs,
    string Verdict);
