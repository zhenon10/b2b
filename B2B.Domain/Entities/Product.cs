namespace B2B.Domain.Entities;

public sealed class Product
{
    public Guid ProductId { get; set; }

    public Guid SellerUserId { get; set; }
    public User SellerUser { get; set; } = null!;

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public string CurrencyCode { get; set; } = null!;
    /// <summary>Bayi (dealer) fiyatı.</summary>
    public decimal DealerPrice { get; set; }
    /// <summary>Tavsiye edilen son kullanıcı fiyatı (MSRP).</summary>
    public decimal MsrpPrice { get; set; }
    public int StockQuantity { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductSpec> Specs { get; set; } = new List<ProductSpec>();

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

