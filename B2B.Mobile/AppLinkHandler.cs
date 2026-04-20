using B2B.Contracts;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;

namespace B2B.Mobile;

/// <summary><c>b2b://product/{guid}</c> derin bağlantıları.</summary>
public static class AppLinkHandler
{
    public static async Task TryNavigateFromUriAsync(Uri uri, IServiceProvider services, CancellationToken ct = default)
    {
        if (!string.Equals(uri.Scheme, "b2b", StringComparison.OrdinalIgnoreCase))
            return;
        if (!string.Equals(uri.Host, "product", StringComparison.OrdinalIgnoreCase))
            return;

        var tail = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(tail) && uri.Segments.Length > 0)
            tail = uri.Segments[^1].Trim('/');

        if (!Guid.TryParse(tail, out var productId))
            return;

        var products = services.GetRequiredService<ProductsService>();
        var resp = await products.GetProductAsync(productId, ct).ConfigureAwait(false);
        if (!resp.Success || resp.Data is null)
            return;

        var p = resp.Data;
        var item = ProductListItemExtensions.FromProductDetail(p);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
                return;
            await Shell.Current.GoToAsync("//main/products", animate: false);
            await Shell.Current.GoToAsync("productDetail", new Dictionary<string, object> { ["product"] = item });
        });
    }
}
