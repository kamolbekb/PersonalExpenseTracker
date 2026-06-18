using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Settings;

namespace ExpenseTracker.Api.Endpoints;

public static class SettingEndpoints
{
    public static IEndpointRouteBuilder MapSettingEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/settings");

        g.MapGet("", async (ICurrentUser cu, SettingService svc) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.GetAsync(user.Id);
            return EndpointResults.ToHttp(result);
        });

        g.MapPut("", async (ICurrentUser cu, SettingService svc, SettingDto input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.UpdateAsync(user.Id, input);
            return EndpointResults.ToHttp(result);
        });

        return api;
    }

}
