namespace B2B.Domain.Entities;

public sealed class CustomerAccount
{
    public Guid CustomerAccountId { get; set; }

    /// <summary>Customer (Buyer / Dealer).</summary>
    public Guid BuyerUserId { get; set; }
    public User BuyerUser { get; set; } = null!;

    /// <summary>Supplier (seller side of the order).</summary>
    public Guid SellerUserId { get; set; }
    public User SellerUser { get; set; } = null!;

    public string CurrencyCode { get; set; } = null!;

    /// <summary>
    /// Current receivable from the customer (>= 0). Updated transactionally with entries.
    /// </summary>
    public decimal Balance { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVer { get; set; } = null!;

    public ICollection<CustomerAccountEntry> Entries { get; set; } = new List<CustomerAccountEntry>();
}

