using B2B.Mobile.Features.Cart.Models;
using B2B.Mobile.Features.Cart.Services;
using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace B2B.Mobile.Features.Products.ViewModels;

[QueryProperty(nameof(Product), "product")]
[QueryProperty(nameof(ProductIdQuery), "productId")]
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

    // Summary passed from list navigation or built from productId query.
    [ObservableProperty] private ProductListItem? product;

    [ObservableProperty] private string? productIdQuery;

    // Full detail loaded from API (images/specs/description).
    [ObservableProperty] private ProductDetail? detail;

    [ObservableProperty] private int quantity = 1;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    [ObservableProperty] private bool canManageProducts;

    /// <summary>Tam ekran önizleme; null ise kapalı.</summary>
    [ObservableProperty] private string? imagePreviewUrl;

    partial void OnProductIdQueryChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (Product is not null)
            return;
        if (!Guid.TryParse(value.Trim(), out var id))
            return;

        Product = new ProductListItem(
            id,
            Guid.Empty,
            "…",
            null,
            null,
            null,
            "",
            "Yükleniyor…",
            "TRY",
            0,
            0,
            0,
            true);
    }

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
                    ? Array.Empty<ProductImageDto>()
                    : new[] { new ProductImageDto(value.PrimaryImageUrl!, SortOrder: 0, IsPrimary: true) },
                Specs: Array.Empty<ProductSpecDto>(),
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
        ApiTraceId = null;
        try
        {
            var resp = await _products.GetProductAsync(summary.ProductId, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = FormatProductDetailError(resp.Error);
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                Detail = null;
                return;
            }

            Detail = resp.Data;
            ApiTraceId = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            ApiTraceId = null;
            Detail = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatProductDetailError(ApiError? err)
    {
        if (err is null) return "Ürün detayı yüklenemedi.";
        return err.Code switch
        {
            "unauthorized" => "Oturum süresi dolmuş olabilir. Yeniden giriş yapın.",
            "forbidden" => "Bu ürüne erişim yetkiniz yok.",
            _ => err.Message
        };
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
    private void OpenImagePreview(ProductImageDto? image)
    {
        if (image is null || string.IsNullOrWhiteSpace(image.Url)) return;
        ImagePreviewUrl = image.Url;
    }

    [RelayCommand]
    private void CloseImagePreview() => ImagePreviewUrl = null;
}
