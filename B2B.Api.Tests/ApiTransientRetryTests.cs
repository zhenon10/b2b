using B2B.Contracts;
using FluentAssertions;
using Xunit;

namespace B2B.Api.Tests;

/// <summary>
/// Mobil <c>ApiTransientRetry</c> ile aynı yeniden deneme kurallarının davranış doğrulaması (paylaşımlı derleme yok).</summary>
public sealed class ApiTransientRetryTests
{
    private static bool IsTransient(ApiError? e) =>
        e?.Code is "timeout" or "network_error" or "empty_response" or "server_error";

    private static async Task<ApiResponse<T>> ExecuteAsync<T>(
        Func<Task<ApiResponse<T>>> send,
        CancellationToken ct)
    {
        const int maxAttempts = 3;
        ApiResponse<T>? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            last = await send();
            if (last.Success || last.Error is null || !IsTransient(last.Error))
                return last;

            if (attempt == maxAttempts)
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

    [Fact]
    public async Task Retries_transient_errors_then_succeeds()
    {
        var calls = 0;
        var r = await ExecuteAsync(
            async () =>
            {
                calls++;
                if (calls < 3)
                    return ApiResponse<int>.Fail(new ApiError("timeout", "t", null), "a");
                return ApiResponse<int>.Ok(42, "b");
            },
            CancellationToken.None);

        r.Success.Should().BeTrue();
        r.Data.Should().Be(42);
        calls.Should().Be(3);
    }

    [Fact]
    public async Task Does_not_retry_business_errors()
    {
        var calls = 0;
        var r = await ExecuteAsync(
            () =>
            {
                calls++;
                return Task.FromResult(ApiResponse<int>.Fail(new ApiError("not_found", "n", null), "x"));
            },
            CancellationToken.None);

        r.Success.Should().BeFalse();
        r.Error!.Code.Should().Be("not_found");
        calls.Should().Be(1);
    }
}
