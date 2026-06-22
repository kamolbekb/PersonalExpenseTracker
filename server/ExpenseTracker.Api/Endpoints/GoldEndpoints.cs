using ExpenseTracker.Application.Gold;

namespace ExpenseTracker.Api.Endpoints;

public static class GoldEndpoints
{
    public static IEndpointRouteBuilder MapGoldEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/gold", async (GoldService svc, string? date, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(date, out var parsed) ? parsed : null;
            return Results.Ok(await svc.GetGoldAsync(d, ct));
        });
        return api;
    }
}
