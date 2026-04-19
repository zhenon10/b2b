namespace B2B.Mobile.Features.Orders.Models;

public sealed record AdminOrderListItem(
    Guid OrderId,
    long OrderNumber,
    Guid BuyerUserId,
    string BuyerEmail,
    string? BuyerDisplayName,
    Guid SellerUserId,
    string? SellerDisplayName,
    string CurrencyCode,
    decimal GrandTotal,
    int Status,
    DateTime CreatedAtUtc);

public sealed record AdminOrderLine(
    int LineNumber,
    string ProductSku,
    string ProductName,
    decimal UnitPrice,
    int Quantity);

public sealed record AdminOrderDetail(
    Guid OrderId,
    long OrderNumber,
    Guid BuyerUserId,
    string BuyerEmail,
    string? BuyerDisplayName,
    Guid SellerUserId,
    string? SellerDisplayName,
    string CurrencyCode,
    decimal Subtotal,
    decimal GrandTotal,
    int Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<AdminOrderLine> Items);
