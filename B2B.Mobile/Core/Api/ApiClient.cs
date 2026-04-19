using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using B2B.Mobile.Core.Auth;

namespace B2B.Mobile.Core.Api;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IAuthSession _authSession;
    private readonly ISessionSignOutHandler _signOut;

    public ApiClient(HttpClient http, IAuthSession authSession, ISessionSignOutHandler signOut)
    {
        _http = http;
        _authSession = authSession;
        _signOut = signOut;
    }

    public async Task<ApiResponse<T>> GetAsync<T>(string relativeUrl, CancellationToken ct = default)
    {
        try
        {
            await ApplyAuthAsync();
            var resp = await _http.GetAsync(relativeUrl, ct);
            return await ReadApiResponseAsync<T>(resp, ct);
        }
        catch (TaskCanceledException ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<T>(
                false,
                default,
                new ApiError(
                    "timeout",
                    $"İstek zaman aşımına uğradı: {baseUrl}. {ex.Message}",
                    null),
                TraceId: "");
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<T>(
                false,
                default,
                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),
                TraceId: "");
        }
    }

    public async Task<ApiResponse<TOut>> PostAsync<TOut>(string relativeUrl, CancellationToken ct = default)
    {
        try
        {
            await ApplyAuthAsync();
            var resp = await _http.PostAsync(relativeUrl, content: null, ct);
            return await ReadApiResponseAsync<TOut>(resp, ct);
        }
        catch (TaskCanceledException ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError(
                    "timeout",
                    $"İstek zaman aşımına uğradı: {baseUrl}. {ex.Message}",
                    null),
                TraceId: "");
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),
                TraceId: "");
        }
    }

    public async Task<ApiResponse<TOut>> PostAsync<TIn, TOut>(string relativeUrl, TIn body, CancellationToken ct = default)
    {
        try
        {
            await ApplyAuthAsync();
            var resp = await _http.PostAsJsonAsync(relativeUrl, body, ct);
            return await ReadApiResponseAsync<TOut>(resp, ct);
        }
        catch (TaskCanceledException ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError(
                    "timeout",
                    $"İstek zaman aşımına uğradı: {baseUrl}. {ex.Message}",
                    null),
                TraceId: "");
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),
                TraceId: "");
        }
    }

    public async Task<ApiResponse<T>> DeleteAsync<T>(string relativeUrl, CancellationToken ct = default)
    {
        try
        {
            await ApplyAuthAsync();
            var resp = await _http.DeleteAsync(relativeUrl, ct);
            return await ReadApiResponseAsync<T>(resp, ct);
        }
        catch (TaskCanceledException ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<T>(
                false,
                default,
                new ApiError("timeout", $"İstek zaman aşımına uğradı: {baseUrl}. {ex.Message}", null),
                TraceId: "");
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<T>(
                false,
                default,
                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),
                TraceId: "");
        }
    }

    public async Task<ApiResponse<TOut>> PatchAsync<TIn, TOut>(string relativeUrl, TIn body, CancellationToken ct = default)
    {
        try
        {
            await ApplyAuthAsync();
            using var req = new HttpRequestMessage(HttpMethod.Patch, relativeUrl)
            {
                Content = JsonContent.Create(body)
            };
            var resp = await _http.SendAsync(req, ct);
            return await ReadApiResponseAsync<TOut>(resp, ct);
        }
        catch (TaskCanceledException ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError(
                    "timeout",
                    $"İstek zaman aşımına uğradı: {baseUrl}. {ex.Message}",
                    null),
                TraceId: "");
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),
                TraceId: "");
        }
    }

    public async Task<ApiResponse<TOut>> PutAsync<TIn, TOut>(string relativeUrl, TIn body, CancellationToken ct = default)
    {
        try
        {
            await ApplyAuthAsync();
            var resp = await _http.PutAsJsonAsync(relativeUrl, body, ct);
            return await ReadApiResponseAsync<TOut>(resp, ct);
        }
        catch (TaskCanceledException ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError(
                    "timeout",
                    $"İstek zaman aşımına uğradı: {baseUrl}. {ex.Message}",
                    null),
                TraceId: "");
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),
                TraceId: "");
        }
    }

    public async Task<ApiResponse<TOut>> PostAsync<TIn, TOut>(
        string relativeUrl,
        TIn body,
        IDictionary<string, string>? headers,
        CancellationToken ct = default)
    {
        try
        {
            await ApplyAuthAsync();

            using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
            {
                Content = JsonContent.Create(body)
            };

            if (headers is not null)
            {
                foreach (var kv in headers)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                        request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            var resp = await _http.SendAsync(request, ct);
            return await ReadApiResponseAsync<TOut>(resp, ct);
        }
        catch (TaskCanceledException ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError(
                    "timeout",
                    $"İstek zaman aşımına uğradı: {baseUrl}. {ex.Message}",
                    null),
                TraceId: "");
        }
        catch (Exception ex)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";
            return new ApiResponse<TOut>(
                false,
                default,
                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),
                TraceId: "");
        }
    }

    private async Task<ApiResponse<T>> ReadApiResponseAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (!IsUnauthorizedOnAnonymousAuthEndpoint(resp))
                await NotifyUnauthorizedSafeAsync(ct).ConfigureAwait(false);
            return new ApiResponse<T>(
                false,
                default,
                new ApiError("unauthorized", "Oturum geçersiz veya süresi doldu.", null),
                TraceId: "");
        }

        if (resp.StatusCode == HttpStatusCode.Forbidden)
        {
            return new ApiResponse<T>(
                false,
                default,
                new ApiError("forbidden", "Bu işlem için yetkiniz yok.", null),
                TraceId: "");
        }

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ApiResponse<T>(
                false,
                default,
                new ApiError(
                    "empty_response",
                    $"Sunucudan boş yanıt geldi (HTTP {(int)resp.StatusCode}). API’yi güncel kodla yeniden başlatın; adres ve portun mobil uygulamadakiyle aynı olduğundan emin olun.",
                    null),
                TraceId: "");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ApiResponse<T>>(text, JsonOptions);
            var result = parsed ?? new ApiResponse<T>(false, default, new ApiError("invalid_response", "Geçersiz yanıt.", null), TraceId: "");
            if (!result.Success
                && result.Error is { Code: var code }
                && string.Equals(code, "unauthorized", StringComparison.OrdinalIgnoreCase)
                && !IsUnauthorizedOnAnonymousAuthEndpoint(resp))
            {
                await NotifyUnauthorizedSafeAsync(ct).ConfigureAwait(false);
            }

            return result;
        }
        catch (JsonException)
        {
            return new ApiResponse<T>(
                false,
                default,
                new ApiError(
                    "invalid_response",
                    $"Sunucu yanıtı JSON formatında değil (HTTP {(int)resp.StatusCode}). API adresi ve güvenlik duvarı ayarlarını kontrol edin.",
                    null),
                TraceId: "");
        }
    }

    private async Task NotifyUnauthorizedSafeAsync(CancellationToken ct)
    {
        try
        {
            await _signOut
                .SignOutAndNavigateToLoginAsync(ct, LoginSessionEndKind.SessionExpired)
                .ConfigureAwait(false);
        }
        catch
        {
            // Oturum temizliği / navigasyon hatası: çağırana yine de ApiResponse döndürülür
        }
    }

    /// <summary>
    /// Yanlış şifre / kayıt hatası gibi durumlarda 401 beklenir; oturumu düşürme ve login’e zorla gitme.
    /// </summary>
    private static bool IsUnauthorizedOnAnonymousAuthEndpoint(HttpResponseMessage resp)
    {
        var path = resp.RequestMessage?.RequestUri?.AbsolutePath ?? "";
        if (path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.Contains("/auth/register", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.Contains("/auth/change-password", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private async Task ApplyAuthAsync()
    {
        var token = await _authSession.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = null;
            return;
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
