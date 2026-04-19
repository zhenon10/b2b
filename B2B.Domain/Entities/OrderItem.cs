namespace B2B.Domain.Entities;

public sealed class OrderItem
{
    public Guid OrderItemId { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int LineNumber { get; set; }

    public string ProductSku { get; set; } = null!;
    public string ProductName { get; set; } = null!;

    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public byte[] RowVer { get; set; } = null!;
}

