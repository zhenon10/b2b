namespace B2B.Domain.Entities;

public sealed class NotificationDelivery
{
    public Guid NotificationDeliveryId { get; set; }

    public Guid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public Guid? DevicePushTokenId { get; set; }
    public DevicePushToken? DevicePushToken { get; set; }

    public string Status { get; set; } = "Sent"; // Sent | Failed
    public string? Error { get; set; }
    public DateTime SentAtUtc { get; set; }
}

