using B2B.Api.Contracts;
using B2B.Api.Infrastructure;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly B2BDbContext _db;

    public ProductsController(B2BDbContext db)
    {
        _db = db;
    }

    public sealed record CreateProductRequest(
        Guid? SellerUserId,
        Guid? CategoryId,
        string Sku,
        string Name,
        string? Description,
        string CurrencyCode,
        decimal DealerPrice,
        decimal MsrpPrice,
        int StockQuantity,
        IReadOnlyList<ProductImageInput>? Images,
        IReadOnlyList<ProductSpecInput>? Specs,
        bool IsActive = true
    );

    public sealed record UpdateProductRequest(
        string Sku,
        string Name,
        string? Description,
        Guid? CategoryId,
        string CurrencyCode,
        decimal DealerPrice,
        decimal MsrpPrice,
        int StockQuantity,
        IReadOnlyList<ProductImageInput>? Images,
        IReadOnlyList<ProductSpecInput>? Specs,
        bool IsActive
    );

    public sealed record ProductImageInput(string Url, int SortOrder, bool IsPrimary);
    public sealed record ProductSpecInput(string Key, string Value, int SortOrder);

    public sealed record ProductListItemDto(
        Guid ProductId,
        Guid SellerUserId,
        string SellerDisplayName,
        Guid? CategoryId,
        string? CategoryName,
        string? PrimaryImageUrl,
        string Sku,
        string Name,
        string CurrencyCode,
        decimal DealerPrice,
        decimal MsrpPrice,
        int StockQuantity,
        bool IsActive
    );

    public sealed record ProductImageDto(string Url, int SortOrder, bool IsPrimary);
    public sealed record ProductSpecDto(string Key, string Value, int SortOrder);

    public sealed record ProductDetailDto(
        Guid ProductId,
        Guid SellerUserId,
        string SellerDisplayName,
        Guid? CategoryId,
        string? CategoryName,
        IReadOnlyList<ProductImageDto> Images,
        IReadOnlyList<ProductSpecDto> Specs,
        string Sku,
        string Name,
        string? Description,
        string CurrencyCode,
        decimal DealerPrice,
        decimal MsrpPrice,
        int StockQuantity,
        bool IsActive
    );

    [HttpGet]
    /// <summary>List products (paginated).</summary>
    /// <remarks>
    /// Returns a lightweight, mobile-friendly product list (no large description fields, no navigation graphs).
    ///
    /// Example requests:
    /// <code>
    /// GET /api/v1/products?page=1&amp;pageSize=20
    /// GET /api/v1/products?sellerUserId={sellerId}&amp;isActive=true&amp;page=1&amp;pageSize=20
    /// GET /api/v1/products?q=bolt&amp;page=1&amp;pageSize=20
    /// </code>
    /// </remarks>
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductListItemDto>>>> List(
        [FromQuery] PageRequest page,
        [FromQuery] Guid? sellerUserId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? q,
        [FromQuery] bool? isActive,
        [FromQuery] int? minStock,
        [FromQuery] int? maxStock,
        [FromQuery] bool? uncategorized,
        CancellationToken ct)
    {
        page = page.Normalize();

        IQueryable<Product> query = _db.Products.AsNoTracking();

        if (sellerUserId.HasValue)
            query = query.Where(x => x.SellerUserId == sellerUserId.Value);

        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId.Value);

        if (uncategorized == true)
            query = query.Where(x => x.CategoryId == null);

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        if (minStock.HasValue)
            query = query.Where(x => x.StockQuantity >= minStock.Value);

        if (maxStock.HasValue)
            query = query.Where(x => x.StockQuantity <= maxStock.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q) || x.Sku.Contains(q));

        query = query.OrderBy(x => x.Name).ThenBy(x => x.ProductId);

        var total = await query.LongCountAsync(ct);

        var items = await query
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Join(
                _db.Users.AsNoTracking(),
                p => p.SellerUserId,
                u => u.UserId,
                (p, u) => new { p, u.DisplayName }
            )
            .Select(x => new
            {
                x.p,
                x.DisplayName,
                PrimaryImageUrl = _db.ProductImages.AsNoTracking()
                    .Where(i => i.ProductId == x.p.ProductId)
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault()
            })
            .Select(x => new ProductListItemDto(
                x.p.ProductId,
                x.p.SellerUserId,
                x.DisplayName ?? x.p.SellerUserId.ToString(),
                x.p.CategoryId,
                x.p.CategoryId == null
                    ? null
                    : _db.Categories.AsNoTracking()
                        .Where(c => c.CategoryId == x.p.CategoryId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                x.PrimaryImageUrl,
                x.p.Sku,
                x.p.Name,
                x.p.CurrencyCode,
                x.p.DealerPrice,
                x.p.MsrpPrice,
                x.p.StockQuantity,
                x.p.IsActive
            ))
            .ToListAsync(ct);

        var httpRequest = HttpContext.Request;
        var itemsWithPublicUrls = items
            .Select(d => d with
            {
                PrimaryImageUrl = PublicAssetUrlRewriter.RewriteForRequest(d.PrimaryImageUrl, httpRequest)
            })
            .ToList();

        var result = new PagedResult<ProductListItemDto>(
            itemsWithPublicUrls,
            new PageMeta(page.Page, page.PageSize, itemsWithPublicUrls.Count, total)
        );

        return Ok(ApiResponse<PagedResult<ProductListItemDto>>.Ok(result, HttpContext.TraceId()));
    }

    public sealed record UpdateStockRequest(int StockQuantity);

    [HttpPatch("{productId:guid}/stock")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStock(Guid productId, [FromBody] UpdateStockRequest req, CancellationToken ct)
    {
        if (req.StockQuantity < 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("invalid_stock", "StockQuantity must be >= 0.", null),
                HttpContext.TraceId()
            ));
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Product not found.", null),
                HttpContext.TraceId()
            ));
        }

        product.StockQuantity = req.StockQuantity;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { productId, product.StockQuantity }, HttpContext.TraceId()));
    }

    public sealed record UpdateActiveRequest(bool IsActive);

    [HttpPatch("{productId:guid}/active")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateActive(Guid productId, [FromBody] UpdateActiveRequest req, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Product not found.", null),
                HttpContext.TraceId()
            ));
        }

        product.IsActive = req.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { productId, product.IsActive }, HttpContext.TraceId()));
    }

    [HttpGet("{productId:guid}")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Get(Guid productId, CancellationToken ct)
    {
        var item = await _db.Products.AsNoTracking()
            .Where(x => x.ProductId == productId)
            .Join(
                _db.Users.AsNoTracking(),
                p => p.SellerUserId,
                u => u.UserId,
                (p, u) => new { p, u.DisplayName }
            )
            .Select(x => new ProductDetailDto(
                x.p.ProductId,
                x.p.SellerUserId,
                x.DisplayName ?? x.p.SellerUserId.ToString(),
                x.p.CategoryId,
                x.p.CategoryId == null
                    ? null
                    : _db.Categories.AsNoTracking()
                        .Where(c => c.CategoryId == x.p.CategoryId)
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                _db.ProductImages.AsNoTracking()
                    .Where(i => i.ProductId == x.p.ProductId)
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => new ProductImageDto(i.Url, i.SortOrder, i.IsPrimary))
                    .ToList(),
                _db.ProductSpecs.AsNoTracking()
                    .Where(s => s.ProductId == x.p.ProductId)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new ProductSpecDto(s.Key, s.Value, s.SortOrder))
                    .ToList(),
                x.p.Sku,
                x.p.Name,
                x.p.Description,
                x.p.CurrencyCode,
                x.p.DealerPrice,
                x.p.MsrpPrice,
                x.p.StockQuantity,
                x.p.IsActive
            ))
            .FirstOrDefaultAsync(ct);

        if (item is null)
        {
            return NotFound(ApiResponse<ProductDetailDto>.Fail(
                new ApiError("not_found", "Product not found.", null),
                HttpContext.TraceId()
            ));
        }

        return Ok(ApiResponse<ProductDetailDto>.Ok(WithPublicImageUrls(item), HttpContext.TraceId()));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Create([FromBody] CreateProductRequest req, CancellationToken ct)
    {
        var (userId, _) = GetCurrentUser();
        var sellerUserId = req.SellerUserId ?? userId;

        var normalizedSku = req.Sku.Trim();
        var skuExists = await _db.Products.AnyAsync(
            p => p.SellerUserId == sellerUserId && p.Sku == normalizedSku,
            ct);

        if (skuExists)
        {
            return Conflict(ApiResponse<object>.Fail(
                new ApiError("sku_taken", "SKU already exists for this seller.", null),
                HttpContext.TraceId()
            ));
        }

        // Ensure seller exists (basic guard)
        var sellerExists = await _db.Users.AnyAsync(u => u.UserId == sellerUserId && u.IsActive, ct);
        if (!sellerExists)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("invalid_seller", "Seller user not found or inactive.", null),
                HttpContext.TraceId()
            ));
        }

        if (req.CategoryId is { } cid)
        {
            var catOk = await _db.Categories.AnyAsync(c => c.CategoryId == cid && c.IsActive, ct);
            if (!catOk)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    new ApiError("invalid_category", "Category not found or inactive.", null),
                    HttpContext.TraceId()
                ));
            }
        }

        var entity = new Product
        {
            ProductId = Guid.NewGuid(),
            SellerUserId = sellerUserId,
            CategoryId = req.CategoryId,
            Sku = normalizedSku,
            Name = req.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            CurrencyCode = req.CurrencyCode.Trim().ToUpperInvariant(),
            DealerPrice = req.DealerPrice,
            MsrpPrice = req.MsrpPrice,
            StockQuantity = req.StockQuantity,
            IsActive = req.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            RowVer = Array.Empty<byte>()
        };

        _db.Products.Add(entity);

        var normalizedImages = NormalizeImages(req.Images);
        if (normalizedImages.Count > 0)
        {
            foreach (var img in normalizedImages)
            {
                _db.ProductImages.Add(new ProductImage
                {
                    ProductImageId = Guid.NewGuid(),
                    ProductId = entity.ProductId,
                    Url = img.Url,
                    SortOrder = img.SortOrder,
                    IsPrimary = img.IsPrimary
                });
            }
        }

        var normalizedSpecs = NormalizeSpecs(req.Specs);
        if (normalizedSpecs.Count > 0)
        {
            foreach (var spec in normalizedSpecs)
            {
                _db.ProductSpecs.Add(new ProductSpec
                {
                    ProductSpecId = Guid.NewGuid(),
                    ProductId = entity.ProductId,
                    Key = spec.Key,
                    Value = spec.Value,
                    SortOrder = spec.SortOrder
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var sellerDisplayName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == entity.SellerUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);

        var categoryName = entity.CategoryId is null
            ? null
            : await _db.Categories.AsNoTracking()
                .Where(c => c.CategoryId == entity.CategoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct);

        var dto = new ProductDetailDto(
            entity.ProductId,
            entity.SellerUserId,
            sellerDisplayName ?? entity.SellerUserId.ToString(),
            entity.CategoryId,
            categoryName,
            normalizedImages.Select(i => new ProductImageDto(i.Url, i.SortOrder, i.IsPrimary)).ToList(),
            normalizedSpecs.Select(s => new ProductSpecDto(s.Key, s.Value, s.SortOrder)).ToList(),
            entity.Sku,
            entity.Name,
            entity.Description,
            entity.CurrencyCode,
            entity.DealerPrice,
            entity.MsrpPrice,
            entity.StockQuantity,
            entity.IsActive
        );

        return CreatedAtAction(
            nameof(Get),
            new { productId = entity.ProductId },
            ApiResponse<ProductDetailDto>.Ok(WithPublicImageUrls(dto), HttpContext.TraceId()));
    }

    [HttpPut("{productId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Update(Guid productId, [FromBody] UpdateProductRequest req, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Product not found.", null),
                HttpContext.TraceId()
            ));
        }

        if (req.CategoryId is { } cidUpd)
        {
            var catOk = await _db.Categories.AnyAsync(c => c.CategoryId == cidUpd && c.IsActive, ct);
            if (!catOk)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    new ApiError("invalid_category", "Category not found or inactive.", null),
                    HttpContext.TraceId()
                ));
            }
        }

        var newSku = req.Sku.Trim();
        if (!string.Equals(product.Sku, newSku, StringComparison.Ordinal))
        {
            var skuExists = await _db.Products.AnyAsync(
                p => p.SellerUserId == product.SellerUserId && p.Sku == newSku && p.ProductId != productId,
                ct);

            if (skuExists)
            {
                return Conflict(ApiResponse<object>.Fail(
                    new ApiError("sku_taken", "SKU already exists for this seller.", null),
                    HttpContext.TraceId()
                ));
            }
        }

        product.Sku = newSku;
        product.Name = req.Name.Trim();
        product.CategoryId = req.CategoryId;
        product.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        product.CurrencyCode = req.CurrencyCode.Trim().ToUpperInvariant();
        product.DealerPrice = req.DealerPrice;
        product.MsrpPrice = req.MsrpPrice;
        product.StockQuantity = req.StockQuantity;
        product.IsActive = req.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        // Replace images/specs (simple admin-friendly model)
        var existingImages = await _db.ProductImages.Where(i => i.ProductId == product.ProductId).ToListAsync(ct);
        if (existingImages.Count > 0) _db.ProductImages.RemoveRange(existingImages);

        var normalizedImages = NormalizeImages(req.Images);
        if (normalizedImages.Count > 0)
        {
            foreach (var img in normalizedImages)
            {
                _db.ProductImages.Add(new ProductImage
                {
                    ProductImageId = Guid.NewGuid(),
                    ProductId = product.ProductId,
                    Url = img.Url,
                    SortOrder = img.SortOrder,
                    IsPrimary = img.IsPrimary
                });
            }
        }

        var existingSpecs = await _db.ProductSpecs.Where(s => s.ProductId == product.ProductId).ToListAsync(ct);
        if (existingSpecs.Count > 0) _db.ProductSpecs.RemoveRange(existingSpecs);

        var normalizedSpecs = NormalizeSpecs(req.Specs);
        if (normalizedSpecs.Count > 0)
        {
            foreach (var spec in normalizedSpecs)
            {
                _db.ProductSpecs.Add(new ProductSpec
                {
                    ProductSpecId = Guid.NewGuid(),
                    ProductId = product.ProductId,
                    Key = spec.Key,
                    Value = spec.Value,
                    SortOrder = spec.SortOrder
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var sellerDisplayName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == product.SellerUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);

        var categoryName = product.CategoryId is null
            ? null
            : await _db.Categories.AsNoTracking()
                .Where(c => c.CategoryId == product.CategoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct);

        var dto = new ProductDetailDto(
            product.ProductId,
            product.SellerUserId,
            sellerDisplayName ?? product.SellerUserId.ToString(),
            product.CategoryId,
            categoryName,
            await _db.ProductImages.AsNoTracking()
                .Where(i => i.ProductId == product.ProductId)
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .Select(i => new ProductImageDto(i.Url, i.SortOrder, i.IsPrimary))
                .ToListAsync(ct),
            await _db.ProductSpecs.AsNoTracking()
                .Where(s => s.ProductId == product.ProductId)
                .OrderBy(s => s.SortOrder)
                .Select(s => new ProductSpecDto(s.Key, s.Value, s.SortOrder))
                .ToListAsync(ct),
            product.Sku,
            product.Name,
            product.Description,
            product.CurrencyCode,
            product.DealerPrice,
            product.MsrpPrice,
            product.StockQuantity,
            product.IsActive
        );

        return Ok(ApiResponse<ProductDetailDto>.Ok(WithPublicImageUrls(dto), HttpContext.TraceId()));
    }

    [HttpPost("{productId:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(Guid productId, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Product not found.", null),
                HttpContext.TraceId()
            ));
        }

        if (product.IsActive)
        {
            product.IsActive = false;
            product.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(ApiResponse<object>.Ok(new { productId, isActive = product.IsActive }, HttpContext.TraceId()));
    }

    private ProductDetailDto WithPublicImageUrls(ProductDetailDto dto)
    {
        var req = HttpContext.Request;
        return dto with
        {
            Images = dto.Images
                .Select(i => i with { Url = PublicAssetUrlRewriter.RewriteForRequest(i.Url, req) ?? i.Url })
                .ToList()
        };
    }

    private (Guid userId, bool isAdmin) GetCurrentUser()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(raw, out var userId))
        {
            // If token is malformed, let authz handle it as forbidden/unauthorized.
            userId = Guid.Empty;
        }

        var isAdmin = User.IsInRole("Admin");
        return (userId, isAdmin);
    }

    private static List<ProductImageInput> NormalizeImages(IReadOnlyList<ProductImageInput>? images)
    {
        if (images is null || images.Count == 0) return new List<ProductImageInput>();

        var cleaned = images
            .Where(i => i is not null)
            .Select(i => new ProductImageInput(
                Url: (i.Url ?? "").Trim(),
                SortOrder: i.SortOrder,
                IsPrimary: i.IsPrimary
            ))
            .Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .Select((i, idx) => new { i, idx })
            .OrderBy(x => x.i.SortOrder)
            .ThenBy(x => x.idx)
            .Select(x => x.i)
            .ToList();

        if (cleaned.Count == 0) return cleaned;

        // Enforce single primary: keep the best candidate
        var primaryIndex = cleaned
            .Select((img, idx) => new { img, idx })
            .OrderByDescending(x => x.img.IsPrimary)
            .ThenBy(x => x.img.SortOrder)
            .ThenBy(x => x.idx)
            .First().idx;

        for (var i = 0; i < cleaned.Count; i++)
            cleaned[i] = cleaned[i] with { IsPrimary = i == primaryIndex };

        return cleaned;
    }

    private static List<ProductSpecInput> NormalizeSpecs(IReadOnlyList<ProductSpecInput>? specs)
    {
        if (specs is null || specs.Count == 0) return new List<ProductSpecInput>();

        return specs
            .Where(s => s is not null)
            .Select(s => new ProductSpecInput(
                Key: (s.Key ?? "").Trim(),
                Value: (s.Value ?? "").Trim(),
                SortOrder: s.SortOrder
            ))
            .Where(s => !string.IsNullOrWhiteSpace(s.Key) && !string.IsNullOrWhiteSpace(s.Value))
            .Select((s, idx) => new { s, idx })
            .OrderBy(x => x.s.SortOrder)
            .ThenBy(x => x.idx)
            .Select(x => x.s)
            .ToList();
    }
}

