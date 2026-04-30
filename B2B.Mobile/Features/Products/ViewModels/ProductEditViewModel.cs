using System.Collections.ObjectModel;
using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Core.Connectivity;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using System.Globalization;

namespace B2B.Mobile.Features.Products.ViewModels;

public partial class ProductEditViewModel : ObservableObject
{
    private readonly ProductsService _products;
    private readonly ImageUploadService _uploads;
    private readonly CategoriesService _categories;
    private readonly IAuthSession _authSession;
    private readonly ConnectivityService _connectivity;

    public ProductEditViewModel(
        ProductsService products,
        ImageUploadService uploads,
        CategoriesService categories,
        IAuthSession authSession,
        ConnectivityService connectivity)
    {
        _products = products;
        _uploads = uploads;
        _categories = categories;
        _authSession = authSession;
        _connectivity = connectivity;
    }

    [ObservableProperty] private string? productId;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;

    [ObservableProperty] private string sku = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string? description;
    [ObservableProperty] private string currencyCode = "USD";
    [ObservableProperty] private decimal dealerPrice;
    [ObservableProperty] private decimal msrpPrice;
    [ObservableProperty] private int stockQuantity;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private bool canDeleteProduct;

    // Numeric inputs are user-entered strings; we parse/validate before save to handle locale separators.
    [ObservableProperty] private string dealerPriceText = "0";
    [ObservableProperty] private string msrpPriceText = "0";
    [ObservableProperty] private string stockQuantityText = "0";

    public ObservableCollection<CategoryListItem> CategoryOptions { get; } = new();
    [ObservableProperty] private CategoryListItem? selectedCategory;

    public ObservableCollection<ImageRow> Images { get; } = new();
    public ObservableCollection<SpecRow> Specs { get; } = new();

    /// <summary>Sayfa navigasyonundan çağrılır (yeni üründe ProductId null kalabildiği için QueryProperty ile güvenilmez).</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        Error = null;
        ApiTraceId = null;
        CanDeleteProduct = false;
        Images.Clear();
        Specs.Clear();
        CategoryOptions.Clear();
        SelectedCategory = null;

