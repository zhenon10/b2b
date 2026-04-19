using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Cart.Models;
using B2B.Mobile.Features.Orders.Models;

namespace B2B.Mobile.Features.Orders.Services;

public sealed class OrdersService
{
    private const int DefaultPageSize = 20;
    private readonly ApiClient _api;

    public OrdersService(ApiClient api)
    {
        _api = api;
    }

    public sealed record SubmitOrderRequest(
        Guid SellerUserId,
        string CurrencyCode,
        IReadOnlyList<SubmitOrderItem> Items
    );

    public sealed record SubmitOrderItem(
        Guid ProductId,
        int Quantity
    );

    public sealed record SubmitOrderResponse(
        Guid OrderId,
        long OrderNumber,
        decimal GrandTotal
    );

    // Backend endpoint expected:
    // - POST /api/v1/orders
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

        // Mobile retry safety: same idempotency key returns same order.
        Dictionary<string, string>? headers = null;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            headers = new Dictionary<string, string>
            {
                ["Idempotency-Key"] = idempotencyKey.Trim()
            };
        }

        return OrderTransientRetry.ExecuteAsync(
            () => _api.PostAsync<SubmitOrderRequest, SubmitOrderResponse>("/api/v1/orders", req, headers, ct),
            ct);
    }

    public Task<ApiResponse<PagedResult<DealerOrderListItem>>> GetMyOrdersAsync(
        int page,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/orders?page={page}&pageSize={pageSize}";
        return OrderTransientRetry.ExecuteAsync(() => _api.GetAsync<PagedResult<DealerOrderListItem>>(url, ct), ct);
    }

    public Task<ApiResponse<DealerOrderDetail>> GetMyOrderDetailAsync(Guid orderId, CancellationToken ct) =>
        OrderTransientRetry.ExecuteAsync(
            () => _api.GetAsync<DealerOrderDetail>($"/api/v1/orders/{orderId}", ct),
            ct);

    /// <summary>Bayi sipariş iptali (sunucu uygun aşamalarda).</summary>
    public Task<ApiResponse<object>> CancelMyOrderAsync(Guid orderId, CancellationToken ct) =>
        OrderTransientRetry.ExecuteAsync(
            () => _api.PostAsync<object>($"/api/v1/orders/{orderId}/cancel", ct),
            ct);
}

