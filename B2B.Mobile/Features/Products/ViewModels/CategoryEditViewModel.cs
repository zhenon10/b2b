using B2B.Mobile.Core;
using B2B.Mobile.Features.Products.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Products.ViewModels;

public partial class CategoryEditViewModel : ObservableObject
{
    private readonly CategoriesService _categories;
    private readonly CatalogNotifications _catalogEvents;

    [ObservableProperty] private string? categoryIdQuery;
    [ObservableProperty] private string pageTitle = "Yeni kategori";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private int sortOrder;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;

    public CategoryEditViewModel(CategoriesService categories, CatalogNotifications catalogEvents)
    {
        _categories = categories;
        _catalogEvents = catalogEvents;
    }

    partial void OnCategoryIdQueryChanged(string? value) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(CategoryIdQuery) || !Guid.TryParse(CategoryIdQuery, out var id))
        {
            PageTitle = "Yeni kategori";
            Name = string.Empty;
            SortOrder = 0;
            IsActive = true;
            return;
        }

        PageTitle = "Kategori düzenle";
        IsBusy = true;
        try
        {
            var resp = await _categories.GetCategoriesAsync(includeInactive: true, CancellationToken.None);
            var item = resp.Data?.FirstOrDefault(x => x.CategoryId == id);
            if (item is null)
            {
                Error = resp.Success ? "Kategori bulunamadı." : (resp.Error?.Message ?? "Yüklenemedi.");
                return;
            }

            Name = item.Name;
            SortOrder = item.SortOrder;
            IsActive = item.IsActive;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        var trimmed = Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            Error = "Kategori adı gerekli.";
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(CategoryIdQuery) || !Guid.TryParse(CategoryIdQuery, out var id))
            {
                var resp = await _categories.CreateCategoryAsync(
                    new CategoriesService.CreateCategoryRequest(trimmed, SortOrder, IsActive),
                    CancellationToken.None);
                if (!resp.Success)
                {
                    Error = resp.Error?.Message ?? "Kaydedilemedi.";
                    return;
                }
            }
            else
            {
                var resp = await _categories.UpdateCategoryAsync(
                    id,
                    new CategoriesService.UpdateCategoryRequest(trimmed, SortOrder, IsActive),
                    CancellationToken.None);
                if (!resp.Success)
                {
                    Error = resp.Error?.Message ?? "Kaydedilemedi.";
                    return;
                }
            }

            _catalogEvents.NotifyCategoriesChanged();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
