namespace VirtualBar.Application.Common;

public class Result<T>
{
    public bool Success { get; private init; }
    public T? Data { get; private init; }
    public string? Error { get; private init; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };
    public static Result<T> Fail(string error) => new() { Success = false, Error = error };
}
