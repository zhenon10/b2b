using B2B.Mobile.Features.Cari.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Cari.Views;

[QueryProperty(nameof(SellerUserId), "sellerUserId")]
[QueryProperty(nameof(CurrencyCode), "currencyCode")]
[QueryProperty(nameof(SellerDisplayName), "sellerDisplayName")]
public partial class CariEntriesPage : ContentPage
{
    private readonly CariEntriesViewModel _vm;

    public CariEntriesPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<CariEntriesViewModel>();
        BindingContext = _vm;
    }

    public string? SellerUserId { get; set; }
    public string? CurrencyCode { get; set; }
    public string? SellerDisplayName { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_vm.IsBusy) return;

        if (Guid.TryParse(SellerUserId, out var sellerId) && !string.IsNullOrWhiteSpace(CurrencyCode))
        {
            _vm.Init(sellerId, CurrencyCode!, SellerDisplayName);
            if (_vm.Items.Count == 0)
                await _vm.RefreshAsync();
        }
    }
}

