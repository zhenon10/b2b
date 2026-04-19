using B2B.Mobile.Features.Cart.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Cart.Views;

public partial class CartPage : ContentPage
{
    private readonly CartViewModel _vm;

    public CartPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<CartViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Sync();
    }
}

