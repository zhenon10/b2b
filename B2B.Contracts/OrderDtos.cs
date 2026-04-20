using B2B.Domain.Enums;

namespace B2B.Contracts;

public sealed record SubmitOrderItem(Guid ProductId, int Quantity);
public sealed record SubmitOrderRequest(Guid SellerUserId, string CurrencyCode, IReadOnlyList<SubmitOrderItem> Items);
public sealed record SubmitOrderResponse(Guid OrderId, long OrderNumber, decimal GrandTotal);

public sealed record DealerOrderListItem(
    Guid OrderId,
    long OrderNumber,
    Guid SellerUserId,
    string? SellerDisplayName,
    string CurrencyCode,
    decimal GrandTotal,
    OrderStatus Status,
    DateTime CreatedAtUtc);

public sealed record DealerOrderLine(
    int LineNumber,
    string ProductSku,
    string ProductName,
    decimal UnitPrice,
    int Quantity);

public sealed record DealerOrderDetail(
    Guid OrderId,
    long OrderNumber,
    Guid SellerUserId,
    string? SellerDisplayName,
    string CurrencyCode,
    decimal Subtotal,
    decimal GrandTotal,
    OrderStatus Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<DealerOrderLine> Items);

public sealed record UpdateOrderStatusRequest(OrderStatus Status);
