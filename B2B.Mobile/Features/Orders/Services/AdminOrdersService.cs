using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Orders.Models;

namespace B2B.Mobile.Features.Orders.Services;

public sealed class AdminOrdersService
{
    /// <summary>API gövdesi: <c>OrderStatus</c> sayısal değer (sipariş durumu PATCH).</summary>
    public sealed record UpdateOrderStatusRequest(int Status);

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
        return _api.GetAsync<PagedResult<AdminOrderListItem>>(url, ct);
    }

    public Task<ApiResponse<AdminOrderDetail>> GetDetailAsync(Guid orderId, CancellationToken ct) =>
        _api.GetAsync<AdminOrderDetail>($"/api/v1/admin/orders/{orderId}", ct);

    public Task<ApiResponse<object>> UpdateStatusAsync(Guid orderId, int status, CancellationToken ct) =>
        _api.PatchAsync<UpdateOrderStatusRequest, object>(
            $"/api/v1/orders/{orderId}/status",
            new UpdateOrderStatusRequest(status),
            ct);
}
