using ExpenseTracker.Application.Rates;

namespace ExpenseTracker.Api.Endpoints;

public static class RatesEndpoints
{
    public static IEndpointRouteBuilder MapRatesEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/rates", async (RatesService svc, string? date, string? currencies, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(date, out var parsed) ? parsed : null;
            var ccys = string.IsNullOrWhiteSpace(currencies)
                ? new[] { "USD", "RUB" }
                : currencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return Results.Ok(await svc.GetRatesAsync(d, ccys, ct));
        });
        return api;
    }
}