        IsBusy = true;
        try
        {
            var token = await _authSession.GetAccessTokenAsync();
            var isAdmin = JwtRoleReader.IsAdmin(token);
            if (!isAdmin)
            {
                Error = "Ürün oluşturma ve düzenleme yalnızca yöneticiler içindir.";
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var page = Shell.Current?.CurrentPage;
                    if (page is not null)
                    {
                        await page.DisplayAlertAsync(
                            "Erişim yok",
                            "Bu sayfa yalnızca yöneticiler içindir.",
                            "Tamam");
                    }

                    if (Shell.Current is not null)
                        await Shell.Current.GoToAsync("//main/products", animate: false);
                });
                return;
            }

            var catResp = await _categories.GetCategoriesAsync(includeInactive: isAdmin, CancellationToken.None);
            if (catResp.Success && catResp.Data is not null)
            {
                foreach (var c in catResp.Data.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
                    CategoryOptions.Add(c);
            }

            if (!Guid.TryParse(ProductId, out var id))
                return; // new product

            var resp = await _products.GetProductAsync(id, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Ürün yüklenemedi.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            CanDeleteProduct = isAdmin;

            var p = resp.Data;
            Sku = p.Sku;
            Name = p.Name;
            Description = p.Description;
            CurrencyCode = p.CurrencyCode;
            DealerPrice = p.DealerPrice;
            MsrpPrice = p.MsrpPrice;
            StockQuantity = p.StockQuantity;
            IsActive = p.IsActive;
            DealerPriceText = DealerPrice.ToString("0.####", CultureInfo.CurrentCulture);
            MsrpPriceText = MsrpPrice.ToString("0.####", CultureInfo.CurrentCulture);
            StockQuantityText = StockQuantity.ToString(CultureInfo.CurrentCulture);
            SelectedCategory = p.CategoryId is { } cid
                ? CategoryOptions.FirstOrDefault(x => x.CategoryId == cid)
                : null;

            foreach (var img in p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder))
                Images.Add(new ImageRow { Url = img.Url, IsPrimary = img.IsPrimary });
            NormalizeImageSort();

            foreach (var spec in p.Specs.OrderBy(s => s.SortOrder))
                Specs.Add(new SpecRow { Key = spec.Key, Value = spec.Value });
            NormalizeSpecSort();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ScanSkuAsync() => Shell.Current.GoToAsync("productScan?returnTo=sku");

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (IsBusy || !CanDeleteProduct) return;
        if (!Guid.TryParse(ProductId, out var id)) return;
        if (_connectivity.IsOffline)
        {
            Error = "İnternet bağlantısı yok. Ürün pasife alınamadı.";
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Ürünü pasife al",
            "Bu ürün pasife alınacak ve katalogda görünmeyecek. Emin misiniz?",
            "Pasife al",
            "İptal");
        if (!confirm) return;

        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        try
        {
            var resp = await _products.DeactivateProductAsync(id, CancellationToken.None);
            if (!resp.Success)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Ürün kaldırılamadı.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["refreshProducts"] = true });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddImage() { Images.Add(new ImageRow()); NormalizeImageSort(); }

    [RelayCommand]
    private void RemoveImage(ImageRow? row)
    {
        if (row is null) return;
        Images.Remove(row);
        NormalizeImageSort();
    }

    [RelayCommand]
    private async Task AddPhotoFromCameraAsync()
    {
        if (IsBusy) return;
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                Error = "Bu cihazda kamera ile fotoğraf çekimi desteklenmiyor.";
                return;
            }

            var file = await MediaPicker.Default.CapturePhotoAsync();
            if (file is null) return;

            await UploadAndAddAsync(file);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddPhotoFromGalleryAsync()
    {
        if (IsBusy) return;
        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync();
            if (file is null) return;

            await UploadAndAddAsync(file);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private async Task UploadAndAddAsync(FileResult file)
    {
        if (_connectivity.IsOffline)
        {
            Error = "İnternet bağlantısı yok. Görsel yüklenemedi.";
            return;
        }

        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        try
        {
            var resp = await _uploads.UploadProductImageAsync(file, CancellationToken.None);
            if (!resp.Success || string.IsNullOrWhiteSpace(resp.Data))
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Yükleme başarısız.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            // Add new image as primary if first.
            Images.Add(new ImageRow
            {
                Url = resp.Data,
                IsPrimary = Images.Count == 0
            });
            NormalizeImageSort();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddSpec() { Specs.Add(new SpecRow()); NormalizeSpecSort(); }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;

        try
        {
            if (_connectivity.IsOffline)
            {
                Error = "İnternet bağlantısı yok. Kaydedilemedi.";
                return;
            }

            // Validate numeric inputs up-front (flexible parsing for comma/dot).
            if (!TryParseDecimalFlexible(DealerPriceText, out var dealerParsed) || dealerParsed < 0)
            {
                Error = "Bayi fiyatı geçersiz. Örn: 12,50";
                return;
            }
            if (!TryParseDecimalFlexible(MsrpPriceText, out var msrpParsed) || msrpParsed < 0)
            {
                Error = "Tavsiye satış (MSRP) geçersiz. Örn: 12,50";
                return;
            }
            if (!TryParseIntFlexible(StockQuantityText, out var stockParsed) || stockParsed < 0)
            {
                Error = "Stok adedi geçersiz. Örn: 10";
                return;
            }

            DealerPrice = dealerParsed;
            MsrpPrice = msrpParsed;
            StockQuantity = stockParsed;

            NormalizeImageSort();
            NormalizeSpecSort();

            var images = Images
                .Where(i => !string.IsNullOrWhiteSpace(i.Url))
                .Select((i, idx) => new ProductImageInput((i.Url ?? "").Trim(), idx, i.IsPrimary))
                .ToList();

            // Ensure single primary
            if (images.Count > 0 && images.Count(i => i.IsPrimary) != 1)
            {
                for (var i = 0; i < images.Count; i++)
                    images[i] = images[i] with { IsPrimary = i == 0 };
            }

            var specs = Specs
                .Where(s => !string.IsNullOrWhiteSpace(s.Key) && !string.IsNullOrWhiteSpace(s.Value))
                .Select((s, idx) => new ProductSpecInput((s.Key ?? "").Trim(), (s.Value ?? "").Trim(), idx))
                .ToList();

            if (!Guid.TryParse(ProductId, out var id))
            {
                var resp = await _products.CreateProductAsync(new CreateProductRequest(
                    SellerUserId: null,
                    CategoryId: SelectedCategory?.CategoryId,
                    Sku: Sku.Trim(),
                    Name: Name.Trim(),
                    Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    CurrencyCode: (CurrencyCode ?? "USD").Trim().ToUpperInvariant(),
                    DealerPrice: DealerPrice,
                    MsrpPrice: MsrpPrice,
                    StockQuantity: StockQuantity,
                    Images: images,
                    Specs: specs,
                    IsActive: IsActive
                ), CancellationToken.None);

                if (!resp.Success)
                {
                    Error = UserFacingApiMessage.Message(resp.Error, "Oluşturma başarısız.");
                    ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                    return;
                }
            }
            else
            {
                var resp = await _products.UpdateProductAsync(id, new UpdateProductRequest(
                    Sku: Sku.Trim(),
                    Name: Name.Trim(),
                    Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    CategoryId: SelectedCategory?.CategoryId,
                    CurrencyCode: (CurrencyCode ?? "USD").Trim().ToUpperInvariant(),
                    DealerPrice: DealerPrice,
                    MsrpPrice: MsrpPrice,
                    StockQuantity: StockQuantity,
                    Images: images,
                    Specs: specs,
                    IsActive: IsActive
                ), CancellationToken.None);

                if (!resp.Success)
                {
                    Error = UserFacingApiMessage.Message(resp.Error, "Güncelleme başarısız.");
                    ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                    return;
                }
            }

            await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["refreshProducts"] = true });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NormalizeImageSort()
    {
        // ensure primary exists if any
        if (Images.Count > 0 && Images.All(i => !i.IsPrimary))
            Images[0].IsPrimary = true;
    }

    private void NormalizeSpecSort()
    {
        // no-op for now
    }

    private static bool TryParseDecimalFlexible(string? raw, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = raw.Trim();

        // First: current culture.
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
            return true;
        // Then: invariant.
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        // Finally: try swapping separators (common in TR vs en-US).
        s = s.Replace(" ", "");
        var swapped = s.Contains(',') && !s.Contains('.')
            ? s.Replace(',', '.')
            : s.Contains('.') && !s.Contains(',')
                ? s.Replace('.', ',')
                : s;

        return decimal.TryParse(swapped, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
               || decimal.TryParse(swapped, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseIntFlexible(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = raw.Trim().Replace(" ", "");
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
               || int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public sealed partial class ImageRow : ObservableObject
    {
        [ObservableProperty] private string? url;
        [ObservableProperty] private bool isPrimary;
    }

    public sealed partial class SpecRow : ObservableObject
    {
        [ObservableProperty] private string? key;
        [ObservableProperty] private string? value;
    }
}

