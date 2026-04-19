using B2B.Mobile.Features.Orders.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Orders.Views;

public partial class OrderPage : ContentPage
{
    private readonly OrderViewModel _dealerVm;

    public OrderPage()
    {
        InitializeComponent();
        _dealerVm = App.Services.GetRequiredService<OrderViewModel>();
        BindingContext = _dealerVm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _dealerVm.EnsureHistoryFreshAsync(TimeSpan.FromMinutes(2));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Sipariş", "Sayfa yüklenirken bir sorun oluştu. Tekrar deneyin.", "Tamam");
        }
    }
}
