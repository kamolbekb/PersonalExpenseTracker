using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Settings;

namespace ExpenseTracker.Api.Features.Settings;

public static class SettingEndpoints
{
    public static IEndpointRouteBuilder MapSettingEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/settings");

        g.MapGet("", async (ICurrentUser cu, SettingService svc) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.GetAsync(user.Id);
            return ToHttp(result);
        });

        g.MapPut("", async (ICurrentUser cu, SettingService svc, SettingDto input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.UpdateAsync(user.Id, input);
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
