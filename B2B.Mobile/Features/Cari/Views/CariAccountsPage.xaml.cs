using B2B.Contracts;
using B2B.Mobile.Features.Cari.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Cari.Views;

public partial class CariAccountsPage : ContentPage
{
    private readonly CariAccountsViewModel _vm;

    public CariAccountsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<CariAccountsViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Items.Count == 0 && !_vm.IsBusy)
            await _vm.RefreshAsync();
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is not CustomerAccountSummary row)
            return;

        if (sender is CollectionView cv)
            cv.SelectedItem = null;

        await Shell.Current.GoToAsync("cariEntries", new Dictionary<string, object>
        {
            ["sellerUserId"] = row.SellerUserId,
            ["currencyCode"] = row.CurrencyCode,
            ["sellerDisplayName"] = row.SellerDisplayName ?? ""
        });
    }
}

