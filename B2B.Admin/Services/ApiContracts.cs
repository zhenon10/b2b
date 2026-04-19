using System.Text.Json.Serialization;

namespace B2B.Admin.Services;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    string TraceId
);

public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null
);

/// <summary>API ile aynı JSON: <c>returned</c> alanı.</summary>
public sealed record PageMeta(
    int Page,
    int PageSize,
    [property: JsonPropertyName("returned")] int Returned,
    long Total);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, PageMeta Meta);

