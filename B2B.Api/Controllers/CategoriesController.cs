using B2B.Api.Infrastructure;
using B2B.Contracts;
using B2B.Api.Security;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly B2BDbContext _db;

    public CategoriesController(B2BDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [EnableRateLimiting("read")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryListItem>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryListItem>>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var q = _db.Categories.AsNoTracking();
        if (!includeInactive)
            q = q.Where(c => c.IsActive);

        var items = await q
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryListItem(c.CategoryId, c.Name, c.SortOrder, c.IsActive))
            .ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<CategoryListItem>>.Ok(items, HttpContext.TraceId()));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<CategoryListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CategoryListItem>>> Create([FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        var exists = await _db.Categories.AnyAsync(c => c.Name == name, ct);
        if (exists)
        {
            return Conflict(ApiResponse<CategoryListItem>.Fail(
                new ApiError("name_taken", "Bu isimde bir kategori zaten var.", null),
                HttpContext.TraceId()));
        }

        var entity = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = name,
            SortOrder = req.SortOrder,
            IsActive = req.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = new CategoryListItem(entity.CategoryId, entity.Name, entity.SortOrder, entity.IsActive);
        return Ok(ApiResponse<CategoryListItem>.Ok(dto, HttpContext.TraceId()));
    }

    [HttpPut("{categoryId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<CategoryListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CategoryListItem>>> Update(
        Guid categoryId,
        [FromBody] UpdateCategoryRequest req,
        CancellationToken ct = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);
        if (entity is null)
        {
            return NotFound(ApiResponse<CategoryListItem>.Fail(
                new ApiError("not_found", "Kategori bulunamadı.", null),
                HttpContext.TraceId()));
        }

        var name = req.Name.Trim();
        var nameTaken = await _db.Categories.AnyAsync(c => c.CategoryId != categoryId && c.Name == name, ct);
        if (nameTaken)
        {
            return Conflict(ApiResponse<CategoryListItem>.Fail(
                new ApiError("name_taken", "Bu isimde bir kategori zaten var.", null),
                HttpContext.TraceId()));
        }

        entity.Name = name;
        entity.SortOrder = req.SortOrder;
        entity.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);

        var dto = new CategoryListItem(entity.CategoryId, entity.Name, entity.SortOrder, entity.IsActive);
        return Ok(ApiResponse<CategoryListItem>.Ok(dto, HttpContext.TraceId()));
    }

    [HttpDelete("{categoryId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid categoryId, CancellationToken ct)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == categoryId, ct);
        if (entity is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Kategori bulunamadı.", null),
                HttpContext.TraceId()));
        }

        // FK Product.CategoryId uses SetNull — ürünler kategorisiz kalır
        _db.Categories.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { categoryId }, HttpContext.TraceId()));
    }
}
