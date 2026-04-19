namespace B2B.Domain.Entities;

public sealed class Category
{
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
