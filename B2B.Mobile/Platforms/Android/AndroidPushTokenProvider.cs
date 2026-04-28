using Android.Gms.Extensions;
using Firebase.Messaging;
using B2B.Mobile.Core.Push;
using Android.Util;

namespace B2B.Mobile.Platforms.Android;

public sealed class AndroidPushTokenProvider : IPushTokenProvider
{
    public async Task<string?> TryGetTokenAsync(CancellationToken ct = default)
    {
        try
        {
            // FirebaseMessaging.GetToken() returns a Play Services Task.
            // The AsAsync() helper only supports Java objects, so we use Java.Lang.String.
            var jToken = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
            var token = jToken?.ToString();
            Log.Info("B2B.Push", "FirebaseMessaging token acquired. len={0}", token?.Length ?? 0);
            return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        }
        catch
        {
            Log.Warn("B2B.Push", "FirebaseMessaging token acquisition failed.");
            return null;
        }
    }
}

