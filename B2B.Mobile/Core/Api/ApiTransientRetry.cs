using B2B.Contracts;

namespace B2B.Mobile.Core.Api;

/// <summary>
/// Ağ dalgalanması / kısa kesintiler için sınırlı yeniden deneme.
/// Yazma işlemleri için sunucunun idempotency desteklediği durumlarda kullanın.
/// </summary>
public static class ApiTransientRetry
{
    private const int MaxAttempts = 3;

    private static bool IsTransient(ApiError? e) =>
        e?.Code is "timeout" or "network_error" or "empty_response" or "server_error";

    public static async Task<ApiResponse<T>> ExecuteAsync<T>(
        Func<Task<ApiResponse<T>>> send,
        CancellationToken ct)
    {
        ApiResponse<T>? last = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            last = await send();
            if (last.Success || last.Error is null || !IsTransient(last.Error))
                return last;

            if (attempt == MaxAttempts)
                return last;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(350 * (1 << (attempt - 1))), ct);
            }
            catch (OperationCanceledException)
            {
                return last;
            }
        }

        return last!;
    }
}
