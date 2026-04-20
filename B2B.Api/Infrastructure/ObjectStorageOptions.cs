namespace B2B.Api.Infrastructure;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>"Local" (default) or "S3".</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Optional absolute base URL for public assets (e.g. https://cdn.example.com). No trailing slash.</summary>
    public string PublicBaseUrl { get; set; } = "";

    // S3-compatible settings
    public string Bucket { get; set; } = "";
    public string Region { get; set; } = "";
    public string Endpoint { get; set; } = ""; // optional (MinIO, etc.)
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public bool ForcePathStyle { get; set; } = false;
}

