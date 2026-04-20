namespace B2B.Contracts;

public sealed record CreateProductRequest(
    Guid? SellerUserId,
    Guid? CategoryId,
    string Sku,
    string Name,
    string? Description,
    string CurrencyCode,
    decimal DealerPrice,
    decimal MsrpPrice,
    int StockQuantity,
    IReadOnlyList<ProductImageInput>? Images,
    IReadOnlyList<ProductSpecInput>? Specs,
    bool IsActive = true
);

public sealed record UpdateProductRequest(
    string Sku,
    string Name,
    string? Description,
    Guid? CategoryId,
    string CurrencyCode,
    decimal DealerPrice,
    decimal MsrpPrice,
    int StockQuantity,
    IReadOnlyList<ProductImageInput>? Images,
    IReadOnlyList<ProductSpecInput>? Specs,
    bool IsActive
);

public sealed record ProductImageInput(string Url, int SortOrder, bool IsPrimary);
public sealed record ProductSpecInput(string Key, string Value, int SortOrder);

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

public sealed record ProductImageDto(string Url, int SortOrder, bool IsPrimary);
public sealed record ProductSpecDto(string Key, string Value, int SortOrder);

public sealed record ProductDetail(
    Guid ProductId,
    Guid SellerUserId,
    string SellerDisplayName,
    Guid? CategoryId,
    string? CategoryName,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductSpecDto> Specs,
    string Sku,
    string Name,
    string? Description,
    string CurrencyCode,
    decimal DealerPrice,
    decimal MsrpPrice,
    int StockQuantity,
    bool IsActive
);

public sealed record UpdateStockRequest(int StockQuantity);
public sealed record UpdateActiveRequest(bool IsActive);
public sealed record UploadImageResponse(string Url);
