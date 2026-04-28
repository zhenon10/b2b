namespace B2B.Domain.Entities;

public sealed class Notification
{
    public Guid NotificationId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public string Target { get; set; } = "All";

    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    /// <summary>Optional structured payload (JSON) for deep-links, etc.</summary>
    public string? DataJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

