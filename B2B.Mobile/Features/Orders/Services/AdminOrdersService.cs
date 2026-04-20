using B2B.Contracts;
using B2B.Domain.Enums;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Features.Orders.Services;

public sealed class AdminOrdersService
{
    private readonly ApiClient _api;

    public AdminOrdersService(ApiClient api) => _api = api;

    public Task<ApiResponse<PagedResult<AdminOrderListItem>>> GetListAsync(
        int page,
        int pageSize,
        int? status,
        CancellationToken ct)
    {
        var url = $"/api/v1/admin/orders?page={page}&pageSize={pageSize}";
        if (status.HasValue)
            url += $"&status={status.Value}";
        return ApiTransientRetry.ExecuteAsync(() => _api.GetAsync<PagedResult<AdminOrderListItem>>(url, ct), ct);
    }

    public Task<ApiResponse<AdminOrderDetail>> GetDetailAsync(Guid orderId, CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(
            () => _api.GetAsync<AdminOrderDetail>($"/api/v1/admin/orders/{orderId}", ct),
            ct);

    public Task<ApiResponse<object>> UpdateStatusAsync(Guid orderId, int status, CancellationToken ct) =>
        _api.PatchAsync<UpdateOrderStatusRequest, object>(
            $"/api/v1/orders/{orderId}/status",
            new UpdateOrderStatusRequest((OrderStatus)status),
            ct);
}
