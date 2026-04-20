using System.Net.Http.Headers;
using System.Net.Http.Json;
using B2B.Contracts;
using B2B.Domain.Enums;

namespace B2B.Admin.Services;

public sealed class B2BApiClient
{
    private readonly HttpClient _http;
    private readonly AuthSession _session;

    public B2BApiClient(HttpClient http, AuthSession session)
    {
        _http = http;
        _session = session;
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password), ct);
            var payload = await resp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(cancellationToken: ct)
                          ?? new ApiResponse<AuthResponse>(false, null, new ApiError("invalid_response", "Empty response."), "");

            if (payload.Success && payload.Data is not null)
            {
                await _session.SetAccessTokenAsync(payload.Data.AccessToken);
                await _session.SetRefreshTokenAsync(payload.Data.RefreshToken);
            }

            return payload;
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<AuthResponse>(
                false,
                null,
                new ApiError("network_error", $"Failed to reach API at {baseUrl}. {ex.Message}"),
                ""
            );
        }
    }

    public async Task<ApiResponse<PagedResult<ProductListItem>>> GetProductsAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItem>>>($"/api/v1/products?page={page}&pageSize={pageSize}", ct)
               ?? new ApiResponse<PagedResult<ProductListItem>>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<PagedResult<ProductListItem>>> GetProductsAsync(
        int page,
        int pageSize,
        string? q,
        bool? isActive,
        int? minStock,
        int? maxStock,
        Guid? categoryId,
        bool? uncategorized,
        CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        if (isActive.HasValue) query.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");
        if (minStock.HasValue) query.Add($"minStock={minStock.Value}");
        if (maxStock.HasValue) query.Add($"maxStock={maxStock.Value}");
        if (categoryId.HasValue) query.Add($"categoryId={categoryId.Value}");
        if (uncategorized == true) query.Add("uncategorized=true");

        var url = "/api/v1/products?" + string.Join("&", query);
        return await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItem>>>(url, ct)
               ?? new ApiResponse<PagedResult<ProductListItem>>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<List<CategoryListItem>>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var url = $"/api/v1/categories?includeInactive={includeInactive.ToString().ToLowerInvariant()}";
        return await _http.GetFromJsonAsync<ApiResponse<List<CategoryListItem>>>(url, ct)
               ?? new ApiResponse<List<CategoryListItem>>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<CategoryListItem>> CreateCategoryAsync(CreateCategoryRequest req, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PostAsJsonAsync("/api/v1/categories", req, ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<CategoryListItem>>(cancellationToken: ct)
               ?? new ApiResponse<CategoryListItem>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<CategoryListItem>> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest req, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PutAsJsonAsync($"/api/v1/categories/{categoryId}", req, ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<CategoryListItem>>(cancellationToken: ct)
               ?? new ApiResponse<CategoryListItem>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<object>> DeleteCategoryAsync(Guid categoryId, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.DeleteAsync($"/api/v1/categories/{categoryId}", ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: ct)
               ?? new ApiResponse<object>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<object>> UpdateProductStockAsync(Guid productId, int stockQuantity, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PatchAsJsonAsync($"/api/v1/products/{productId}/stock", new UpdateStockRequest(stockQuantity), ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: ct)
               ?? new ApiResponse<object>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<object>> UpdateProductActiveAsync(Guid productId, bool isActive, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PatchAsJsonAsync($"/api/v1/products/{productId}/active", new UpdateActiveRequest(isActive), ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: ct)
               ?? new ApiResponse<object>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<ProductDetail>> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        return await _http.GetFromJsonAsync<ApiResponse<ProductDetail>>($"/api/v1/products/{productId}", ct)
               ?? new ApiResponse<ProductDetail>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<ProductDetail>> CreateProductAsync(CreateProductRequest req, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PostAsJsonAsync("/api/v1/products", req, ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<ProductDetail>>(cancellationToken: ct)
               ?? new ApiResponse<ProductDetail>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<ProductDetail>> UpdateProductAsync(Guid productId, UpdateProductRequest req, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PutAsJsonAsync($"/api/v1/products/{productId}", req, ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<ProductDetail>>(cancellationToken: ct)
               ?? new ApiResponse<ProductDetail>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<IReadOnlyList<PendingDealerDto>>> GetPendingDealersAsync(CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        return await _http.GetFromJsonAsync<ApiResponse<IReadOnlyList<PendingDealerDto>>>("/api/v1/admin/users/pending-dealers", ct)
               ?? new ApiResponse<IReadOnlyList<PendingDealerDto>>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<object>> ApproveDealerAsync(Guid userId, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{userId}/approve");
        req.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var resp = await _http.SendAsync(req, ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: ct)
               ?? new ApiResponse<object>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<PagedResult<AdminOrderListItem>>> GetAdminOrdersAsync(
        int page = 1,
        int pageSize = 20,
        int? status = null,
        CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var url = $"/api/v1/admin/orders?page={page}&pageSize={pageSize}";
        if (status.HasValue)
            url += $"&status={status.Value}";
        return await _http.GetFromJsonAsync<ApiResponse<PagedResult<AdminOrderListItem>>>(url, ct)
               ?? new ApiResponse<PagedResult<AdminOrderListItem>>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<AdminOrderDetail>> GetAdminOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        return await _http.GetFromJsonAsync<ApiResponse<AdminOrderDetail>>($"/api/v1/admin/orders/{orderId}", ct)
               ?? new ApiResponse<AdminOrderDetail>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<object>> UpdateOrderStatusAsync(Guid orderId, int status, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PatchAsJsonAsync(
            $"/api/v1/orders/{orderId}/status",
            new UpdateOrderStatusRequest((OrderStatus)status),
            ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: ct)
               ?? new ApiResponse<object>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    public async Task<ApiResponse<ReconcileProductImagesResponse>> ReconcileProductImagesAsync(bool dryRun = true, CancellationToken ct = default)
    {
        await ApplyAuthAsync();
        var resp = await _http.PostAsync($"/api/v1/maintenance/product-images/reconcile?dryRun={dryRun.ToString().ToLowerInvariant()}", content: null, ct);
        return await resp.Content.ReadFromJsonAsync<ApiResponse<ReconcileProductImagesResponse>>(cancellationToken: ct)
               ?? new ApiResponse<ReconcileProductImagesResponse>(false, null, new ApiError("invalid_response", "Empty response."), "");
    }

    private async Task ApplyAuthAsync()
    {
        var token = await _session.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = null;
            return;
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
