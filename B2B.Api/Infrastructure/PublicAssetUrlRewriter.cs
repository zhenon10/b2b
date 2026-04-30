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

        var opts = request.HttpContext.RequestServices?.GetService<IOptions<PublicAssetUrlOptions>>()?.Value;

        var trimmed = storedUrl.Trim();

        if (trimmed.StartsWith(UploadsPathPrefix, StringComparison.OrdinalIgnoreCase))
            return $"{request.Scheme}://{request.Host}{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return storedUrl;

        if (!uri.AbsolutePath.StartsWith(UploadsPathPrefix, StringComparison.OrdinalIgnoreCase))
            return storedUrl;

        // Fix common misconfiguration: URLs stored without the external port (e.g. http://host/uploads/...)
        // while clients call the API on a non-default port (e.g. :8080). If host matches, prefer the
        // current request authority.
        if (HostMatchesButPortDiffers(uri, request))
            return $"{request.Scheme}://{request.Host}{uri.PathAndQuery}";

        if (IsLoopbackHost(uri.Host))
            return $"{request.Scheme}://{request.Host}{uri.PathAndQuery}";

        // Optional extra safety net for legacy data.
        if (opts?.RewritePrivateLanUploadUrls == true && IsPrivateOrCarrierNatIPv4Host(uri.Host))
            return $"{request.Scheme}://{request.Host}{uri.PathAndQuery}";

        return storedUrl;
    }

    private static bool HostMatchesButPortDiffers(Uri uri, HttpRequest request)
    {
        // Compare hosts only (ignore port). For IPv6, Uri.Host contains the address without brackets.
        if (!uri.Host.Equals(request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        // If either side has no explicit port, Uri/Host will surface defaults (80/443).
        // We want to rewrite only when the authority differs.
        var reqPort = request.Host.Port ?? (string.Equals(request.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);
        var uriPort = uri.IsDefaultPort
            ? (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : uri.Port;

        return reqPort != uriPort;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        return IPAddress.IsLoopback(ip);
    }

    /// <summary>
    /// Unroutable IPv4 hosts: RFC1918 + Carrier-grade NAT (RFC6598, 100.64.0.0/10).
    /// </summary>
    private static bool IsPrivateOrCarrierNatIPv4Host(string host)
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
        // RFC6598: 100.64.0.0/10 (Carrier-grade NAT, also used by Tailscale CGNAT range).
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            return true;
        return false;
    }
}
