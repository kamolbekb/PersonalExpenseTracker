using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Tests.TestData;

public class StubGoldSource : IGoldSource
{
    public Task<IReadOnlyList<SourceGold>> FetchAsync(DateOnly date, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourceGold>>(new List<SourceGold>
        { new("5 g", 9243000m, 8281000m), new("100 g", 184861000m, 165622000m) });
}
