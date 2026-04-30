using B2B.Contracts;
using B2B.Domain.Enums;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Connectivity;
using B2B.Mobile.Features.Cart.Services;
using B2B.Mobile.Features.Orders.Services;
using B2B.Mobile.Features.Orders;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace B2B.Mobile.Features.Orders.ViewModels;

public partial class OrderViewModel : ObservableObject
{
    private const int HistoryPageSize = 20;

    private readonly CartService _cart;
    private readonly OrdersService _orders;
    private readonly ConnectivityService _connectivity;
    private string? _idempotencyKey;
    private string? _lastSubmitErrorCode;
    private DateTime? _lastHistorySuccessUtc;
    private Guid? _pendingHistoryDetailOrderId;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? errorTraceId;
    [ObservableProperty] private string currencyCode = "USD";
    [ObservableProperty] private string? result;
    [ObservableProperty] private decimal total;
    [ObservableProperty] private string totalWithCurrency = "0";
    /// <summary>Tek satıcı özeti, örn. "Satıcı: Mağaza Adı" veya sepet boşken "Satıcı: —".</summary>
    [ObservableProperty] private string sellerSummary = "Satıcı: —";
    [ObservableProperty] private bool canSubmit;

    /// <summary>
    /// Yalnızca idempotency çatışması yaşandığında kullanıcıya "yeni anahtar ile tekrar dene" aksiyonu gösterilir.
    /// </summary>
    public bool CanRetryWithNewKey => string.Equals(_lastSubmitErrorCode, "idempotency_conflict", StringComparison.OrdinalIgnoreCase);

    /// <summary>0 = sepet, 1 = sipariş geçmişi.</summary>
    [ObservableProperty] private int dealerPanel;

    [ObservableProperty] private bool isHistoryBusy;
    [ObservableProperty] private bool isHistoryRefreshing;
    [ObservableProperty] private string? historyError;
    [ObservableProperty] private string? historyErrorTraceId;
    [ObservableProperty] private int historyPage = 1;
    [ObservableProperty] private long historyTotalCount;
    [ObservableProperty] private DealerOrderDetail? selectedHistoryDetail;
    [ObservableProperty] private string? historyDetailError;
    [ObservableProperty] private string? historyDetailErrorTraceId;
    [ObservableProperty] private bool hasHistoryDetail;
    [ObservableProperty] private bool isHistoryDetailBusy;

    public ObservableCollection<CartLineVm> Lines { get; } = new();
    public ObservableCollection<DealerOrderListItem> HistoryItems { get; } = new();

    public bool HasCartLines => Lines.Count > 0;

    public OrderViewModel(CartService cart, OrdersService orders, ConnectivityService connectivity)
    {
        _cart = cart;
        _orders = orders;
        _connectivity = connectivity;

        if (_cart.Lines is INotifyCollectionChanged changed)
            changed.CollectionChanged += (_, __) => Recompute();

        Recompute();
    }

    public sealed record CartLineVm(
        string Name,
        string Sku,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        string CurrencyCode)
    {
        public string UnitDisplay => $"{UnitPrice:0.##} {CurrencyCode}";
        public string LineDisplay => $"{LineTotal:0.##} {CurrencyCode}";
    }

    public bool IsCartPanel => DealerPanel == 0;

    public bool IsHistoryPanel => DealerPanel == 1;

    public int HistoryTotalPages =>
        HistoryTotalCount <= 0 ? 1 : (int)Math.Ceiling(HistoryTotalCount / (double)HistoryPageSize);

    public string HistoryPageSummary => $"Sayfa {HistoryPage}/{HistoryTotalPages}";

    public bool CanHistoryPrev => HistoryPage > 1;

    public bool CanHistoryNext => HistoryPage < HistoryTotalPages;

    /// <summary>Detay API başarısız; liste satırı seçili kaldı, yeniden deneme için sipariş kimliği tutulur.</summary>
    public bool ShowHistoryDetailLoadFailure =>
        !HasHistoryDetail && !string.IsNullOrWhiteSpace(HistoryDetailError);

