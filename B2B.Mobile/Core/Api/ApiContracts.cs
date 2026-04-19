namespace B2B.Mobile.Core.Api;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    string TraceId
);

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details
);

public sealed record PageMeta(
    int Page,
    int PageSize,
    int Returned,
    long Total
);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    PageMeta Meta
);

