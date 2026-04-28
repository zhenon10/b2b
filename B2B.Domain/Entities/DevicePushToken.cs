namespace B2B.Domain.Entities;

public sealed class DevicePushToken
{
    public Guid DevicePushTokenId { get; set; }

    /// <summary>Nullable: allow token registration before login; can be linked later.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string Platform { get; set; } = "Android";

    public string Token { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

