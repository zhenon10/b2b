using B2B.Mobile.Features.Products.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Products.Views;

public partial class ProductDetailPage : ContentPage
{
    private readonly ProductDetailViewModel _vm;
    private readonly ToolbarItem _editToolbar;

    public ProductDetailPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ProductDetailViewModel>();
        BindingContext = _vm;

        _editToolbar = new ToolbarItem
        {
            Text = "Düzenle",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            Command = _vm.EditCommand
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProductDetailViewModel.CanManageProducts))
                UpdateToolbar();
        };

        UpdateToolbar();
    }

    private void UpdateToolbar()
    {
        if (_vm.CanManageProducts)
        {
            if (!ToolbarItems.Contains(_editToolbar))
                ToolbarItems.Add(_editToolbar);
        }
        else
        {
            if (ToolbarItems.Contains(_editToolbar))
                ToolbarItems.Remove(_editToolbar);
        }
    }
}

