namespace VirtualBar.Application.Common;

public class Result<T>
{
    public bool Success { get; private init; }

    public T? Data { get; private init; }

    public string? Error { get; private init; }

    public ErrorCode ErrorCode { get; private init; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };

    public static Result<T> Fail(string error) => new() { Success = false, Error = error, ErrorCode = ErrorCode.Validation };

    public static Result<T> NotFound(string error) => new() { Success = false, Error = error, ErrorCode = ErrorCode.NotFound };

    public static Result<T> Forbidden(string error) => new() { Success = false, Error = error, ErrorCode = ErrorCode.Forbidden };

    public static Result<T> Conflict(string error) => new() { Success = false, Error = error, ErrorCode = ErrorCode.Conflict };
}
