using CommunityToolkit.Mvvm.ComponentModel;

namespace B2B.Mobile.Features.Products.Services;

/// <summary>Ürün listesi için seçili kategori filtresi (singleton).</summary>
public partial class ProductCatalogFilter : ObservableObject
{
    [ObservableProperty] private Guid? categoryId;
    [ObservableProperty] private string summary = "Tümü";
    /// <summary>True iken API <c>uncategorized=true</c> ile kategorisiz ürünler listelenir.</summary>
    [ObservableProperty] private bool uncategorizedOnly;

    public event EventHandler? Changed;

    public void SetAll()
    {
        CategoryId = null;
        UncategorizedOnly = false;
        Summary = "Tümü";
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCategory(Guid categoryId, string name)
    {
        CategoryId = categoryId;
        UncategorizedOnly = false;
        Summary = name;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetUncategorized()
    {
        CategoryId = null;
        UncategorizedOnly = true;
        Summary = "Kategorisiz";
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
