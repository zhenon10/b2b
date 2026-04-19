namespace B2B.Api.Contracts;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    string TraceId
)
{
    public static ApiResponse<T> Ok(T data, string traceId) =>
        new(true, data, null, traceId);

    public static ApiResponse<T> Fail(ApiError error, string traceId) =>
        new(false, default, error, traceId);
}

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null
);

