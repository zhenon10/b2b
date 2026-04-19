namespace B2B.Application.Products.CreateProduct;

public sealed record CreateProductRequest(
    Guid SellerUserId,
    string Sku,
    string Name,
    string? Description,
    string CurrencyCode,
    decimal Price,
    int StockQuantity
);

