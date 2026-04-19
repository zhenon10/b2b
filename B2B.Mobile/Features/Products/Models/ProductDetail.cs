namespace B2B.Mobile.Features.Products.Models;

public sealed record ProductImage(string Url, int SortOrder, bool IsPrimary);
public sealed record ProductSpec(string Key, string Value, int SortOrder);

public sealed record ProductDetail(
    Guid ProductId,
    Guid SellerUserId,
    string SellerDisplayName,
    Guid? CategoryId,
    string? CategoryName,
    IReadOnlyList<ProductImage> Images,
    IReadOnlyList<ProductSpec> Specs,
    string Sku,
    string Name,
    string? Description,
    string CurrencyCode,
    decimal DealerPrice,
    decimal MsrpPrice,
    int StockQuantity,
    bool IsActive
);

