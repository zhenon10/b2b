using B2B.Mobile.Core.Api;
using B2B.Mobile.Features.Products.Models;

namespace B2B.Mobile.Features.Products.Services;

public sealed class CategoriesService
{
    private readonly ApiClient _api;

    public CategoriesService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResponse<List<CategoryListItem>>> GetCategoriesAsync(bool includeInactive, CancellationToken ct)
    {
        var url = $"/api/v1/categories?includeInactive={includeInactive.ToString().ToLowerInvariant()}";
        return _api.GetAsync<List<CategoryListItem>>(url, ct);
    }

    public sealed record CreateCategoryRequest(string Name, int SortOrder = 0, bool IsActive = true);

    public sealed record UpdateCategoryRequest(string Name, int SortOrder, bool IsActive);

    public Task<ApiResponse<CategoryListItem>> CreateCategoryAsync(CreateCategoryRequest body, CancellationToken ct) =>
        _api.PostAsync<CreateCategoryRequest, CategoryListItem>("/api/v1/categories", body, ct);

    public Task<ApiResponse<CategoryListItem>> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest body, CancellationToken ct) =>
        _api.PutAsync<UpdateCategoryRequest, CategoryListItem>($"/api/v1/categories/{categoryId}", body, ct);

    public Task<ApiResponse<object>> DeleteCategoryAsync(Guid categoryId, CancellationToken ct) =>
        _api.DeleteAsync<object>($"/api/v1/categories/{categoryId}", ct);
}
