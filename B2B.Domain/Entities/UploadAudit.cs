namespace B2B.Domain.Entities;

public sealed class UploadAudit
{
    public Guid UploadAuditId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Kind { get; set; } = null!; // e.g. "product_image"
    public string FileExt { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string StoredPath { get; set; } = null!;
    public string PublicUrl { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}

