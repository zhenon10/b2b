using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace B2B.Api.Infrastructure;

/// <summary>
/// Rewrites stored upload URLs so LAN/mobile clients receive a reachable absolute URL.
/// Fixes DB rows saved as <c>http://localhost:port/uploads/...</c> or relative <c>/uploads/...</c>.
/// </summary>
public static class PublicAssetUrlRewriter
{
    private const string UploadsPathPrefix = "/uploads/";

    public static string? RewriteForRequest(string? storedUrl, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(storedUrl))
            return storedUrl;

        var opts = request.HttpContext.RequestServices.GetService<IOptions<PublicAssetUrlOptions>>()?.Value;

        var trimmed = storedUrl.Trim();

        if (trimmed.StartsWith(UploadsPathPrefix, StringComparison.OrdinalIgnoreCase))
            return $"{request.Scheme}://{request.Host}{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return storedUrl;

        if (!uri.AbsolutePath.StartsWith(UploadsPathPrefix, StringComparison.OrdinalIgnoreCase))
            return storedUrl;

        if (IsLoopbackHost(uri.Host))
            return $"{request.Scheme}://{request.Host}{uri.PathAndQuery}";

        if (opts?.RewritePrivateLanUploadUrls == true && IsPrivateIPv4Host(uri.Host))
            return $"{request.Scheme}://{request.Host}{uri.PathAndQuery}";

        return storedUrl;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        return IPAddress.IsLoopback(ip);
    }

    /// <summary>RFC1918 IPv4 hosts only (upload URLs on same LAN).</summary>
    private static bool IsPrivateIPv4Host(string host)
    {
        if (!IPAddress.TryParse(host, out var ip))
            return false;
        if (ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var b = ip.GetAddressBytes();
        if (b[0] == 10)
            return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            return true;
        if (b[0] == 192 && b[1] == 168)
            return true;
        return false;
    }
}
