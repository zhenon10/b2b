using System.Collections.ObjectModel;
using B2B.Mobile.Core;
using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Products.ViewModels;

public partial class CategoriesFlyoutViewModel : ObservableObject
{
    /// <summary>Tüm ürünler; gerçek kategori kimliği olamaz.</summary>
    private static readonly Guid AllCategoriesId = Guid.Empty;

    private readonly CategoriesService _categories;
    private readonly ProductCatalogFilter _filter;
    private readonly CatalogNotifications _catalogEvents;

    public ObservableCollection<CategoryListItem> Categories { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    public CategoriesFlyoutViewModel(
        CategoriesService categories,
        ProductCatalogFilter filter,
        CatalogNotifications catalogEvents)
    {
        _categories = categories;
        _filter = filter;
        _catalogEvents = catalogEvents;

        _catalogEvents.SessionChanged += OnSessionOrCategoriesChanged;
        _catalogEvents.CategoriesChanged += OnSessionOrCategoriesChanged;

        _ = LoadCommand.ExecuteAsync(null);
    }

    private void OnSessionOrCategoriesChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => _ = LoadCommand.ExecuteAsync(null));

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        try
        {
            var resp = await _categories.GetCategoriesAsync(includeInactive: false, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Kategoriler yüklenemedi.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            Categories.Clear();
            Categories.Add(new CategoryListItem(AllCategoriesId, "Tümü", -1, true));
            foreach (var c in resp.Data.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
                Categories.Add(c);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectCategoryAsync(CategoryListItem? item)
    {
        if (item is null) return;

        Shell.Current.FlyoutIsPresented = false;
        // Filtre değişimi ProductsPage.OnAppearing içindeki Changed aboneliğinden sonra tetiklenmeli;
        // aksi halde başka sekmedeyken kategori seçilirse olay kaçırılır ve liste güncellenmez.
        await Shell.Current.GoToAsync("//main/products", animate: false);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (item.CategoryId == AllCategoriesId)
                _filter.SetAll();
            else
                _filter.SetCategory(item.CategoryId, item.Name);
        });
    }

    [RelayCommand]
    private async Task SelectUncategorizedAsync()
    {
        Shell.Current.FlyoutIsPresented = false;
        await Shell.Current.GoToAsync("//main/products", animate: false);
        await MainThread.InvokeOnMainThreadAsync(() => _filter.SetUncategorized());
    }
}
