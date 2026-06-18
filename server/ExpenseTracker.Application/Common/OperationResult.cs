namespace ExpenseTracker.Application.Common;

public enum ResultStatus { Ok, Created, NotFound, BadRequest, NoContent }

public sealed record OperationResult<T>(ResultStatus Status, T? Value = default, string? Error = null)
{
    public static OperationResult<T> Ok(T value) => new(ResultStatus.Ok, value);
    public static OperationResult<T> Created(T value) => new(ResultStatus.Created, value);
    public static OperationResult<T> NotFound() => new(ResultStatus.NotFound);
    public static OperationResult<T> Bad(string error) => new(ResultStatus.BadRequest, Error: error);
    public static OperationResult<T> NoContent() => new(ResultStatus.NoContent);
}
