namespace B2B.Domain.Entities;

/// <summary>Admin bayi onayı için Idempotency-Key kaydı (yeniden deneme güvenliği).</summary>
public sealed class AdminDealerApprovalIdempotency
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public Guid TargetUserId { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
}
