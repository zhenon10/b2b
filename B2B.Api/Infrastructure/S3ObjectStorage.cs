using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace B2B.Api.Infrastructure;

public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _s3;
    private readonly IOptions<ObjectStorageOptions> _opts;

    public S3ObjectStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> opts)
    {
        _s3 = s3;
        _opts = opts;
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var o = _opts.Value;
        var bucket = o.Bucket?.Trim();
        if (string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOperationException("ObjectStorage:Bucket is required for S3 provider.");

        var safeKey = key.Replace("\\", "/").TrimStart('/');
        content.Position = 0;

        var req = new PutObjectRequest
        {
            BucketName = bucket,
            Key = safeKey,
            InputStream = content,
            ContentType = contentType
        };

        await _s3.PutObjectAsync(req, ct);
    }

    public string GetPublicUrl(string key, HttpRequest request)
    {
        var o = _opts.Value;
        var safeKey = key.Replace("\\", "/").TrimStart('/');

        var configured = o.PublicBaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
            return $"{configured}/{safeKey}";

        // Best-effort default: virtual-hosted AWS style.
        // For custom endpoints (MinIO), prefer configuring PublicBaseUrl.
        var bucket = o.Bucket?.Trim();
        var region = o.Region?.Trim();
        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(region))
            return safeKey;

        return $"https://{bucket}.s3.{region}.amazonaws.com/{safeKey}";
    }

    public static IAmazonS3 BuildClient(ObjectStorageOptions o)
    {
        var cfg = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(o.Region))
            cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(o.Region);

        if (!string.IsNullOrWhiteSpace(o.Endpoint))
        {
            cfg.ServiceURL = o.Endpoint.Trim();
            cfg.ForcePathStyle = o.ForcePathStyle;
        }

        // Prefer explicit static credentials when configured; otherwise let AWS SDK resolve credentials (env/instance role/etc.).
        AWSCredentials? creds = string.IsNullOrWhiteSpace(o.AccessKeyId)
            ? null
            : new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey);

        return creds is null ? new AmazonS3Client(cfg) : new AmazonS3Client(creds, cfg);
    }
}

