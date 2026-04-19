using B2B.Domain.Enums;

namespace B2B.Domain.Entities;

public sealed class Order
{
    public Guid OrderId { get; set; }

    public long OrderNumber { get; set; }

    public Guid BuyerUserId { get; set; }
    public User BuyerUser { get; set; } = null!;

    public Guid SellerUserId { get; set; }
    public User SellerUser { get; set; } = null!;

    public OrderStatus Status { get; set; }

    public string CurrencyCode { get; set; } = null!;
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

