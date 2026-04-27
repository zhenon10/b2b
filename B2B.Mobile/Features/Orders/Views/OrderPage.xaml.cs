using B2B.Mobile.Features.Orders.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace B2B.Mobile.Features.Orders.Views;

public partial class OrderPage : ContentPage
{
    private readonly OrderViewModel _dealerVm;

    public OrderPage()
    {
        InitializeComponent();
        _dealerVm = App.Services.GetRequiredService<OrderViewModel>();
        BindingContext = _dealerVm;

        _dealerVm.PropertyChanged += DealerVmOnPropertyChanged;
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
            await DisplayAlertAsync("Sipariş", "Sayfa yüklenirken bir sorun oluştu. Tekrar deneyin.", "Tamam");
        }
    }

    private async void DealerVmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OrderViewModel.HasHistoryDetail))
            return;
        if (!_dealerVm.HasHistoryDetail)
            return;

        try
        {
            // Detay yüklendiğinde kullanıcıyı otomatik olarak detay kartına götür.
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (HistoryScroll is null || HistoryDetailCard is null)
                    return;
                await HistoryScroll.ScrollToAsync(HistoryDetailCard, ScrollToPosition.Start, animated: true);
            });
        }
        catch
        {
            // UX iyileştirmesi; başarısız olursa sessiz geç.
        }
    }
}
