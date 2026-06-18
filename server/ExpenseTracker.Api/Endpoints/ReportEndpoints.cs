using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Reports;

namespace ExpenseTracker.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/reports/summary", async (ICurrentUser cu, ReportService svc,
            DateOnly? from, DateOnly? to) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.SummaryAsync(user.Id, from, to);
            return EndpointResults.ToHttp(result);
        });

        return api;
    }

}
