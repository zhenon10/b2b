using System.Diagnostics;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Features.Orders.Services;

/// <summary>
/// Ağ dalgalanması / kısa kesintiler için sınırlı yeniden deneme (aynı idempotency ile güvenli).
/// </summary>
internal static class OrderTransientRetry
{
    private const int MaxAttempts = 3;

    private static bool IsTransient(ApiError? e) =>
        e?.Code is "timeout" or "network_error" or "empty_response" or "server_error";

    public static async Task<ApiResponse<T>> ExecuteAsync<T>(
        Func<Task<ApiResponse<T>>> send,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var r = await send();
            if (r.Success || r.Error is null || !IsTransient(r.Error))
                return r;

            if (attempt == MaxAttempts)
                return r;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(350 * (1 << (attempt - 1))), ct);
            }
            catch (OperationCanceledException)
            {
                return r;
            }
        }

        throw new UnreachableException();
    }
}
