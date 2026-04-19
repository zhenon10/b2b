namespace B2B.Domain.Entities;

/// <summary>
/// Idempotency record for order submissions (mobile retry safety).
/// Key is unique per buyer.
/// </summary>
public sealed class OrderSubmission
{
    public Guid OrderSubmissionId { get; set; }

    public Guid BuyerUserId { get; set; }
    public Guid SellerUserId { get; set; }

    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}

