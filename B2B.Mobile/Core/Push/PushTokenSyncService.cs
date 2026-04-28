using B2B.Mobile.Core.Api;
using Microsoft.Extensions.Logging;
#if ANDROID
using Android.Util;
#endif

namespace B2B.Mobile.Core.Push;

public sealed class PushTokenSyncService
{
    private readonly IPushTokenProvider _tokenProvider;
    private readonly PushTokensService _pushTokens;
    private readonly ILogger<PushTokenSyncService> _log;

    private DateTime _lastAttemptUtc = DateTime.MinValue;

    public PushTokenSyncService(IPushTokenProvider tokenProvider, ApiClient api, ILogger<PushTokenSyncService> log)
    {
        _tokenProvider = tokenProvider;
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
            var token = await _tokenProvider.TryGetTokenAsync(ct);

            if (string.IsNullOrWhiteSpace(token))
            {
#if ANDROID
                Log.Warn("B2B.Push", "FCM token is empty; skipping registration.");
#endif
                _log.LogWarning("FCM token is empty; skipping registration.");
                return;
            }

            var resp = await _pushTokens.RegisterAsync(token.Trim(), "Android", ct);
            if (!resp.Success)
            {
#if ANDROID
                Log.Warn("B2B.Push", "Push token register failed: {0} {1}", resp.Error?.Code, resp.Error?.Message);
#endif
                _log.LogWarning("Push token register failed: {Code} {Message}", resp.Error?.Code, resp.Error?.Message);
                return;
            }

#if ANDROID
            Log.Info("B2B.Push", "Push token registered. id={0}", resp.Data?.DevicePushTokenId);
#endif
            _log.LogInformation("Push token registered. id={Id}", resp.Data?.DevicePushTokenId);
        }
        catch (Exception ex)
        {
#if ANDROID
            Log.Warn("B2B.Push", "Push token registration failed unexpectedly: {0}", ex);
#endif
            _log.LogWarning(ex, "Push token registration failed unexpectedly.");
        }
    }
}

