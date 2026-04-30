using B2B.Mobile.Features.Products.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace B2B.Mobile.Features.Products.Views;

public partial class ProductEditPage : ContentPage, IQueryAttributable
{
    private readonly ProductEditViewModel _vm;
    private bool _isSubscribed;

    public ProductEditPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ProductEditViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_isSubscribed) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _isSubscribed = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_isSubscribed) return;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _isSubscribed = false;
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProductEditViewModel.FocusField))
            return;

        var field = _vm.FocusField;
        if (string.IsNullOrWhiteSpace(field))
            return;

        VisualElement? target = field switch
        {
            "Sku" => SkuBorder,
            "Name" => NameBorder,
            "CurrencyCode" => CurrencyBorder,
            "StockQuantity" => StockBorder,
            "DealerPrice" => DealerBorder,
            "MsrpPrice" => MsrpBorder,
            _ => null
        };

        if (target is null)
            return;

        // Give layout a tick before scrolling/focusing.
        await Task.Delay(50);
        await RootScroll.ScrollToAsync(target, ScrollToPosition.Start, true);

        _ = field switch
        {
            "Sku" => SkuEntry.Focus(),
            "Name" => NameEntry.Focus(),
            "CurrencyCode" => CurrencyEntry.Focus(),
            "StockQuantity" => StockEntry.Focus(),
            "DealerPrice" => DealerEntry.Focus(),
            "MsrpPrice" => MsrpEntry.Focus(),
            _ => false
        };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var hasProductIdKey = query.ContainsKey("productId");
        var hasScannedKey = query.ContainsKey("scanned");

        if (hasProductIdKey)
        {
            string? id = null;
            if (query.TryGetValue("productId", out var raw))
            {
                id = raw switch
                {
                    string s when !string.IsNullOrWhiteSpace(s) => s,
                    Guid g => g.ToString("D"),
                    _ => raw?.ToString()
                };
                if (string.IsNullOrWhiteSpace(id))
                    id = null;
            }

            _vm.ProductId = id;
            _ = _vm.LoadCommand.ExecuteAsync(null);
        }
        else if (!hasScannedKey)
        {
            _vm.ProductId = null;
            _ = _vm.LoadCommand.ExecuteAsync(null);
        }

        if (hasScannedKey &&
            query.TryGetValue("scanned", out var scanRaw) &&
            scanRaw is string code &&
            !string.IsNullOrWhiteSpace(code))
        {
            _vm.Sku = code.Trim();
        }
    }
}

