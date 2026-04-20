using B2B.Contracts;
using B2B.Api.Infrastructure;
using B2B.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
public sealed class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<ApiPublishingOptions> _apiPublish;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public UploadsController(IWebHostEnvironment env, IOptions<ApiPublishingOptions> apiPublish)
    {
        _env = env;
        _apiPublish = apiPublish;
    }

    [HttpPost("images")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadImageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UploadImageResponse>>> UploadImage([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UploadImageResponse>.Fail(
                new ApiError("invalid_file", "File is required.", null),
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

        // Use the actual web root so static file middleware can serve the saved files.
        // (Directory.GetCurrentDirectory() can differ depending on how the app is launched.)
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var uploadsRoot = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(uploadsRoot);

        var safeName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absPath = Path.Combine(uploadsRoot, safeName);

        await using (var fs = System.IO.File.Create(absPath))
        {
            await file.CopyToAsync(fs, ct);
        }

        var origin = string.IsNullOrWhiteSpace(_apiPublish.Value.PublicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : _apiPublish.Value.PublicBaseUrl.Trim().TrimEnd('/');
        var publicUrl = $"{origin}/uploads/products/{safeName}";
        return Ok(ApiResponse<UploadImageResponse>.Ok(new UploadImageResponse(publicUrl), HttpContext.TraceIdentifier));
    }
}

