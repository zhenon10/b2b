using B2B.Contracts;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;
using B2B.Mobile.Features.Products.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Products.Views;

public partial class ProductsPage : ContentPage, IQueryAttributable
{
    private readonly ProductsViewModel _vm;
    private readonly ToolbarItem _layoutToolbar;
    private readonly ProductCatalogFilter _catalogFilter;
    private readonly ProductScanReturnBuffer _scanReturnBuffer;

    public ProductsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ProductsViewModel>();
        _catalogFilter = App.Services.GetRequiredService<ProductCatalogFilter>();
        _scanReturnBuffer = App.Services.GetRequiredService<ProductScanReturnBuffer>();
        BindingContext = _vm;

        _layoutToolbar = new ToolbarItem
        {
            Text = "Görünüm",
            Order = ToolbarItemOrder.Primary,
            Priority = 1,
            Command = _vm.ToggleCatalogLayoutCommand
        };

        _vm.PropertyChanged += OnVmPropertyChanged;

        ToolbarItems.Add(_layoutToolbar);
        UpdateLayoutToggleIcon();
        ApplyCatalogLayout();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProductsViewModel.CatalogViewMode) or nameof(ProductsViewModel.CatalogColumns) or nameof(ProductsViewModel.IsNoPhotoListMode))
        {
            ApplyCatalogLayout();
            UpdateLayoutToggleIcon();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _catalogFilter.Changed += OnCatalogFilterChanged;
        _vm.NotifyCategoryFilterChanged();
        await _vm.RefreshRolesAsync();
        ApplyCatalogLayout();
        UpdateLayoutToggleIcon();

        var pendingScan = _scanReturnBuffer.GetAndClearPendingCode();
        if (!string.IsNullOrWhiteSpace(pendingScan))
        {
            await _vm.ApplyScannedCodeAsync(pendingScan);
            return;
        }

        if (_vm.Items.Count == 0)
            await _vm.RefreshCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        _catalogFilter.Changed -= OnCatalogFilterChanged;
        base.OnDisappearing();
    }

    private void OnCatalogFilterChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _vm.NotifyCategoryFilterChanged();
            await _vm.RefreshCommand.ExecuteAsync(null);
        });
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProductListItem item)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        await _vm.OpenCommand.ExecuteAsync(item);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("refreshProducts", out var refreshRaw) && refreshRaw is bool refresh && refresh)
        {
            MainThread.BeginInvokeOnMainThread(async () => await _vm.RefreshCommand.ExecuteAsync(null));
        }
    }

    private void ApplyCatalogLayout()
    {
        if (_vm.CatalogColumns == 1)
        {
            ProductsCollection.ItemsLayout = LinearItemsLayout.Vertical;
        }
        else
        {
            ProductsCollection.ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical)
            {
                HorizontalItemSpacing = 10,
                VerticalItemSpacing = 10
            };
        }
    }

    /// <summary>3 mod döngüsü için ikon + kısa metin.</summary>
    private void UpdateLayoutToggleIcon()
    {
        // Icon represents the *next* mode (tap action).
        // 0(list+photo)->1(grid)->2(list no photo)->0...
        var next = _vm.CatalogViewMode switch { 0 => 1, 1 => 2, _ => 0 };
        var glyph = next switch
        {
            1 => "\uE8F0", // grid
            2 => "\uE8EF", // list
            _ => "\uE8F0"  // back to photo list -> show grid icon again
        };

        _layoutToolbar.Text = _vm.CatalogViewMode switch
        {
            0 => "1x",
            1 => "2x",
            _ => "Liste"
        };
        _layoutToolbar.IconImageSource = new FontImageSource
        {
            FontFamily = "MaterialIcons",
            Glyph = glyph,
            Size = 22,
            Color = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#AC99EA")
                : Color.FromArgb("#512BD4")
        };
    }
}
