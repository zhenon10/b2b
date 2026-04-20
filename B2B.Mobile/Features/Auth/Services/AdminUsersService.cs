using B2B.Contracts;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Features.Auth.Services;

public sealed class AdminUsersService
{
    private readonly ApiClient _api;

    public AdminUsersService(ApiClient api) => _api = api;

    public Task<ApiResponse<List<PendingDealerDto>>> GetPendingDealersAsync(CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(
            () => _api.GetAsync<List<PendingDealerDto>>("/api/v1/admin/users/pending-dealers", ct),
            ct);

    public Task<ApiResponse<object>> ApproveDealerAsync(Guid userId, string idempotencyKey, CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(
            () => _api.PostAsync<object>(
                $"/api/v1/admin/users/{userId:D}/approve",
                new Dictionary<string, string> { ["Idempotency-Key"] = idempotencyKey },
                ct),
            ct);
}
