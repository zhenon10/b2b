using B2B.Domain.Enums;

namespace B2B.Domain.Entities;

public sealed class CustomerAccountEntry
{
    public Guid CustomerAccountEntryId { get; set; }

    public Guid CustomerAccountId { get; set; }
    public CustomerAccount CustomerAccount { get; set; } = null!;

    public CustomerAccountEntryType Type { get; set; }

    public string CurrencyCode { get; set; } = null!;

    /// <summary>Positive amount. Debit increases balance, credit decreases balance.</summary>
    public decimal Amount { get; set; }

    /// <summary>Optional link to an order that caused this movement.</summary>
    public Guid? OrderId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

