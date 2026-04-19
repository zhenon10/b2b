namespace B2B.Domain.Enums;

public enum OrderStatus : byte
{
    Draft = 0,
    Placed = 1,
    Paid = 2,
    Shipped = 3,
    Cancelled = 4
}

