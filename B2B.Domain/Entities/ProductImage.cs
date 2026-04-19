namespace B2B.Domain.Entities;

public sealed class ProductImage
{
    public Guid ProductImageId { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

