namespace B2B.Contracts;

public sealed record PageRequest(
    int Page = 1,
    int PageSize = 20
)
{
    public int Skip => (Page - 1) * PageSize;

    public PageRequest Normalize(int maxPageSize = 100)
    {
        var page = Page < 1 ? 1 : Page;
        var pageSize = PageSize < 1 ? 1 : PageSize;
        if (pageSize > maxPageSize) pageSize = maxPageSize;
        return this with { Page = page, PageSize = pageSize };
    }
}

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
