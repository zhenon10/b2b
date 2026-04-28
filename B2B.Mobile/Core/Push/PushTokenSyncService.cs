using B2B.Mobile.Core.Api;
using Microsoft.Extensions.Logging;
using Plugin.Firebase.CloudMessaging;

namespace B2B.Mobile.Core.Push;

public sealed class PushTokenSyncService
{
    private readonly IFirebaseCloudMessaging _fcm;
    private readonly PushTokensService _pushTokens;
    private readonly ILogger<PushTokenSyncService> _log;

    private DateTime _lastAttemptUtc = DateTime.MinValue;

    public PushTokenSyncService(IFirebaseCloudMessaging fcm, ApiClient api, ILogger<PushTokenSyncService> log)
    {
        _fcm = fcm;
        _pushTokens = new PushTokensService(api);
        _log = log;
    }

    public void Start()
    {
        // Fire-and-forget; errors are logged.
        _ = EnsureRegisteredAsync();
    }

    public async Task EnsureRegisteredAsync(CancellationToken ct = default)
    {
        // Avoid spamming API on frequent resume events.
        if (DateTime.UtcNow - _lastAttemptUtc < TimeSpan.FromMinutes(5))
            return;
        _lastAttemptUtc = DateTime.UtcNow;

        try
        {
            await _fcm.CheckIfValidAsync();

            // Some versions return Task<string>, some only trigger internal state.
            string? token = null;
            var task = _fcm.GetTokenAsync();
            if (task is Task<string> tokenTask)
                token = await tokenTask;
            else
                await task;

            if (string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    dynamic d = _fcm;
                    token = d.Token as string;
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                _log.LogWarning("FCM token is empty; skipping registration.");
                return;
            }

            var resp = await _pushTokens.RegisterAsync(token.Trim(), "Android", ct);
            if (!resp.Success)
            {
                _log.LogWarning("Push token register failed: {Code} {Message}", resp.Error?.Code, resp.Error?.Message);
                return;
            }

            _log.LogInformation("Push token registered. id={Id}", resp.Data?.DevicePushTokenId);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Push token registration failed unexpectedly.");
        }
    }
}

