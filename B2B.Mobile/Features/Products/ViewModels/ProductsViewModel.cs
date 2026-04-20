using System.Collections.ObjectModel;
using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.Cart.Models;
using B2B.Mobile.Features.Cart.Services;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Features.Products.ViewModels;

public partial class ProductsViewModel : ObservableObject
{
    private readonly ProductsService _products;
    private readonly IAuthSession _authSession;
    private readonly ProductCatalogFilter _catalogFilter;
    private readonly CartService _cart;

    public ObservableCollection<ProductListItem> Items { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    [ObservableProperty] private string? query;
    [ObservableProperty] private string? scanFilterCode;
    [ObservableProperty] private bool canManageProducts;
    /// <summary>Yalnızca admin; açıkken pasif ürünler de listelenir (<c>isActive</c> filtresi kalkar).</summary>
    [ObservableProperty] private bool includeInactiveProducts;
    [ObservableProperty] private string catalogEmptyTitle = "Ürün bulunamadı";
    [ObservableProperty] private string catalogEmptyHint = "Aramanızı veya kategori seçimini değiştirmeyi deneyin.";
    /// <summary>1 = liste, 2 = ızgara; <see cref="Preferences"/> ile kalıcı.</summary>
    [ObservableProperty] private int catalogColumns = 1;

    public string FilterSummary => _catalogFilter.Summary;

    private const string PrefCatalogColumnsKey = "products_catalog_columns";

    private int _page = 1;
    private const int PageSize = 20;
    private bool _hasMore = true;
    private int _refreshInFlight;
    private CancellationTokenSource? _queryDebounceCts;
    private bool _suppressQueryRefresh;

    private const int SearchDebounceMs = 450;

    public bool ShowSkeleton => IsBusy && Items.Count == 0 && string.IsNullOrWhiteSpace(Error);

    public ProductsViewModel(ProductsService products, IAuthSession authSession, ProductCatalogFilter catalogFilter, CartService cart)
    {
        _products = products;
        _authSession = authSession;
        _catalogFilter = catalogFilter;
        _cart = cart;
        var saved = Preferences.Default.Get(PrefCatalogColumnsKey, 2);
        CatalogColumns = saved == 1 ? 1 : 2;
        _ = RefreshRolesAsync();
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowSkeleton));
    }

    public void NotifyFilterSummaryChanged() => OnPropertyChanged(nameof(FilterSummary));

    public async Task RefreshRolesAsync()
    {
        try
        {
            var token = await _authSession.GetAccessTokenAsync();
            CanManageProducts = JwtRoleReader.IsAdmin(token);
            if (!CanManageProducts)
                IncludeInactiveProducts = false;
        }
        catch
        {
            CanManageProducts = false;
            IncludeInactiveProducts = false;
        }
    }

    partial void OnQueryChanged(string? value)
    {
        if (_suppressQueryRefresh) return;

        // Kullanıcı manuel aramaya geçtiyse barkod filtresi bilgisini kaldır.
        if (!string.IsNullOrWhiteSpace(ScanFilterCode) && !string.Equals(value?.Trim(), ScanFilterCode, StringComparison.OrdinalIgnoreCase))
            ScanFilterCode = null;

        ScheduleDebouncedRefresh();
    }

    partial void OnIncludeInactiveProductsChanged(bool value) =>
        _ = RefreshAsync();

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowSkeleton));
    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(ShowSkeleton));

    [RelayCommand]
    private async Task ClearScanFilterAsync()
    {
        ScanFilterCode = null;
        _suppressQueryRefresh = true;
        Query = null;
        _suppressQueryRefresh = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ClearQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(Query)) return;
        _suppressQueryRefresh = true;
        Query = null;
        _suppressQueryRefresh = false;
        await RefreshAsync();
    }

    private void CancelSearchDebounce()
    {
        _queryDebounceCts?.Cancel();
        _queryDebounceCts?.Dispose();
        _queryDebounceCts = null;
    }

    private void ScheduleDebouncedRefresh()
    {
        CancelSearchDebounce();
        var cts = new CancellationTokenSource();
        _queryDebounceCts = cts;
        _ = DebouncedRefreshAsync(cts.Token);
    }

    private async Task DebouncedRefreshAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchDebounceMs, ct);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!ct.IsCancellationRequested)
                    await RefreshAsync();
            });
        }
        catch (OperationCanceledException)
        {
            // yeni tuş vuruşu veya manuel yenileme
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        CancelSearchDebounce();

        if (Interlocked.Exchange(ref _refreshInFlight, 1) == 1) return;

        IsRefreshing = true;
        try
        {
            var waitedMs = 0;
            while (IsBusy && waitedMs < 4000)
            {
                await Task.Delay(80);
                waitedMs += 80;
            }

            Items.Clear();
            _page = 1;
            _hasMore = true;
            await LoadMoreCoreAsync(force: true);
        }
        finally
        {
            IsRefreshing = false;
            Interlocked.Exchange(ref _refreshInFlight, 0);
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        await LoadMoreCoreAsync(force: false);
    }

    private async Task LoadMoreCoreAsync(bool force)
    {
        if ((!force && IsBusy) || !_hasMore) return;

        IsBusy = true;
        Error = null;
        ApiTraceId = null;

        try
        {
            bool? activeFilter = IncludeInactiveProducts ? null : true;
            var uncategorized = _catalogFilter.UncategorizedOnly ? true : (bool?)null;

            var resp = await _products.GetProductsAsync(
                _page,
                PageSize,
                sellerUserId: null,
                q: Query,
                isActive: activeFilter,
                categoryId: _catalogFilter.UncategorizedOnly ? null : _catalogFilter.CategoryId,
                uncategorized: uncategorized,
                ct: CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = FormatProductListError(resp.Error);
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                UpdateEmptyMessagingAfterFailedLoad();
                return;
            }

            var loadingFirstPage = _page == 1;
            foreach (var item in resp.Data.Items)
                Items.Add(item);

            _hasMore = Items.Count < resp.Data.Meta.Total;
            _page++;

            if (loadingFirstPage && Items.Count == 0)
                UpdateEmptyMessagingForZeroResults();
        }
        catch (Exception ex)
        {
            Error = $"Ürünler yüklenirken hata: {ex.Message}";
            UpdateEmptyMessagingAfterFailedLoad();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateEmptyMessagingForZeroResults()
    {
        var hasQuery = !string.IsNullOrWhiteSpace(Query);
        var hasScan = !string.IsNullOrWhiteSpace(ScanFilterCode);
        var hasCategory = _catalogFilter.CategoryId.HasValue;
        var uncategorized = _catalogFilter.UncategorizedOnly;

        if (hasScan)
        {
            CatalogEmptyTitle = "Barkod bulunamadı";
            CatalogEmptyHint = "Barkod filtresini temizleyip tekrar deneyin veya arama ile kontrol edin.";
        }
        else if (hasQuery || hasCategory || uncategorized)
        {
            CatalogEmptyTitle = "Sonuç bulunamadı";
            CatalogEmptyHint = "Arama metnini veya kategori seçimini değiştirmeyi deneyin.";
        }
        else
        {
            CatalogEmptyTitle = "Henüz ürün yok";
            if (IncludeInactiveProducts)
                CatalogEmptyHint = "Aktif veya pasif tüm ürünler için kayıt yok.";
            else if (CanManageProducts)
                CatalogEmptyHint = "Katalogda listelenecek ürün bulunmuyor. Pasif ürünleri göstermek için alttaki filtreyi açın.";
            else
                CatalogEmptyHint = "Katalogda listelenecek ürün bulunmuyor.";
        }
    }

    private void UpdateEmptyMessagingAfterFailedLoad()
    {
        CatalogEmptyTitle = "Liste yüklenemedi";
        CatalogEmptyHint = "Aşağıdaki hata mesajını kontrol edip yenileyin.";
    }

    private static string FormatProductListError(ApiError? err)
    {
        return UserFacingApiMessage.Message(err, "Ürün listesi alınamadı.");
    }

    [RelayCommand]
    private Task OpenAsync(ProductListItem item) =>
        Shell.Current.GoToAsync("productDetail", new Dictionary<string, object>
        {
            ["product"] = item
        });

    [RelayCommand]
    private async Task AddToCartQuickAsync(ProductListItem? item)
    {
        if (item is null) return;
        if (!item.IsActive)
        {
            await AlertAsync("Sepet", "Bu ürün pasif; sepete eklenemez.");
            return;
        }

        if (item.StockQuantity <= 0)
        {
            await AlertAsync("Sepet", "Bu ürün için stok yok.");
            return;
        }

        _cart.AddOrIncrement(new CartLine(
            item.ProductId,
            item.SellerUserId,
            item.SellerDisplayName,
            item.Name,
            item.Sku,
            item.CurrencyCode,
            item.DealerPrice,
            1));

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var snack = Snackbar.Make(
                "Sepete eklendi",
                async () => await Shell.Current.GoToAsync("//main/cart"),
                "Sepete git",
                TimeSpan.FromSeconds(3),
                new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#1F1F1F"),
                    TextColor = Colors.White,
                    ActionButtonTextColor = Color.FromArgb("#AC99EA")
                });
            await snack.Show();
        });
    }

    private static Task AlertAsync(string title, string message)
    {
        var page = Shell.Current?.CurrentPage;
        return page is null ? Task.CompletedTask : page.DisplayAlertAsync(title, message, "Tamam");
    }

    [RelayCommand]
    private Task ScanAsync() => Shell.Current.GoToAsync("productScan?returnTo=catalog");

    [RelayCommand]
    private void ToggleCatalogLayout()
    {
        CatalogColumns = CatalogColumns == 1 ? 2 : 1;
        Preferences.Default.Set(PrefCatalogColumnsKey, CatalogColumns);
    }

    public async Task ApplyScannedCodeAsync(string code)
    {
        var trimmed = code.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;

        ScanFilterCode = trimmed;

        IsBusy = true;
        Error = null;
        try
        {
            bool? activeFilter = IncludeInactiveProducts ? null : true;
            var uncategorized = _catalogFilter.UncategorizedOnly ? true : (bool?)null;

            var resp = await _products.GetProductsAsync(
                page: 1,
                pageSize: 2,
                sellerUserId: null,
                q: trimmed,
                isActive: activeFilter,
                categoryId: _catalogFilter.UncategorizedOnly ? null : _catalogFilter.CategoryId,
                uncategorized: uncategorized,
                ct: CancellationToken.None);

            if (resp.Success && resp.Data is not null && resp.Data.Meta.Total == 1 && resp.Data.Items.Count >= 1)
            {
                await Shell.Current.GoToAsync("productDetail", new Dictionary<string, object>
                {
                    ["product"] = resp.Data.Items[0]
                });
                return;
            }

            _suppressQueryRefresh = true;
            Query = trimmed;
            _suppressQueryRefresh = false;
            IsBusy = false;
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
