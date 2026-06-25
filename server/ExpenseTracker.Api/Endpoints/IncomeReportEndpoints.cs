using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Reports;

namespace ExpenseTracker.Api.Endpoints;

public static class IncomeReportEndpoints
{
    public static IEndpointRouteBuilder MapIncomeReportEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/reports/income-summary", async (ICurrentUser cu, IncomeReportService svc,
            DateOnly? from, DateOnly? to) =>
        {
            var user = await cu.GetOrCreateAsync();
            return EndpointResults.ToHttp(await svc.SummaryAsync(user.Id, from, to));
        });
        return api;
    }
}
