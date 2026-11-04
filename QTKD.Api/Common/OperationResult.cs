namespace QTKD.Api.Common;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyList<string>? Details = null);

public sealed record OperationResult<T>(T? Value, ApiErrorResponse? Error)
{
    public bool Succeeded => Error is null;

    public static OperationResult<T> Success(T value) => new(value, null);

    public static OperationResult<T> Failure(
        string code,
        string message,
        IReadOnlyList<string>? details = null) =>
        new(default, new ApiErrorResponse(code, message, details));
}