    public string HistoryDetailStatusLine =>
        SelectedHistoryDetail is { } d ? $"Durum: {OrderStatuses.ToTrLabel((int)d.Status)}" : "";

    public string HistoryDetailTotalLine =>
        SelectedHistoryDetail is { } d ? $"Toplam: {d.GrandTotal:0.##} {d.CurrencyCode}" : "";

    /// <summary>Kargoda veya iptal değilse bayi iptal edebilir.</summary>
    public bool CanCancelSelectedOrder =>
        SelectedHistoryDetail is { } d && d.Status is not OrderStatus.Shipped and not OrderStatus.Cancelled;

    partial void OnDealerPanelChanged(int value)
    {
        OnPropertyChanged(nameof(IsCartPanel));
        OnPropertyChanged(nameof(IsHistoryPanel));
        if (value == 1)
            _ = EnsureHistoryLoadedAsync();
    }

    partial void OnSelectedHistoryDetailChanged(DealerOrderDetail? value)
    {
        HasHistoryDetail = value is not null;
        OnPropertyChanged(nameof(HistoryDetailStatusLine));
        OnPropertyChanged(nameof(HistoryDetailTotalLine));
        OnPropertyChanged(nameof(CanCancelSelectedOrder));
        OnPropertyChanged(nameof(ShowHistoryDetailLoadFailure));
    }

    partial void OnHistoryDetailErrorChanged(string? value) =>
        OnPropertyChanged(nameof(ShowHistoryDetailLoadFailure));

    partial void OnHistoryPageChanged(int value)
    {
        NotifyHistoryNav();
    }

    partial void OnHistoryTotalCountChanged(long value)
    {
        NotifyHistoryNav();
    }

    private void NotifyHistoryNav()
    {
        OnPropertyChanged(nameof(HistoryTotalPages));
        OnPropertyChanged(nameof(HistoryPageSummary));
        OnPropertyChanged(nameof(CanHistoryPrev));
        OnPropertyChanged(nameof(CanHistoryNext));
    }

    [RelayCommand]
    private void ShowCartPanel() => DealerPanel = 0;

    [RelayCommand]
    private void ShowHistoryPanel() => DealerPanel = 1;

    public async Task EnsureHistoryFreshAsync(TimeSpan maxAge)
    {
        if (DealerPanel != 1) return;
        if (_lastHistorySuccessUtc is DateTime t && DateTime.UtcNow - t < maxAge)
            return;
        await LoadHistoryPageAsync(HistoryPage, clearSelection: false);
    }

