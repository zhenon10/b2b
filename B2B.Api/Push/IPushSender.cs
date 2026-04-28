using B2B.Domain.Entities;

namespace B2B.Api.Push;

public sealed record PushTokenSendResult(
    Guid DevicePushTokenId,
    Guid? UserId,
    bool Success,
    string? Error,
    bool DeactivateToken
);

public sealed record PushBroadcastResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<PushTokenSendResult> Results
);

public interface IPushSender
{
    Task<PushBroadcastResult> SendBroadcastAsync(Notification notification, IReadOnlyList<DevicePushToken> tokens, CancellationToken ct);
}

public sealed class NoopPushSender : IPushSender
{
    public Task<PushBroadcastResult> SendBroadcastAsync(Notification notification, IReadOnlyList<DevicePushToken> tokens, CancellationToken ct) =>
        Task.FromResult(new PushBroadcastResult(0, 0, Array.Empty<PushTokenSendResult>()));
}

