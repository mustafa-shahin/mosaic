namespace Mosaic.Contracts;

public sealed record ApiResponse
{
    public ApiError? Error { get; init; }
    public string? TraceId { get; init; }

    public bool Success => Error is null;

    public static ApiResponse Ok() => new();

    public static ApiResponse Fail(ApiError error) => new() { Error = error };

    public static ApiResponse<T> Ok<T>(T data) => new() { Data = data };

    public static ApiResponse<T> Fail<T>(ApiError error) => new() { Error = error };
}

public sealed record ApiResponse<T>
{
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public string? TraceId { get; init; }

    public bool Success => Error is null;
}
