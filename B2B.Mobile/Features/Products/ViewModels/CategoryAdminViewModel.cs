using System.Collections.ObjectModel;
using B2B.Mobile.Core;
using B2B.Mobile.Features.Products.Models;
using B2B.Mobile.Features.Products.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Products.ViewModels;

public partial class CategoryAdminViewModel : ObservableObject
{
    private readonly CategoriesService _categories;
    private readonly CatalogNotifications _catalogEvents;

    public ObservableCollection<CategoryListItem> Items { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;

    public CategoryAdminViewModel(CategoriesService categories, CatalogNotifications catalogEvents)
    {
        _categories = categories;
        _catalogEvents = catalogEvents;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var resp = await _categories.GetCategoriesAsync(includeInactive: true, CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = resp.Error?.Message ?? "Kategoriler yüklenemedi.";
                return;
            }

            Items.Clear();
            foreach (var c in resp.Data.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
                Items.Add(c);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task AddAsync() =>
        Shell.Current.GoToAsync("categoryEdit", new Dictionary<string, object> { ["categoryId"] = "" });

    [RelayCommand]
    private Task EditAsync(CategoryListItem item) =>
        Shell.Current.GoToAsync("categoryEdit", new Dictionary<string, object>
        {
            ["categoryId"] = item.CategoryId.ToString()
        });

    [RelayCommand]
    private async Task DeleteAsync(CategoryListItem item)
    {
        if (IsBusy) return;
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Sil",
            $"“{item.Name}” silinsin mi? Bu kategorideki ürünler kategorisiz kalır.",
            "Sil",
            "İptal");
        if (!confirm) return;

        IsBusy = true;
        Error = null;
        try
        {
            var resp = await _categories.DeleteCategoryAsync(item.CategoryId, CancellationToken.None);
            if (!resp.Success)
            {
                Error = resp.Error?.Message ?? "Silinemedi.";
                return;
            }

            _catalogEvents.NotifyCategoriesChanged();
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
