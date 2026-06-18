using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Tests.TestData;

public class StubRateSource : IRateSource
{
    public string SourceCode => "CBU";
    // deterministic test rates (UZS per 1 unit)
    public Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourceRate>>(new List<SourceRate>
        {
            new("USD", 12000m, null, null),
            new("RUB", 160m, null, null),
            new("EUR", 13000m, null, null),
        });
}
