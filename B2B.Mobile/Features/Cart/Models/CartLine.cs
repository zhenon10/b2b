namespace B2B.Mobile.Features.Cart.Models;

public sealed record CartLine(
    Guid ProductId,
    Guid SellerUserId,
    string SellerDisplayName,
    string Name,
    string Sku,
    string CurrencyCode,
    decimal UnitPrice,
    int Quantity
)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