    private async Task EnsureHistoryLoadedAsync()
    {
        if (HistoryItems.Count > 0 || IsHistoryBusy) return;
        await LoadHistoryPageAsync(1, clearSelection: true);
    }

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        if (IsHistoryBusy) return;
        IsHistoryRefreshing = true;
        try
        {
            await LoadHistoryPageAsync(HistoryPage, clearSelection: false);
        }
        finally
        {
            IsHistoryRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RetryHistoryAsync()
    {
        if (IsHistoryBusy) return;
        await LoadHistoryPageAsync(HistoryPage, clearSelection: false);
    }

    [RelayCommand]
    private async Task RetrySubmitAsync() => await SubmitAsync();

    [RelayCommand]
    private async Task RetryWithNewKeyAsync()
    {
        ResetIdempotency();
        await SubmitAsync();
    }

    [RelayCommand]
    private async Task RetryHistoryDetailAsync()
    {
        if (IsHistoryDetailBusy || _pendingHistoryDetailOrderId is not Guid id)
            return;
        await LoadHistoryDetailForOrderAsync(id, CancellationToken.None, clearDetailFirst: true);
    }

    /// <summary>Açık detay kartında hata (ör. iptal) sonrası sunucudan yeniden yükle.</summary>
    [RelayCommand]
    private async Task RefreshSelectedHistoryDetailAsync()
    {
        if (IsHistoryDetailBusy || SelectedHistoryDetail is not { OrderId: var id })
            return;
        await LoadHistoryDetailForOrderAsync(id, CancellationToken.None, clearDetailFirst: false);
    }

    private async Task LoadHistoryPageAsync(int page, bool clearSelection)
    {
        if (IsHistoryBusy) return;
        IsHistoryBusy = true;
        HistoryError = null;
        HistoryErrorTraceId = null;
        if (clearSelection)
        {
            SelectedHistoryDetail = null;
            HistoryDetailError = null;
            HistoryDetailErrorTraceId = null;
            _pendingHistoryDetailOrderId = null;
            OnPropertyChanged(nameof(ShowHistoryDetailLoadFailure));
        }

        try
        {
            var resp = await _orders.GetMyOrdersAsync(page, HistoryPageSize, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                HistoryError = FormatOrderApiError(resp.Error) ?? "Siparişler yüklenemedi.";
                HistoryErrorTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            HistoryItems.Clear();
            foreach (var row in resp.Data.Items)
                HistoryItems.Add(row);

            HistoryPage = resp.Data.Meta.Page;
            HistoryTotalCount = resp.Data.Meta.Total;
            _lastHistorySuccessUtc = DateTime.UtcNow;
            NotifyHistoryNav();
        }
        finally
        {
            IsHistoryBusy = false;
        }
    }

    [RelayCommand]
    private async Task HistoryPrevPageAsync()
    {
        if (!CanHistoryPrev || IsHistoryBusy) return;
        await LoadHistoryPageAsync(HistoryPage - 1, clearSelection: true);
    }

    [RelayCommand]
    private async Task HistoryNextPageAsync()
    {
        if (!CanHistoryNext || IsHistoryBusy) return;
        await LoadHistoryPageAsync(HistoryPage + 1, clearSelection: true);
    }

    [RelayCommand]
    private async Task SelectHistoryOrderAsync(DealerOrderListItem? item)
    {
        if (item is null) return;
        _pendingHistoryDetailOrderId = item.OrderId;
        await LoadHistoryDetailForOrderAsync(item.OrderId, CancellationToken.None, clearDetailFirst: true);
    }

    [RelayCommand]
    private void CloseHistoryDetail()
    {
        SelectedHistoryDetail = null;
        HistoryDetailError = null;
        HistoryDetailErrorTraceId = null;
        _pendingHistoryDetailOrderId = null;
        OnPropertyChanged(nameof(ShowHistoryDetailLoadFailure));
    }

    private async Task LoadHistoryDetailForOrderAsync(Guid orderId, CancellationToken ct, bool clearDetailFirst)
    {
        HistoryDetailError = null;
        HistoryDetailErrorTraceId = null;
        if (clearDetailFirst)
        {
            SelectedHistoryDetail = null;
            OnPropertyChanged(nameof(ShowHistoryDetailLoadFailure));
        }

        IsHistoryDetailBusy = true;
        try
        {
            var resp = await _orders.GetMyOrderDetailAsync(orderId, ct);
            if (!resp.Success || resp.Data is null)
            {
                HistoryDetailError = FormatOrderApiError(resp.Error) ?? "Detay yüklenemedi.";
                HistoryDetailErrorTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                if (clearDetailFirst)
                    OnPropertyChanged(nameof(ShowHistoryDetailLoadFailure));
                return;
            }

            _pendingHistoryDetailOrderId = null;
            SelectedHistoryDetail = resp.Data;
        }
        finally
        {
            IsHistoryDetailBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelSelectedOrderAsync()
    {
        if (SelectedHistoryDetail is not { } d || !CanCancelSelectedOrder || IsHistoryBusy) return;
        if (_connectivity.IsOffline)
        {
            HistoryDetailError = "İnternet bağlantısı yok. Sipariş iptal edilemedi.";
            HistoryDetailErrorTraceId = null;
            return;
        }

        var page = Shell.Current?.CurrentPage;
        if (page is not null)
        {
            var ok = await page.DisplayAlertAsync(
                "Sipariş iptali",
                $"Sipariş #{d.OrderNumber} iptal edilsin mi? Uygun aşamalarda stok iade edilir.",
                "İptal et",
                "Vazgeç");
            if (!ok) return;
        }

        IsHistoryBusy = true;
        HistoryDetailError = null;
        HistoryDetailErrorTraceId = null;
        try
        {
            var resp = await _orders.CancelMyOrderAsync(d.OrderId, CancellationToken.None);
            if (!resp.Success)
            {
                HistoryDetailError = FormatOrderApiError(resp.Error) ?? "Sipariş iptal edilemedi.";
                HistoryDetailErrorTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            SelectedHistoryDetail = null;
            await LoadHistoryPageAsync(HistoryPage, clearSelection: false);
        }
        finally
        {
            IsHistoryBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ErrorTraceId = null;
        Result = null;
        _lastSubmitErrorCode = null;
        OnPropertyChanged(nameof(CanRetryWithNewKey));

        try
        {
            if (_connectivity.IsOffline)
            {
                Error = "İnternet bağlantısı yok. Sipariş gönderilemedi.";
                ErrorTraceId = null;
                return;
            }

            if (!CanSubmit)
            {
                Error ??= "Sipariş göndermeye hazır değil.";
                return;
            }

            var sellerIds = _cart.Lines.Select(l => l.SellerUserId).Distinct().ToList();
            if (sellerIds.Count != 1)
            {
                Error = "Sepette yalnızca tek satıcıya ait ürünler olmalıdır.";
                ErrorTraceId = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(_idempotencyKey))
                _idempotencyKey = Guid.NewGuid().ToString("N");

            var resp = await _orders.SubmitOrderAsync(
                sellerIds[0],
                CurrencyCode,
                _cart.Lines.ToList(),
                _idempotencyKey,
                CancellationToken.None);

            if (!resp.Success || resp.Data is null)
            {
                _lastSubmitErrorCode = resp.Error?.Code;
                OnPropertyChanged(nameof(CanRetryWithNewKey));
                Error = FormatOrderApiError(resp.Error) ?? "Sipariş gönderilemedi.";
                ErrorTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            Result = $"Sipariş #{resp.Data.OrderNumber} alındı. Toplam: {resp.Data.GrandTotal:0.00} {CurrencyCode}";
            _cart.Clear();
            _idempotencyKey = null;
            _lastSubmitErrorCode = null;
            OnPropertyChanged(nameof(CanRetryWithNewKey));

            // Keep history stale so "Siparişlerim" CTA can force refresh.
            _lastHistorySuccessUtc = null;
            if (DealerPanel == 1)
                await LoadHistoryPageAsync(1, clearSelection: true);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var msg = $"Sipariş #{resp.Data.OrderNumber} alındı";
                var snack = Snackbar.Make(
                    msg,
                    () =>
                    {
                        DealerPanel = 1;
                        _ = EnsureHistoryFreshAsync(TimeSpan.Zero);
                    },
                    "Siparişlerim’e git",
                    TimeSpan.FromSeconds(4),
                    new SnackbarOptions
                    {
                        BackgroundColor = Color.FromArgb("#1F1F1F"),
                        TextColor = Colors.White,
                        ActionButtonTextColor = Color.FromArgb("#AC99EA")
                    });
                await snack.Show();
            });
        }
        catch (OperationCanceledException)
        {
            Error = "İşlem iptal edildi.";
            ErrorTraceId = null;
        }
        catch (Exception ex)
        {
            Error = $"Beklenmeyen hata: {ex.Message}";
            ErrorTraceId = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static Task GoToCartAsync() =>
        Microsoft.Maui.Controls.Shell.Current.GoToAsync("//main/cart");

    [RelayCommand]
    private static Task GoToProductsAsync() =>
        Microsoft.Maui.Controls.Shell.Current.GoToAsync("//main/products");

    [RelayCommand]
    private void ResetIdempotency()
    {
        _idempotencyKey = Guid.NewGuid().ToString("N");
        Result = null;
        Error = null;
        ErrorTraceId = null;
        _lastSubmitErrorCode = null;
        OnPropertyChanged(nameof(CanRetryWithNewKey));
    }

    private void Recompute()
    {
        Lines.Clear();
        foreach (var line in _cart.Lines.OrderBy(l => l.Name))
            Lines.Add(new CartLineVm(
                line.Name,
                line.Sku,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal,
                line.CurrencyCode.ToUpperInvariant()));
        OnPropertyChanged(nameof(HasCartLines));

        Total = _cart.Total;

        var sellerIds = _cart.Lines.Select(l => l.SellerUserId).Distinct().ToList();
        string? sellerName = null;
        if (sellerIds.Count == 1)
        {
            var sid = sellerIds[0];
            var name = _cart.Lines.FirstOrDefault(l => l.SellerUserId == sid)?.SellerDisplayName;
            sellerName = string.IsNullOrWhiteSpace(name) ? sid.ToString() : name;
        }

        SellerSummary = sellerName is { } n ? $"Satıcı: {n}" : "Satıcı: —";

        var currencies = _cart.Lines.Select(l => l.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (currencies.Count == 1)
        {
            CurrencyCode = currencies[0].ToUpperInvariant();
            if (sellerIds.Count <= 1)
            {
                Error = null;
                ErrorTraceId = null;
            }
        }
        else if (currencies.Count > 1)
        {
            Error = "Sepet birden fazla para birimi içeriyor. Aynı para birimindeki ürünlerle sipariş verin.";
            ErrorTraceId = null;
        }

        if (sellerIds.Count > 1)
        {
            Error = "Sepette yalnızca tek satıcıya ait ürünler olabilir.";
            ErrorTraceId = null;
        }

        TotalWithCurrency = _cart.Lines.Count == 0
            ? $"0 {CurrencyCode}"
            : $"{Total:0.##} {CurrencyCode}";

        CanSubmit = _cart.Lines.Count > 0
                    && sellerIds.Count == 1
                    && currencies.Count == 1
                    && !IsBusy;
    }

    /// <summary>Sipariş gönderme, geçmiş, iptal ve detay için ortak hata metni.</summary>
    private static string? FormatOrderApiError(ApiError? err)
    {
        if (err is null) return null;

        if (err.Code is "timeout" or "network_error" or "empty_response" or "invalid_response" or "server_error" or "unauthorized" or "forbidden")
            return UserFacingApiMessage.Message(err, "Sipariş işlemi başarısız.");

        if (err.Code == "not_found")
            return "Sipariş bulunamadı veya artık erişilemiyor.";

        if (err.Code == "cannot_cancel")
            return "Bu sipariş bu aşamada iptal edilemez.";

        if (err.Code == "invalid_status_transition")
            return "Sipariş durumu bu işlem için uygun değil.";

        if (err.Code == "insufficient_stock" && err.Details is not null)
        {
            err.Details.TryGetValue("sku", out var sku);
            err.Details.TryGetValue("available", out var available);
            err.Details.TryGetValue("requested", out var requested);
            return $"Yetersiz stok: {sku?.FirstOrDefault() ?? "ürün"} (mevcut {available?.FirstOrDefault() ?? "?"}, istenen {requested?.FirstOrDefault() ?? "?"}).";
        }

        if (err.Code == "idempotency_conflict")
            return "Bu gönderim anahtarı farklı bir sepetle kullanılmış. «Yeni anahtar» ile tekrar deneyin.";

        if (err.Code == "invalid_seller")
            return "Ürünler seçilen satıcı ile eşleşmiyor.";

        if (err.Code == "currency_mismatch")
            return "Ürün para birimleri uyuşmuyor.";

        if (err.Code == "empty_order")
            return "Sepet boş olamaz.";

        if (err.Code == "invalid_products")
            return "Bir veya daha fazla ürün geçersiz.";

        if (err.Code == "inactive_product")
            return "Sepette pasif ürün var.";

        if (err.Code == "invalid_quantity")
            return "Geçersiz adet.";

        return string.IsNullOrWhiteSpace(err.Message) ? null : err.Message;
    }
}
