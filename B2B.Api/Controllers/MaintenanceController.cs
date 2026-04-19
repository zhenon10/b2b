using B2B.Api.Contracts;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/maintenance")]
[Authorize(Roles = "Admin")]
public sealed class MaintenanceController : ControllerBase
{
    private readonly B2BDbContext _db;
    private readonly IWebHostEnvironment _env;

    public MaintenanceController(B2BDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpPost("product-images/reconcile")]
    [ProducesResponseType(typeof(ApiResponse<ReconcileProductImagesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReconcileProductImagesResponse>>> ReconcileProductImages(
        [FromQuery] bool dryRun = true,
        CancellationToken ct = default)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var uploadsRoot = Path.Combine(webRoot, "uploads", "products");

        // Only reconcile images that look like our own uploads path.
        // If Url is external (cdn, etc.), we won't touch it.
        var images = await _db.ProductImages
            .AsNoTracking()
            .Select(x => new { x.ProductImageId, x.ProductId, x.Url })
            .ToListAsync(ct);

        var broken = new List<BrokenProductImage>();
        foreach (var img in images)
        {
            if (!TryGetUploadsFileName(img.Url, out var fileName))
                continue;

            var absPath = Path.Combine(uploadsRoot, fileName);
            if (!System.IO.File.Exists(absPath))
            {
                broken.Add(new BrokenProductImage(img.ProductImageId, img.ProductId, img.Url));
            }
        }

        if (!dryRun && broken.Count > 0)
        {
            var ids = broken.Select(x => x.ProductImageId).ToList();
            // Use ExecuteDeleteAsync for efficiency; fall back to tracked delete if unavailable.
            await _db.ProductImages
                .Where(x => ids.Contains(x.ProductImageId))
                .ExecuteDeleteAsync(ct);
        }

        var resp = new ReconcileProductImagesResponse(
            DryRun: dryRun,
            WebRoot: webRoot,
            UploadsRoot: uploadsRoot,
            TotalImages: images.Count,
            BrokenCount: broken.Count,
            DeletedCount: dryRun ? 0 : broken.Count,
            Broken: broken
        );

        return Ok(ApiResponse<ReconcileProductImagesResponse>.Ok(resp, HttpContext.TraceIdentifier));
    }

    private static bool TryGetUploadsFileName(string? url, out string fileName)
    {
        fileName = "";
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Accept: http(s)://host/uploads/products/<file>
        // Accept: /uploads/products/<file>
        // Reject: anything else (external urls, different paths)
        var marker = "/uploads/products/";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        var tail = url[(idx + marker.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(tail)) return false;

        // strip querystring if present
        var q = tail.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0) tail = tail[..q];

        // stop at next slash (shouldn't happen, but be safe)
        var slash = tail.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0) tail = tail[..slash];

        // basic filename safety: no path traversal
        tail = tail.Replace("\\", "").Replace("/", "");
        if (tail.Contains("..", StringComparison.Ordinal)) return false;

        fileName = tail;
        return true;
    }

    public sealed record BrokenProductImage(Guid ProductImageId, Guid ProductId, string Url);

    public sealed record ReconcileProductImagesResponse(
        bool DryRun,
        string WebRoot,
        string UploadsRoot,
        int TotalImages,
        int BrokenCount,
        int DeletedCount,
        IReadOnlyList<BrokenProductImage> Broken
    );
}

