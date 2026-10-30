namespace VirtualBar.Application.Common;

public class Result<T>
{
    public bool Success { get; private init; }

    public T? Data { get; private init; }

    public string? Error { get; private init; }

    public ErrorCode ErrorCode { get; private init; }

    /// <summary>
    /// Optional machine-readable reason for the failure (e.g. <see cref="AuthErrorReasons.EmailNotConfirmed"/>), sent
    /// to clients next to <see cref="Error"/> so they can show a translated message instead of the English text.
    /// </summary>
    public string? Reason { get; private init; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };

    public static Result<T> Fail(string error, string? reason = null) =>
        new() { Success = false, Error = error, ErrorCode = ErrorCode.Validation, Reason = reason };

    public static Result<T> NotFound(string error) => new() { Success = false, Error = error, ErrorCode = ErrorCode.NotFound };

    public static Result<T> Forbidden(string error) => new() { Success = false, Error = error, ErrorCode = ErrorCode.Forbidden };

    public static Result<T> Conflict(string error, string? reason = null) =>
        new() { Success = false, Error = error, ErrorCode = ErrorCode.Conflict, Reason = reason };
}
