using B2B.Mobile.Features.Products.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Products.Views;

public partial class ProductEditPage : ContentPage, IQueryAttributable
{
    private readonly ProductEditViewModel _vm;

    public ProductEditPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ProductEditViewModel>();
        BindingContext = _vm;
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

