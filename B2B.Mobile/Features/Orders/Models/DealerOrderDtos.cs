namespace B2B.Mobile.Features.Orders.Models;

public sealed record DealerOrderListItem(
    Guid OrderId,
    long OrderNumber,
    Guid SellerUserId,
    string? SellerDisplayName,
    string CurrencyCode,
    decimal GrandTotal,
    int Status,
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
    int Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<DealerOrderLine> Items);
