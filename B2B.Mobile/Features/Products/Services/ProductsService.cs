using B2B.Contracts;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Features.Products.Services;

public sealed class ProductsService
{
    private readonly ApiClient _api;

    public ProductsService(ApiClient api)
    {
        _api = api;
    }

    public Task<ApiResponse<PagedResult<ProductListItem>>> GetProductsAsync(
        int page,
        int pageSize,
        Guid? sellerUserId,
        string? q,
        bool? isActive,
        Guid? categoryId,
        bool? uncategorized,
        CancellationToken ct)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (sellerUserId.HasValue) query.Add($"sellerUserId={sellerUserId}");
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        if (isActive.HasValue) query.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");
        if (categoryId.HasValue) query.Add($"categoryId={categoryId.Value}");
        if (uncategorized == true) query.Add("uncategorized=true");

        var url = "/api/v1/products?" + string.Join("&", query);
        return ApiTransientRetry.ExecuteAsync(() => _api.GetAsync<PagedResult<ProductListItem>>(url, ct), ct);
    }

    public Task<ApiResponse<ProductDetail>> GetProductAsync(Guid productId, CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(
            () => _api.GetAsync<ProductDetail>($"/api/v1/products/{productId}", ct),
            ct);

    public Task<ApiResponse<ProductDetail>> CreateProductAsync(CreateProductRequest req, CancellationToken ct) =>
        _api.PostAsync<CreateProductRequest, ProductDetail>("/api/v1/products", req, ct);

    public Task<ApiResponse<ProductDetail>> UpdateProductAsync(Guid productId, UpdateProductRequest req, CancellationToken ct) =>
        _api.PutAsync<UpdateProductRequest, ProductDetail>($"/api/v1/products/{productId}", req, ct);

    /// <summary>Admin: ürünü pasifleştirir (katalogdan kaldırır).</summary>
    public Task<ApiResponse<object>> DeactivateProductAsync(Guid productId, CancellationToken ct) =>
        _api.PostAsync<object>($"/api/v1/products/{productId}/deactivate", ct);
}
