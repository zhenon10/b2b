using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Auth.Models;

namespace B2B.Mobile.Features.Auth.Services;

public sealed class AdminUsersService
{
    private readonly ApiClient _api;

    public AdminUsersService(ApiClient api) => _api = api;

    public Task<ApiResponse<List<PendingDealerDto>>> GetPendingDealersAsync(CancellationToken ct) =>
        _api.GetAsync<List<PendingDealerDto>>("/api/v1/admin/users/pending-dealers", ct);

    public Task<ApiResponse<object>> ApproveDealerAsync(Guid userId, CancellationToken ct) =>
        _api.PostAsync<object>($"/api/v1/admin/users/{userId:D}/approve", ct);
}
