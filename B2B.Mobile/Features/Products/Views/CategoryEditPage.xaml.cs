using B2B.Mobile.Features.Products.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Features.Products.Views;

public partial class CategoryEditPage : ContentPage, IQueryAttributable
{
    private readonly CategoryEditViewModel _vm;

    public CategoryEditPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<CategoryEditViewModel>();
        BindingContext = _vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoryId", out var raw) && raw is string s && !string.IsNullOrWhiteSpace(s))
            _vm.CategoryIdQuery = s;
        else
            _vm.CategoryIdQuery = null;
    }
}
