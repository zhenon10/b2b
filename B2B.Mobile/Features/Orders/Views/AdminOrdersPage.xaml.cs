using B2B.Mobile.Features.Orders.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Orders.Views;

public partial class AdminOrdersPage : ContentPage
{
    private static readonly TimeSpan ListStaleAfter = TimeSpan.FromMinutes(2);

    private readonly AdminOrdersViewModel _vm;

    public AdminOrdersPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AdminOrdersViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.EnsureFreshListAsync(ListStaleAfter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Sipariş onayları", "Sayfa yüklenirken bir sorun oluştu. Tekrar deneyin.", "Tamam");
        }
    }
}
