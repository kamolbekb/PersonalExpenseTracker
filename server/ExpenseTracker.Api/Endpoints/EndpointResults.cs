using ExpenseTracker.Application.Common;

namespace ExpenseTracker.Api.Endpoints;

internal static class EndpointResults
{
    public static IResult ToHttp<T>(OperationResult<T> result, string? createdAtPath = null) =>
        result.Status switch
        {
            ResultStatus.Ok => Results.Ok(result.Value),
            ResultStatus.Created => Results.Created(createdAtPath ?? "", result.Value),
            ResultStatus.NoContent => Results.NoContent(),
            ResultStatus.NotFound => Results.NotFound(),
            ResultStatus.BadRequest => Results.BadRequest(result.Error),
            _ => Results.StatusCode(500),
        };
}
