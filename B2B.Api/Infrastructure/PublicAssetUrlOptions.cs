namespace B2B.Api.Infrastructure;

/// <summary>Upload URL rewriting for product images returned by the API.</summary>
public sealed class PublicAssetUrlOptions
{
    public const string SectionName = "PublicAssets";

    /// <summary>
    /// When true, absolute URLs under <c>/uploads/</c> whose host is a private IPv4 address
    /// (10/8, 172.16–31, 192.168/16) are rewritten to the current request host.
    /// Helps DB rows saved with an old LAN IP while clients call the API on the PC’s current IP.
    /// </summary>
    public bool RewritePrivateLanUploadUrls { get; set; }
}
