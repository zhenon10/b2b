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

    // Used by view to scroll/focus first invalid input after save attempt.
    [ObservableProperty] private string? focusField;

    private bool _suppressCurrencyNormalize;

    // Field-level validation (inline under inputs)
    [ObservableProperty] private string? skuError;
    [ObservableProperty] private string? nameError;
    [ObservableProperty] private string? currencyCodeError;
    [ObservableProperty] private string? dealerPriceError;
    [ObservableProperty] private string? msrpPriceError;
    [ObservableProperty] private string? stockQuantityError;

    public bool CanSave =>
        !IsBusy
        && string.IsNullOrWhiteSpace(SkuError)
        && string.IsNullOrWhiteSpace(NameError)
        && string.IsNullOrWhiteSpace(CurrencyCodeError)
        && string.IsNullOrWhiteSpace(DealerPriceError)
        && string.IsNullOrWhiteSpace(MsrpPriceError)
        && string.IsNullOrWhiteSpace(StockQuantityError)
        && !string.IsNullOrWhiteSpace(Sku)
        && !string.IsNullOrWhiteSpace(Name)
        && !string.IsNullOrWhiteSpace(CurrencyCode);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSave));
    partial void OnSkuChanged(string value)
    {
        ValidateSku();
        OnPropertyChanged(nameof(CanSave));
    }
    partial void OnNameChanged(string value)
    {
        ValidateName();
        OnPropertyChanged(nameof(CanSave));
    }
    partial void OnCurrencyCodeChanged(string value)
    {
        if (!_suppressCurrencyNormalize)
        {
            var upper = (value ?? "").Trim().ToUpperInvariant();
            if (!string.Equals(value, upper, StringComparison.Ordinal))
            {
                _suppressCurrencyNormalize = true;
                CurrencyCode = upper;
                _suppressCurrencyNormalize = false;
            }
        }
        ValidateCurrencyCode();
        OnPropertyChanged(nameof(CanSave));
    }
    partial void OnDealerPriceTextChanged(string value)
    {
        ValidateDealerPrice();
        OnPropertyChanged(nameof(CanSave));
    }
    partial void OnMsrpPriceTextChanged(string value)
    {
        ValidateMsrpPrice();
        OnPropertyChanged(nameof(CanSave));
    }
    partial void OnStockQuantityTextChanged(string value)
    {
        ValidateStockQuantity();
        OnPropertyChanged(nameof(CanSave));
    }

    public ObservableCollection<CategoryListItem> CategoryOptions { get; } = new();
    [ObservableProperty] private CategoryListItem? selectedCategory;

    public ObservableCollection<ImageRow> Images { get; } = new();
    public ObservableCollection<SpecRow> Specs { get; } = new();

    private void AttachImageRow(ImageRow row)
    {
        row.PropertyChanged += OnImageRowPropertyChanged;
    }

    private void DetachImageRow(ImageRow row)
    {
        row.PropertyChanged -= OnImageRowPropertyChanged;
    }

    private void OnImageRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ImageRow row) return;
        if (e.PropertyName == nameof(ImageRow.IsPrimary) && row.IsPrimary)
        {
            // Ensure single primary (radio button group doesn't automatically sync ViewModel properties)
            foreach (var other in Images)
            {
                if (!ReferenceEquals(other, row) && other.IsPrimary)
                    other.IsPrimary = false;
            }
        }
    }

    /// <summary>Sayfa navigasyonundan çağrılır (yeni üründe ProductId null kalabildiği için QueryProperty ile güvenilmez).</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        Error = null;
        ApiTraceId = null;
        CanDeleteProduct = false;
        ClearInlineErrors();
        foreach (var r in Images.ToList())
            DetachImageRow(r);
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
            {
                var row = new ImageRow { Url = img.Url, IsPrimary = img.IsPrimary, SortOrder = img.SortOrder };
                AttachImageRow(row);
                Images.Add(row);
            }
            NormalizeImageSort();

            foreach (var spec in p.Specs.OrderBy(s => s.SortOrder))
                Specs.Add(new SpecRow { Key = spec.Key, Value = spec.Value, SortOrder = spec.SortOrder });
            NormalizeSpecSort();

            // Ensure inline validations reflect loaded values.
            ValidateAll();
            OnPropertyChanged(nameof(CanSave));
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
    private void AddImage()
    {
        var row = new ImageRow { SortOrder = Images.Count };
        AttachImageRow(row);
        Images.Add(row);
        NormalizeImageSort();
    }

    [RelayCommand]
    private void RemoveImage(ImageRow? row)
    {
        if (row is null) return;
        DetachImageRow(row);
        Images.Remove(row);
        NormalizeImageSort();
    }

    [RelayCommand]
    private void MoveImageUp(ImageRow? row)
    {
        if (row is null) return;
        var idx = Images.IndexOf(row);
        if (idx <= 0) return;
        Images.Move(idx, idx - 1);
        NormalizeImageSort();
    }

    [RelayCommand]
    private void MoveImageDown(ImageRow? row)
    {
        if (row is null) return;
        var idx = Images.IndexOf(row);
        if (idx < 0 || idx >= Images.Count - 1) return;
        Images.Move(idx, idx + 1);
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
            var row = new ImageRow
            {
                Url = resp.Data,
                IsPrimary = Images.Count == 0,
                SortOrder = Images.Count
            };
            AttachImageRow(row);
            Images.Add(row);
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
    private void RemoveSpec(SpecRow? row)
    {
        if (row is null) return;
        Specs.Remove(row);
        NormalizeSpecSort();
    }

    [RelayCommand]
    private void MoveSpecUp(SpecRow? row)
    {
        if (row is null) return;
        var idx = Specs.IndexOf(row);
        if (idx <= 0) return;
        Specs.Move(idx, idx - 1);
        NormalizeSpecSort();
    }

    [RelayCommand]
    private void MoveSpecDown(SpecRow? row)
    {
        if (row is null) return;
        var idx = Specs.IndexOf(row);
        if (idx < 0 || idx >= Specs.Count - 1) return;
        Specs.Move(idx, idx + 1);
        NormalizeSpecSort();
    }

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

            if (!ValidateAll())
            {
                Error = "Lütfen işaretli alanları düzeltin.";
                return;
            }

            // Validate numeric inputs up-front (flexible parsing for comma/dot).
            if (!TryParseDecimalFlexible(DealerPriceText, out var dealerParsed) || dealerParsed < 0)
            {
                DealerPriceError = "Geçersiz. Örn: 12,50";
                Error = "Lütfen işaretli alanları düzeltin.";
                return;
            }
            if (!TryParseDecimalFlexible(MsrpPriceText, out var msrpParsed) || msrpParsed < 0)
            {
                MsrpPriceError = "Geçersiz. Örn: 12,50";
                Error = "Lütfen işaretli alanları düzeltin.";
                return;
            }
            if (!TryParseIntFlexible(StockQuantityText, out var stockParsed) || stockParsed < 0)
            {
                StockQuantityError = "Geçersiz. Örn: 10";
                Error = "Lütfen işaretli alanları düzeltin.";
                return;
            }

            DealerPrice = dealerParsed;
            MsrpPrice = msrpParsed;
            StockQuantity = stockParsed;

            NormalizeImageSort();
            NormalizeSpecSort();

            // Keep explicit sort order; normalize to 0..n-1 before sending.
            NormalizeImageSort();
            var images = Images
                .OrderBy(i => i.SortOrder)
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
                .OrderBy(s => s.SortOrder)
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
        // normalize sort order to current visual order
        for (var i = 0; i < Images.Count; i++)
        {
            Images[i].SortOrder = i;
            Images[i].CanMoveUp = i > 0;
            Images[i].CanMoveDown = i < Images.Count - 1;
        }

        // ensure single primary if any
        if (Images.Count == 0) return;

        var primary = Images.FirstOrDefault(i => i.IsPrimary);
        if (primary is null)
        {
            Images[0].IsPrimary = true;
            return;
        }

        for (var i = 0; i < Images.Count; i++)
            Images[i].IsPrimary = ReferenceEquals(Images[i], primary);
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
        [ObservableProperty] private int sortOrder;
        [ObservableProperty] private bool canMoveUp;
        [ObservableProperty] private bool canMoveDown;
    }

    public sealed partial class SpecRow : ObservableObject
    {
        [ObservableProperty] private string? key;
        [ObservableProperty] private string? value;
        [ObservableProperty] private int sortOrder;
        [ObservableProperty] private bool canMoveUp;
        [ObservableProperty] private bool canMoveDown;
    }

    private void NormalizeSpecSort()
    {
        for (var i = 0; i < Specs.Count; i++)
        {
            Specs[i].SortOrder = i;
            Specs[i].CanMoveUp = i > 0;
            Specs[i].CanMoveDown = i < Specs.Count - 1;
        }
    }

    private void ClearInlineErrors()
    {
        SkuError = null;
        NameError = null;
        CurrencyCodeError = null;
        DealerPriceError = null;
        MsrpPriceError = null;
        StockQuantityError = null;
        FocusField = null;
        OnPropertyChanged(nameof(CanSave));
    }

    private bool ValidateAll()
    {
        ValidateSku();
        ValidateName();
        ValidateCurrencyCode();
        ValidateDealerPrice();
        ValidateMsrpPrice();
        ValidateStockQuantity();
        FocusField = FirstInvalidField();
        OnPropertyChanged(nameof(CanSave));
        return CanSave;
    }

    private string? FirstInvalidField()
    {
        if (!string.IsNullOrWhiteSpace(SkuError)) return "Sku";
        if (!string.IsNullOrWhiteSpace(NameError)) return "Name";
        if (!string.IsNullOrWhiteSpace(CurrencyCodeError)) return "CurrencyCode";
        if (!string.IsNullOrWhiteSpace(StockQuantityError)) return "StockQuantity";
        if (!string.IsNullOrWhiteSpace(DealerPriceError)) return "DealerPrice";
        if (!string.IsNullOrWhiteSpace(MsrpPriceError)) return "MsrpPrice";
        return null;
    }

    private void ValidateSku()
    {
        var s = (Sku ?? "").Trim();
        SkuError = string.IsNullOrWhiteSpace(s) ? "SKU gerekli." : null;
    }

    private void ValidateName()
    {
        var s = (Name ?? "").Trim();
        NameError = string.IsNullOrWhiteSpace(s) ? "Ürün adı gerekli." : null;
    }

    private void ValidateCurrencyCode()
    {
        var raw = (CurrencyCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            CurrencyCodeError = "Para birimi gerekli (örn: TRY).";
            return;
        }
        if (raw.Length != 3 || !raw.All(char.IsLetter))
        {
            CurrencyCodeError = "3 harf olmalı (örn: TRY).";
            return;
        }
        CurrencyCodeError = null;
    }

    private void ValidateDealerPrice()
    {
        if (string.IsNullOrWhiteSpace(DealerPriceText))
        {
            DealerPriceError = "Gerekli.";
            return;
        }
        DealerPriceError = TryParseDecimalFlexible(DealerPriceText, out var v) && v >= 0
            ? null
            : "Geçersiz.";
    }

    private void ValidateMsrpPrice()
    {
        if (string.IsNullOrWhiteSpace(MsrpPriceText))
        {
            MsrpPriceError = "Gerekli.";
            return;
        }
        MsrpPriceError = TryParseDecimalFlexible(MsrpPriceText, out var v) && v >= 0
            ? null
            : "Geçersiz.";
    }

    private void ValidateStockQuantity()
    {
        if (string.IsNullOrWhiteSpace(StockQuantityText))
        {
            StockQuantityError = "Gerekli.";
            return;
        }
        StockQuantityError = TryParseIntFlexible(StockQuantityText, out var v) && v >= 0
            ? null
            : "Geçersiz.";
    }
}

