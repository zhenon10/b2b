namespace B2B.Domain.Entities;

public sealed class NotificationRead
{
    public Guid NotificationReadId { get; set; }

    public Guid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime ReadAtUtc { get; set; }
}

