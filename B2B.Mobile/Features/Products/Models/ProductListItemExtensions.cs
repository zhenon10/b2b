using System.Linq;
using B2B.Contracts;

namespace B2B.Mobile.Features.Products.Models;

public static class ProductListItemExtensions
{
    public static ProductListItem FromProductDetail(ProductDetail d)
    {
        var primary = d.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .Select(i => i.Url)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

        return new ProductListItem(
            d.ProductId,
            d.SellerUserId,
            d.SellerDisplayName,
            d.CategoryId,
            d.CategoryName,
            primary,
            d.Sku,
            d.Name,
            d.CurrencyCode,
            d.DealerPrice,
            d.MsrpPrice,
            d.StockQuantity,
            d.IsActive);
    }
}
