namespace B2B.Contracts;

public sealed record RegisterPushTokenRequest(
    string Token,
    string Platform
);

public sealed record RegisterPushTokenResponse(
    Guid DevicePushTokenId,
    bool IsActive
);

public sealed record CreateAdminNotificationRequest(
    string Title,
    string Body,
    string? DataJson = null
);

public sealed record NotificationListItem(
    Guid NotificationId,
    string Title,
    string Body,
    string? DataJson,
    DateTime CreatedAtUtc,
    bool IsRead
);

