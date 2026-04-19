using B2B.Mobile.Features.Products.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Products.Views;

public partial class CategoryAdminPage : ContentPage
{
    private readonly CategoryAdminViewModel _vm;

    public CategoryAdminPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<CategoryAdminViewModel>();
        BindingContext = _vm;

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "+",
            Order = ToolbarItemOrder.Primary,
            Command = _vm.AddCommand
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshCommand.ExecuteAsync(null);
    }
}
