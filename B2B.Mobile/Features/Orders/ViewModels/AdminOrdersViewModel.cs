using System.Collections.ObjectModel;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Orders;
using B2B.Mobile.Features.Orders.Models;
using B2B.Mobile.Features.Orders.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Orders.ViewModels;

public partial class AdminOrdersViewModel : ObservableObject
{
    private const int PageSize = 20;
    private readonly AdminOrdersService _orders;
    private DateTime? _lastListSuccessUtc;

    public sealed record NextStatusOption(int Value, string Label);

    public ObservableCollection<AdminOrderListItem> Items { get; } = new();
    public ObservableCollection<NextStatusOption> NextStatusOptions { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private int page = 1;
    [ObservableProperty] private long totalCount;
    [ObservableProperty] private AdminOrderDetail? selectedDetail;
    [ObservableProperty] private NextStatusOption? selectedNextStatusOption;
    [ObservableProperty] private string? detailError;
    [ObservableProperty] private bool hasSelectedDetail;
    /// <summary>Filtre: boş = tümü, aksi halde durum sayısı (0–4).</summary>
    [ObservableProperty] private string statusFilter = "";

    /// <summary>Picker: 0=Tümü, 1=Taslak … 5=İptal</summary>
    [ObservableProperty] private int filterPickerIndex;
    /// <summary>Admin liste için çek-bırak yenileme göstergesi.</summary>
    [ObservableProperty] private bool isListRefreshing;

    public int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public string PageSummary => $"Sayfa {Page}/{TotalPages}";

    public bool CanGoPrev => Page > 1;

    public bool CanGoNext => Page < TotalPages;

    public AdminOrdersViewModel(AdminOrdersService orders) => _orders = orders;

    /// <summary>
    /// Son başarılı listeden bu yana <paramref name="maxAge"/> aşıldıysa yeniler.
    /// Sayfa tekrar göründüğünde gereksiz istekleri azaltmak için kullanılır.
    /// </summary>
    public async Task EnsureFreshListAsync(TimeSpan maxAge)
    {
        if (_lastListSuccessUtc is DateTime t && DateTime.UtcNow - t < maxAge)
            return;
        await RefreshAsync();
    }

    public bool CanSaveDetailStatus =>
        SelectedDetail is not null
        && SelectedNextStatusOption is not null
        && SelectedNextStatusOption.Value != SelectedDetail.Status;

    public bool CanEditOrderStatus =>
        SelectedDetail is not null && SelectedDetail.Status is not 3 and not 4;

    partial void OnSelectedDetailChanged(AdminOrderDetail? value)
    {
        HasSelectedDetail = value is not null;
        RebuildNextStatusOptions();
        OnPropertyChanged(nameof(CanSaveDetailStatus));
        OnPropertyChanged(nameof(CanEditOrderStatus));
        OnPropertyChanged(nameof(DetailStatusLine));
        OnPropertyChanged(nameof(DetailGrandTotalLine));
    }

    /// <summary>Detay başlığı altında gösterilen durum metni.</summary>
    public string DetailStatusLine =>
        SelectedDetail is { } d ? $"Durum: {StatusLabel(d.Status)}" : "";

    public string DetailGrandTotalLine =>
        SelectedDetail is { } d ? $"Toplam: {d.GrandTotal:0.##} {d.CurrencyCode}" : "";

    partial void OnSelectedNextStatusOptionChanged(NextStatusOption? value) =>
        OnPropertyChanged(nameof(CanSaveDetailStatus));

    partial void OnPageChanged(int value)
    {
        NotifyNav();
        OnPropertyChanged(nameof(PageSummary));
    }

    partial void OnTotalCountChanged(long value)
    {
        NotifyNav();
        OnPropertyChanged(nameof(PageSummary));
    }

    private void NotifyNav()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoPrev));
        OnPropertyChanged(nameof(CanGoNext));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        await RefreshCoreAsync(showGlobalBusy: true);
    }

    [RelayCommand]
    private async Task PullToRefreshAsync()
    {
        if (IsListRefreshing) return;
        IsListRefreshing = true;
        try
        {
            await RefreshCoreAsync(showGlobalBusy: false);
        }
        finally
        {
            IsListRefreshing = false;
        }
    }

    /// <summary>Liste yenileme; durum kaydı gibi işlemler <see cref="IsBusy"/> varken de çağrılabilir.</summary>
    private async Task RefreshCoreAsync(bool showGlobalBusy = true)
    {
        if (showGlobalBusy)
            IsBusy = true;
        Error = null;
        try
        {
            int? st = string.IsNullOrWhiteSpace(StatusFilter) ? null : int.Parse(StatusFilter);
            var resp = await _orders.GetListAsync(Page, PageSize, st, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = FormatAdminApiError(resp.Error) ?? resp.Error?.Message ?? "Siparişler yüklenemedi.";
                Items.Clear();
                TotalCount = 0;
                return;
            }

            Items.Clear();
            foreach (var x in resp.Data.Items)
                Items.Add(x);
            TotalCount = resp.Data.Meta.Total;
            _lastListSuccessUtc = DateTime.UtcNow;
        }
        finally
        {
            if (showGlobalBusy)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectOrderAsync(AdminOrderListItem? item)
    {
        if (item is null) return;
        DetailError = null;
        IsBusy = true;
        try
        {
            var resp = await _orders.GetDetailAsync(item.OrderId, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                DetailError = FormatAdminApiError(resp.Error) ?? "Detay yüklenemedi.";
                SelectedDetail = null;
                return;
            }

            SelectedDetail = resp.Data;
            DetailError = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildNextStatusOptions()
    {
        NextStatusOptions.Clear();
        SelectedNextStatusOption = null;
        if (SelectedDetail is null) return;

        var cur = SelectedDetail.Status;
        NextStatusOptions.Add(new NextStatusOption(cur, $"{StatusLabel(cur)} (mevcut)"));
        foreach (var x in AllowedNextStatuses(cur))
            NextStatusOptions.Add(new NextStatusOption(x.Value, x.Label));

        SelectedNextStatusOption = NextStatusOptions.FirstOrDefault(o => o.Value == cur);
    }

    [RelayCommand]
    private void CloseDetail()
    {
        SelectedDetail = null;
        DetailError = null;
    }

    [RelayCommand]
    private async Task SaveStatusAsync()
    {
        if (SelectedDetail is null || SelectedNextStatusOption is null || SelectedNextStatusOption.Value == SelectedDetail.Status)
            return;
        var orderId = SelectedDetail.OrderId;
        var newStatus = SelectedNextStatusOption.Value;
        DetailError = null;
        IsBusy = true;
        try
        {
            var resp = await _orders.UpdateStatusAsync(orderId, newStatus, CancellationToken.None);
            if (!resp.Success)
            {
                DetailError = FormatAdminApiError(resp.Error) ?? "Durum güncellenemedi.";
                return;
            }

            await RefreshCoreAsync();
            var d = await _orders.GetDetailAsync(orderId, CancellationToken.None);
            if (d.Success && d.Data is not null)
                SelectedDetail = d.Data;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        StatusFilter = FilterPickerIndex switch
        {
            0 => "",
            1 => "0",
            2 => "1",
            3 => "2",
            4 => "3",
            5 => "4",
            _ => ""
        };
        Page = 1;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task PrevPageAsync()
    {
        if (Page <= 1) return;
        Page--;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page >= TotalPages) return;
        Page++;
        await RefreshAsync();
    }

    public static string StatusLabel(int s) => OrderStatuses.ToTrLabel(s);

    private static string? FormatAdminApiError(ApiError? err)
    {
        if (err is null) return null;
        return err.Code switch
        {
            "not_found" => "Sipariş bulunamadı.",
            "invalid_status_transition" => "Bu durum geçişine izin verilmiyor.",
            "unauthorized" => "Oturum geçersiz. Yeniden giriş yapın.",
            "forbidden" => "Bu sayfa veya işlem yalnızca yöneticiler içindir.",
            _ => err.Message
        };
    }

    /// <summary>Geçerli sipariş durumundan izin verilen hedef durumlar (API kurallarıyla uyumlu).</summary>
    public static (int Value, string Label)[] AllowedNextStatuses(int current) => current switch
    {
        0 => new[] { (1, "Verildi"), (4, "İptal") },
        1 => new[] { (2, "Ödendi"), (4, "İptal") },
        2 => new[] { (3, "Kargoda"), (4, "İptal") },
        _ => Array.Empty<(int, string)>()
    };
}
