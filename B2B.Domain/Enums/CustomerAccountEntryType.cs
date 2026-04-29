namespace B2B.Domain.Enums;

public enum CustomerAccountEntryType : byte
{
    /// <summary>Order placed (unpaid) -> customer owes money.</summary>
    DebitOrderPlaced = 1,

    /// <summary>Order marked as paid -> customer debt is settled.</summary>
    CreditOrderPaid = 2,

    /// <summary>Order cancelled after being placed -> remove previously created debt.</summary>
    CreditOrderCancelled = 3
}

