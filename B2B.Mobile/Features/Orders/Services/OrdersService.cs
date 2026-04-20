using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Cart.Models;

namespace B2B.Mobile.Features.Orders.Services;

public sealed class OrdersService
{
    private const int DefaultPageSize = 20;
    private readonly ApiClient _api;

    public OrdersService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResponse<SubmitOrderResponse>> SubmitOrderAsync(
        Guid sellerUserId,
        string currencyCode,
        IReadOnlyList<CartLine> lines,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var req = new SubmitOrderRequest(
            sellerUserId,
            currencyCode,
            lines.Select(x => new SubmitOrderItem(x.ProductId, x.Quantity)).ToList()
        );

        Dictionary<string, string>? headers = null;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            headers = new Dictionary<string, string>
            {
                ["Idempotency-Key"] = idempotencyKey.Trim()
            };
        }

        return ApiTransientRetry.ExecuteAsync(
            () => _api.PostAsync<SubmitOrderRequest, SubmitOrderResponse>("/api/v1/orders", req, headers, ct),
            ct);
    }

    public Task<ApiResponse<PagedResult<DealerOrderListItem>>> GetMyOrdersAsync(
        int page,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/orders?page={page}&pageSize={pageSize}";
        return ApiTransientRetry.ExecuteAsync(() => _api.GetAsync<PagedResult<DealerOrderListItem>>(url, ct), ct);
    }

    public Task<ApiResponse<DealerOrderDetail>> GetMyOrderDetailAsync(Guid orderId, CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(
            () => _api.GetAsync<DealerOrderDetail>($"/api/v1/orders/{orderId}", ct),
            ct);

    public Task<ApiResponse<object>> CancelMyOrderAsync(Guid orderId, CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(
            () => _api.PostAsync<object>($"/api/v1/orders/{orderId}/cancel", ct),
            ct);
}
