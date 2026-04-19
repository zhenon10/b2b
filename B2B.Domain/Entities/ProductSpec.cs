namespace B2B.Domain.Entities;

public sealed class ProductSpec
{
    public Guid ProductSpecId { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

