using Microsoft.Extensions.Options;

namespace B2B.Api.Infrastructure;

public sealed class LocalFileObjectStorage : IObjectStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<ApiPublishingOptions> _apiPublishing;
    private readonly IOptions<ObjectStorageOptions> _storage;

    public LocalFileObjectStorage(
        IWebHostEnvironment env,
        IOptions<ApiPublishingOptions> apiPublishing,
        IOptions<ObjectStorageOptions> storage)
    {
        _env = env;
        _apiPublishing = apiPublishing;
        _storage = storage;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        // key like: uploads/products/<file>
        var safeKey = key.Replace("\\", "/").TrimStart('/');
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var absPath = Path.Combine(webRoot, safeKey.Replace("/", Path.DirectorySeparatorChar.ToString()));
        Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);

        content.Position = 0;
        await using var fs = System.IO.File.Create(absPath);
        await content.CopyToAsync(fs, ct);
    }

    public string GetPublicUrl(string key, HttpRequest request)
    {
        var safeKey = key.Replace("\\", "/").TrimStart('/');
        var configured = _storage.Value.PublicBaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
            return $"{configured}/{safeKey}";

        var apiBase = _apiPublishing.Value.PublicBaseUrl?.Trim().TrimEnd('/');
        var origin = string.IsNullOrWhiteSpace(apiBase) ? $"{request.Scheme}://{request.Host}" : apiBase;
        return $"{origin}/{safeKey}";
    }
}

