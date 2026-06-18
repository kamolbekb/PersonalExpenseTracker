using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Expenses;

namespace ExpenseTracker.Api.Features.Expenses;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/expenses");

        g.MapGet("", async (ICurrentUser cu, ExpenseService svc,
            DateOnly? from, DateOnly? to, int? categoryId) =>
        {
            var user = await cu.GetOrCreateAsync();
            var items = await svc.ListAsync(user.Id, from, to, categoryId);
            return Results.Ok(items);
        });

        g.MapPost("", async (ICurrentUser cu, ExpenseService svc, ExpenseInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.CreateAsync(user.Id, input);
            return ToHttp(result, result.Value is not null ? $"/api/expenses/{result.Value.Id}" : null);
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, ExpenseService svc, int id, ExpenseInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.UpdateAsync(user.Id, id, input);
            return ToHttp(result);
        });

        g.MapDelete("/{id:int}", async (ICurrentUser cu, ExpenseService svc, int id) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.DeleteAsync(user.Id, id);
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
