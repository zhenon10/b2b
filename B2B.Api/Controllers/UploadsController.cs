using B2B.Contracts;
using B2B.Api.Infrastructure;
using B2B.Api.Security;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using System.Security.Claims;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
public sealed class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<ApiPublishingOptions> _apiPublish;
    private readonly IOptions<UploadLimitsOptions> _limits;
    private readonly B2BDbContext _db;
    private readonly ILogger<UploadsController> _logger;

    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private const int MaxWidth = 4096;
    private const int MaxHeight = 4096;
    private const long MaxPixels = (long)MaxWidth * MaxHeight;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public UploadsController(
        IWebHostEnvironment env,
        IOptions<ApiPublishingOptions> apiPublish,
        IOptions<UploadLimitsOptions> limits,
        B2BDbContext db,
        ILogger<UploadsController> logger)
    {
        _env = env;
        _apiPublish = apiPublish;
        _limits = limits;
        _db = db;
        _logger = logger;
    }

    [HttpPost("images")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(ApiResponse<UploadImageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UploadImageResponse>>> UploadImage([FromForm] IFormFile file, CancellationToken ct)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized(ApiResponse<UploadImageResponse>.Fail(
                new ApiError("unauthorized", "Missing user identity.", null),
                HttpContext.TraceIdentifier
            ));
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UploadImageResponse>.Fail(
                new ApiError("invalid_file", "File is required.", null),
                HttpContext.TraceIdentifier
            ));
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(ApiResponse<UploadImageResponse>.Fail(
                new ApiError("file_too_large", $"Max upload size is {MaxUploadBytes / (1024 * 1024)} MB.", null),
                HttpContext.TraceIdentifier
            ));
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<UploadImageResponse>.Fail(
                new ApiError("invalid_file_type", "Only .jpg, .jpeg, .png, .webp are allowed.", null),
                HttpContext.TraceIdentifier
            ));
        }

        // Decode & validate image content (not just extension), enforce dimensions, and re-encode to strip any payload/metadata.
        Image image;
        await using (var input = file.OpenReadStream())
        {
            try
            {
                image = await Image.LoadAsync(input, ct);
            }
            catch
            {
                return BadRequest(ApiResponse<UploadImageResponse>.Fail(
                    new ApiError("invalid_image", "File could not be decoded as a supported image.", null),
                    HttpContext.TraceIdentifier
                ));
            }
        }

        string safeName;
        string absPath;
        var now = DateTime.UtcNow;
        var todayUtc = now.Date;
        var dailyMaxCount = Math.Max(0, _limits.Value.DailyMaxCount);
        var dailyMaxBytes = Math.Max(0, _limits.Value.DailyMaxBytes);
        var width = 0;
        var height = 0;

        using (image)
        {
            // Per-user daily quota (count + bytes) based on audit table.
            // Uses UTC day boundary for deterministic behavior across servers.
            if (dailyMaxCount > 0 || dailyMaxBytes > 0)
            {
                var q = _db.UploadAudits.AsNoTracking()
                    .Where(x => x.UserId == userId && x.CreatedAtUtc >= todayUtc && x.Kind == "product_image");

                var agg = await q
                    .GroupBy(_ => 1)
                    .Select(g => new { Count = g.Count(), Bytes = g.Sum(x => x.FileSizeBytes) })
                    .FirstOrDefaultAsync(ct);

                var usedCount = agg?.Count ?? 0;
                var usedBytes = agg?.Bytes ?? 0;

                if (dailyMaxCount > 0 && usedCount >= dailyMaxCount)
                {
                    var details = new Dictionary<string, string[]>
                    {
                        ["limitDailyCount"] = [dailyMaxCount.ToString()],
                        ["usedDailyCount"] = [usedCount.ToString()]
                    };
                    return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<UploadImageResponse>.Fail(
                        new ApiError("upload_quota_exceeded", "Daily upload limit reached.", details),
                        HttpContext.TraceIdentifier
                    ));
                }

                if (dailyMaxBytes > 0 && usedBytes >= dailyMaxBytes)
                {
                    var details = new Dictionary<string, string[]>
                    {
                        ["limitDailyBytes"] = [dailyMaxBytes.ToString()],
                        ["usedDailyBytes"] = [usedBytes.ToString()]
                    };
                    return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<UploadImageResponse>.Fail(
                        new ApiError("upload_quota_exceeded", "Daily upload bandwidth limit reached.", details),
                        HttpContext.TraceIdentifier
                    ));
                }
            }

            if (image.Width <= 0 || image.Height <= 0 || image.Width > MaxWidth || image.Height > MaxHeight)
            {
                return BadRequest(ApiResponse<UploadImageResponse>.Fail(
                    new ApiError("invalid_dimensions", $"Image dimensions must be between 1..{MaxWidth}x{MaxHeight}.", null),
                    HttpContext.TraceIdentifier
                ));
            }
            if ((long)image.Width * image.Height > MaxPixels)
            {
                return BadRequest(ApiResponse<UploadImageResponse>.Fail(
                    new ApiError("invalid_dimensions", $"Image pixel count exceeds limit ({MaxPixels}).", null),
                    HttpContext.TraceIdentifier
                ));
            }

            width = image.Width;
            height = image.Height;

            // Use the actual web root so static file middleware can serve the saved files.
            // (Directory.GetCurrentDirectory() can differ depending on how the app is launched.)
            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadsRoot = Path.Combine(webRoot, "uploads", "products");
            Directory.CreateDirectory(uploadsRoot);

            safeName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            absPath = Path.Combine(uploadsRoot, safeName);

            IImageEncoder encoder = ext.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => new JpegEncoder
                {
                    Quality = 85
                },
                ".png" => new PngEncoder
                {
                    ColorType = PngColorType.RgbWithAlpha,
                    CompressionLevel = PngCompressionLevel.DefaultCompression
                },
                ".webp" => new WebpEncoder
                {
                    Quality = 80,
                    FileFormat = WebpFileFormatType.Lossy
                },
                _ => new WebpEncoder { Quality = 80, FileFormat = WebpFileFormatType.Lossy }
            };

            await using (var fs = System.IO.File.Create(absPath))
            {
                await image.SaveAsync(fs, encoder, ct);
            }
        }

        var origin = string.IsNullOrWhiteSpace(_apiPublish.Value.PublicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : _apiPublish.Value.PublicBaseUrl.Trim().TrimEnd('/');
        var publicUrl = $"{origin}/uploads/products/{safeName}";

        long storedBytes = 0;
        try
        {
            storedBytes = new FileInfo(absPath).Length;
        }
        catch
        {
            // keep 0 if file stat fails
        }

        _db.UploadAudits.Add(new UploadAudit
        {
            UploadAuditId = Guid.NewGuid(),
            UserId = userId,
            Kind = "product_image",
            FileExt = ext.ToLowerInvariant(),
            FileSizeBytes = storedBytes == 0 ? file.Length : storedBytes,
            Width = width,
            Height = height,
            StoredPath = $"/uploads/products/{safeName}",
            PublicUrl = publicUrl,
            CreatedAtUtc = now
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Upload saved. kind={Kind} userId={UserId} bytes={Bytes} width={Width} height={Height} path={Path} limitDailyCount={LimitDailyCount} limitDailyBytes={LimitDailyBytes}",
            "product_image",
            userId,
            storedBytes == 0 ? file.Length : storedBytes,
            width,
            height,
            $"/uploads/products/{safeName}",
            dailyMaxCount,
            dailyMaxBytes);

        return Ok(ApiResponse<UploadImageResponse>.Ok(new UploadImageResponse(publicUrl), HttpContext.TraceIdentifier));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        userId = default;
        var raw =
            user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ??
            user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out userId);
    }
}

