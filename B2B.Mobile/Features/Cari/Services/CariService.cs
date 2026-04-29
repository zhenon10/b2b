using B2B.Contracts;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Features.Cari.Services;

public sealed class CariService
{
    private const int DefaultPageSize = 20;
    private readonly ApiClient _api;

    public CariService(ApiClient api) => _api = api;

    public Task<ApiResponse<IReadOnlyList<CustomerAccountSummary>>> ListAsync(CancellationToken ct = default) =>
        ApiTransientRetry.ExecuteAsync(() => _api.GetAsync<IReadOnlyList<CustomerAccountSummary>>("/api/v1/cari", ct), ct);

    public Task<ApiResponse<PagedResult<CustomerAccountEntryDto>>> EntriesAsync(
        Guid sellerUserId,
        string currencyCode,
        int page,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        var cc = (currencyCode ?? "").Trim().ToUpperInvariant();
        var url = $"/api/v1/cari/{sellerUserId:D}/{cc}?page={page}&pageSize={pageSize}";
        return ApiTransientRetry.ExecuteAsync(() => _api.GetAsync<PagedResult<CustomerAccountEntryDto>>(url, ct), ct);
    }
}

