using System.Text.Json;
using B2B.Domain.Entities;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace B2B.Api.Push;

public sealed class FcmPushSender : IPushSender
{
    private static readonly object Gate = new();
    private static FirebaseApp? _app;

    private readonly ILogger<FcmPushSender> _log;
    private readonly PushOptions _push;
    private readonly FcmOptions _fcm;

    public FcmPushSender(
        ILogger<FcmPushSender> log,
        IOptions<PushOptions> push,
        IOptions<FcmOptions> fcm)
    {
        _log = log;
        _push = push.Value;
        _fcm = fcm.Value;
    }

    public async Task<PushBroadcastResult> SendBroadcastAsync(B2B.Domain.Entities.Notification notification, IReadOnlyList<DevicePushToken> tokens, CancellationToken ct)
    {
        if (!_push.Enabled) return new PushBroadcastResult(0, 0, Array.Empty<PushTokenSendResult>());

        var serviceAccountJson = _fcm.ServiceAccountJson;
        if (string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            _log.LogWarning("FCM enabled but ServiceAccountJson missing.");
            return new PushBroadcastResult(0, 0, Array.Empty<PushTokenSendResult>());
        }

        EnsureFirebaseApp(serviceAccountJson);

        var results = new List<PushTokenSendResult>(tokens.Count);
        var successTotal = 0;
        var failureTotal = 0;

        // FCM multicast limit is 500 tokens per call
        const int batchSize = 500;
        for (var i = 0; i < tokens.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = tokens.Skip(i).Take(batchSize).ToList();
            var to = batch.Select(x => x.Token).ToList();

            var msg = new MulticastMessage
            {
                Tokens = to,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = notification.Title,
                    Body = notification.Body
                },
                Data = BuildData(notification)
            };

            var resp = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(msg, ct);
            successTotal += resp.SuccessCount;
            failureTotal += resp.FailureCount;

            for (var j = 0; j < batch.Count; j++)
            {
                var t = batch[j];
                var r = resp.Responses[j];
                if (r.IsSuccess)
                {
                    results.Add(new PushTokenSendResult(t.DevicePushTokenId, t.UserId, true, null, false));
                }
                else
                {
                    var err = r.Exception?.Message;
                    var deactivate = ShouldDeactivate(r.Exception);
                    results.Add(new PushTokenSendResult(t.DevicePushTokenId, t.UserId, false, err, deactivate));
                }
            }

            _log.LogInformation("FCM multicast sent: success={Success} failure={Failure}", resp.SuccessCount, resp.FailureCount);
        }

        return new PushBroadcastResult(successTotal, failureTotal, results);
    }

    private static Dictionary<string, string> BuildData(B2B.Domain.Entities.Notification notification)
    {
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.NotificationId.ToString()
        };

        if (!string.IsNullOrWhiteSpace(notification.DataJson))
        {
            // store as-is for clients; keep key stable
            data["dataJson"] = notification.DataJson!;

            // best-effort: also flatten simple JSON object into key/values
            try
            {
                using var doc = JsonDocument.Parse(notification.DataJson!);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.String)
                            data[$"data.{p.Name}"] = p.Value.GetString() ?? "";
                        else if (p.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                            data[$"data.{p.Name}"] = p.Value.ToString();
                    }
                }
            }
            catch
            {
                // ignore invalid json
            }
        }

        return data;
    }

    private static void EnsureFirebaseApp(string serviceAccountJson)
    {
        if (_app is not null) return;
        lock (Gate)
        {
            if (_app is not null) return;
            _app = FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(serviceAccountJson)
            });
        }
    }

    private static bool ShouldDeactivate(Exception? ex)
    {
        if (ex is FirebaseMessagingException fex)
        {
            // Conservative: only deactivate on known permanent token errors.
            // FirebaseAdmin .NET doesn't expose a stable enum across versions; check by string.
            var code = fex.MessagingErrorCode?.ToString() ?? "";
            return code.Contains("Unregistered", StringComparison.OrdinalIgnoreCase)
                   || code.Contains("Invalid", StringComparison.OrdinalIgnoreCase);
        }

        var msg = ex?.Message ?? "";
        return msg.Contains("registration token is not a valid FCM registration token", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("Requested entity was not found", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase);
    }
}

