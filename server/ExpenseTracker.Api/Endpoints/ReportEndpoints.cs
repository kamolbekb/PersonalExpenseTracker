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
            return ToHttp(result);
        });

        return api;
    }

    static IResult ToHttp<T>(OperationResult<T> r, string? createdAtPath = null) => r.Status switch
    {
        ResultStatus.Ok => Results.Ok(r.Value),
        ResultStatus.Created => Results.Created(createdAtPath ?? "", r.Value),
        ResultStatus.NoContent => Results.NoContent(),
        ResultStatus.NotFound => Results.NotFound(),
        ResultStatus.BadRequest => Results.BadRequest(r.Error),
        _ => Results.StatusCode(500),
    };
}
