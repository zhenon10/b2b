using B2B.Mobile.Features.Auth.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Auth.Views;

public partial class PendingDealersPage : ContentPage
{
    private readonly PendingDealersViewModel _vm;

    public PendingDealersPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<PendingDealersViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.RefreshCommand is IAsyncRelayCommand rc)
            await rc.ExecuteAsync(null);
    }
}
