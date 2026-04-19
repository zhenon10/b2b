namespace B2B.Mobile.Features.Products.Models;

public sealed record ProductListItem(
    Guid ProductId,
    Guid SellerUserId,
    string SellerDisplayName,
    Guid? CategoryId,
    string? CategoryName,
    string? PrimaryImageUrl,
    string Sku,
    string Name,
    string CurrencyCode,
    decimal DealerPrice,
    decimal MsrpPrice,
    int StockQuantity,
    bool IsActive
);

