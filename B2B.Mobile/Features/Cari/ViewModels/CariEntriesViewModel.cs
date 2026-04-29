using System.Collections.ObjectModel;
using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Cari.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Cari.ViewModels;

public sealed partial class CariEntriesViewModel : ObservableObject
{
    private readonly CariService _svc;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    [ObservableProperty] private bool hasMore;

    [ObservableProperty] private Guid sellerUserId;
    [ObservableProperty] private string currencyCode = "USD";
    [ObservableProperty] private string? sellerDisplayName;

    public ObservableCollection<CustomerAccountEntryDto> Items { get; } = new();

    private int _page = 1;
    private const int PageSize = 30;

    public CariEntriesViewModel(CariService svc) => _svc = svc;

    public string TitleLine =>
        string.IsNullOrWhiteSpace(SellerDisplayName)
            ? $"{CurrencyCode} Ekstre"
            : $"{SellerDisplayName} • {CurrencyCode}";

    partial void OnSellerDisplayNameChanged(string? value) => OnPropertyChanged(nameof(TitleLine));
    partial void OnCurrencyCodeChanged(string value) => OnPropertyChanged(nameof(TitleLine));

    public void Init(Guid sellerId, string currency, string? sellerName)
    {
        SellerUserId = sellerId;
        CurrencyCode = (currency ?? "").Trim().ToUpperInvariant();
        SellerDisplayName = sellerName;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        try
        {
            _page = 1;
            Items.Clear();
            await LoadNextPageInternalAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsBusy || !HasMore) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        try
        {
            await LoadNextPageInternalAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadNextPageInternalAsync()
    {
        if (SellerUserId == Guid.Empty || string.IsNullOrWhiteSpace(CurrencyCode))
        {
            HasMore = false;
            return;
        }

        var resp = await _svc.EntriesAsync(SellerUserId, CurrencyCode, _page, PageSize, CancellationToken.None);
        if (!resp.Success || resp.Data is null)
        {
            Error = UserFacingApiMessage.Message(resp.Error, "Cari ekstre yüklenemedi.");
            ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
            HasMore = false;
            return;
        }

        foreach (var it in resp.Data.Items)
            Items.Add(it);

        var meta = resp.Data.Meta;
        var loadedSoFar = meta.Page * meta.PageSize;
        HasMore = loadedSoFar < meta.Total && meta.Returned > 0;
        _page++;
    }
}

