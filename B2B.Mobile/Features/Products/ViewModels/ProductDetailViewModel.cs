using B2B.Mobile.Features.Cart.Models;
using B2B.Mobile.Features.Cart.Services;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Products.ViewModels;

[QueryProperty(nameof(Product), "product")]
public partial class ProductDetailViewModel : ObservableObject
{
    private readonly CartService _cart;
    private readonly ProductsService _products;
    private readonly IAuthSession _authSession;

    public ProductDetailViewModel(CartService cart, ProductsService products, IAuthSession authSession)
    {
        _cart = cart;
        _products = products;
        _authSession = authSession;
        _ = LoadRolesAsync();
    }

    // Summary passed from list navigation.
    [ObservableProperty] private ProductListItem? product;

    // Full detail loaded from API (images/specs/description).
    [ObservableProperty] private ProductDetail? detail;

    [ObservableProperty] private int quantity = 1;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private bool canManageProducts;

    /// <summary>Tam ekran önizleme; null ise kapalı.</summary>
    [ObservableProperty] private string? imagePreviewUrl;

    private async Task LoadRolesAsync()
    {
        try
        {
            var token = await _authSession.GetAccessTokenAsync();
            CanManageProducts = JwtRoleReader.IsAdmin(token);
        }
        catch
        {
            CanManageProducts = false;
        }
    }

    partial void OnProductChanged(ProductListItem? value)
    {
        Quantity = 1;
        ImagePreviewUrl = null;

        if (value is not null)
        {
            // Populate immediately to avoid null bindings while fetching full detail.
            Detail = new ProductDetail(
                ProductId: value.ProductId,
                SellerUserId: value.SellerUserId,
                SellerDisplayName: value.SellerDisplayName,
                CategoryId: value.CategoryId,
                CategoryName: value.CategoryName,
                Images: string.IsNullOrWhiteSpace(value.PrimaryImageUrl)
                    ? Array.Empty<ProductImage>()
                    : new[] { new ProductImage(value.PrimaryImageUrl!, SortOrder: 0, IsPrimary: true) },
                Specs: Array.Empty<ProductSpec>(),
                Sku: value.Sku,
                Name: value.Name,
                Description: null,
                CurrencyCode: value.CurrencyCode,
                DealerPrice: value.DealerPrice,
                MsrpPrice: value.MsrpPrice,
                StockQuantity: value.StockQuantity,
                IsActive: value.IsActive
            );
        }

        // Fire-and-forget: view model owns IsBusy/Error.
        _ = LoadDetailAsync(value);
    }

    private async Task LoadDetailAsync(ProductListItem? summary)
    {
        if (summary is null) return;

        IsBusy = true;
        Error = null;
        try
        {
            var resp = await _products.GetProductAsync(summary.ProductId, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = resp.Error?.Message ?? "Failed to load product.";
                Detail = null;
                return;
            }

            Detail = resp.Data;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Detail = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void IncrementCartQuantity()
    {
        var p = Detail;
        var max = p is { StockQuantity: > 0 } ? p.StockQuantity : int.MaxValue;
        if (Quantity < max)
            Quantity++;
    }

    [RelayCommand]
    private void DecrementCartQuantity()
    {
        if (Quantity > 1)
            Quantity--;
    }

    [RelayCommand]
    private async Task AddToCartAsync()
    {
        var p = Detail;
        if (p is null) return;
        if (Quantity < 1) Quantity = 1;
        if (p.StockQuantity > 0 && Quantity > p.StockQuantity)
            Quantity = p.StockQuantity;

        _cart.AddOrIncrement(new CartLine(
            p.ProductId,
            p.SellerUserId,
            p.SellerDisplayName,
            p.Name,
            p.Sku,
            p.CurrencyCode,
            p.DealerPrice,
            Quantity
        ));

        await Shell.Current.GoToAsync("//main/cart");
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        var p = Detail;
        if (p is null) return;

        var token = await _authSession.GetAccessTokenAsync();
        if (!CanManageProducts || !JwtRoleReader.IsAdmin(token))
        {
            var page = Shell.Current?.CurrentPage;
            if (page is not null)
            {
                await page.DisplayAlertAsync(
                    "Erişim yok",
                    "Ürün düzenleme yalnızca yöneticiler içindir.",
                    "Tamam");
            }

            return;
        }

        await Shell.Current.GoToAsync("productEdit", new Dictionary<string, object>
        {
            ["productId"] = p.ProductId.ToString()
        });
    }

    [RelayCommand]
    private void OpenImagePreview(ProductImage? image)
    {
        if (image is null || string.IsNullOrWhiteSpace(image.Url)) return;
        ImagePreviewUrl = image.Url;
    }

    [RelayCommand]
    private void CloseImagePreview() => ImagePreviewUrl = null;
}

