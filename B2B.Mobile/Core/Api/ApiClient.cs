using System.Net;

using System.Net.Http.Headers;

using System.Net.Http.Json;

using System.Text.Json;

using B2B.Contracts;
using B2B.Mobile.Core.Auth;
using Microsoft.Extensions.Logging;

namespace B2B.Mobile.Core.Api;



public sealed class ApiClient

{

    private static readonly JsonSerializerOptions JsonOptions = new()

    {

        PropertyNameCaseInsensitive = true

    };



    private readonly HttpClient _http;

    private readonly IAuthSession _authSession;

    private readonly IAccessTokenRefresher _refresher;

    private readonly ISessionSignOutHandler _signOut;

    private readonly ILogger<ApiClient> _logger;



    public ApiClient(

        HttpClient http,

        IAuthSession authSession,

        IAccessTokenRefresher refresher,

        ISessionSignOutHandler signOut,

        ILogger<ApiClient> logger)

    {

        _http = http;

        _authSession = authSession;

        _refresher = refresher;

        _signOut = signOut;

        _logger = logger;

    }



    public Task<ApiResponse<T>> GetAsync<T>(string relativeUrl, CancellationToken ct = default) =>

        SendWithRefreshAsync<T>(() => _http.GetAsync(relativeUrl, ct), ct);



    public Task<ApiResponse<TOut>> PostAsync<TOut>(string relativeUrl, CancellationToken ct = default) =>

        SendWithRefreshAsync<TOut>(() => _http.PostAsync(relativeUrl, content: null, ct), ct);



    public Task<ApiResponse<TOut>> PostAsync<TIn, TOut>(string relativeUrl, TIn body, CancellationToken ct = default) =>

        SendWithRefreshAsync<TOut>(() => _http.PostAsJsonAsync(relativeUrl, body, ct), ct);



    public Task<ApiResponse<T>> DeleteAsync<T>(string relativeUrl, CancellationToken ct = default) =>

        SendWithRefreshAsync<T>(() => _http.DeleteAsync(relativeUrl, ct), ct);



    public Task<ApiResponse<TOut>> PatchAsync<TIn, TOut>(string relativeUrl, TIn body, CancellationToken ct = default) =>

        SendWithRefreshAsync<TOut>(async () =>

        {

            using var req = new HttpRequestMessage(HttpMethod.Patch, relativeUrl)

            {

                Content = JsonContent.Create(body)

            };

            return await _http.SendAsync(req, ct);

        }, ct);



    public Task<ApiResponse<TOut>> PutAsync<TIn, TOut>(string relativeUrl, TIn body, CancellationToken ct = default) =>

        SendWithRefreshAsync<TOut>(() => _http.PutAsJsonAsync(relativeUrl, body, ct), ct);



    public Task<ApiResponse<TOut>> PostAsync<TIn, TOut>(

        string relativeUrl,

        TIn body,

        IDictionary<string, string>? headers,

        CancellationToken ct = default) =>

        SendWithRefreshAsync<TOut>(async () =>

        {

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



            return await _http.SendAsync(request, ct);

        }, ct);



    public Task<ApiResponse<TOut>> PostAsync<TOut>(

        string relativeUrl,

        IDictionary<string, string>? headers,

        CancellationToken ct = default) =>

        SendWithRefreshAsync<TOut>(async () =>

        {

            using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl);

            if (headers is not null)

            {

                foreach (var kv in headers)

                {

                    if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))

                        request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);

                }

            }



            return await _http.SendAsync(request, ct);

        }, ct);



    private async Task<ApiResponse<T>> SendWithRefreshAsync<T>(Func<Task<HttpResponseMessage>> sendOnce, CancellationToken ct)

    {

        try

        {

            await ApplyAuthAsync();

            var resp = await sendOnce();

            if (resp.StatusCode == HttpStatusCode.Unauthorized)

            {

                var path = resp.RequestMessage?.RequestUri?.AbsolutePath ?? "";

                if (!IsUnauthorizedOnAnonymousAuthPath(path)

                    && await _refresher.TryRefreshAsync(ct).ConfigureAwait(false))

                {

                    resp.Dispose();

                    await ApplyAuthAsync();

                    resp = await sendOnce();

                }

            }



            var apiResult = await ReadApiResponseAsync<T>(resp, ct);

            if (!apiResult.Success)

            {

                _logger.LogWarning(

                    "API call failed: {Method} {Url} trace={Trace} code={Code} message={Message}",

                    resp.RequestMessage?.Method.Method,

                    resp.RequestMessage?.RequestUri?.ToString(),

                    apiResult.TraceId,

                    apiResult.Error?.Code,

                    apiResult.Error?.Message);

            }

            return apiResult;

        }

        catch (TaskCanceledException ex)

        {

            var baseUrl = _http.BaseAddress?.ToString() ?? "(no base address)";

            _logger.LogWarning(ex, "API request timeout: {BaseUrl}", baseUrl);

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

            _logger.LogWarning(ex, "API request failed: {BaseUrl}", baseUrl);

            return new ApiResponse<T>(

                false,

                default,

                new ApiError("network_error", $"API’ye ulaşılamadı ({baseUrl}). {ex.Message}", null),

                TraceId: "");

        }

    }



    private async Task<ApiResponse<T>> ReadApiResponseAsync<T>(HttpResponseMessage resp, CancellationToken ct)

    {

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        var traceFromEnvelope = TryExtractTraceId(text);



        if (resp.StatusCode == HttpStatusCode.Unauthorized)

        {

            if (!IsUnauthorizedOnAnonymousAuthEndpoint(resp))

                await NotifyUnauthorizedSafeAsync(ct).ConfigureAwait(false);

            return new ApiResponse<T>(

                false,

                default,

                new ApiError("unauthorized", "Oturum geçersiz veya süresi doldu.", null),

                TraceId: traceFromEnvelope);

        }



        if (resp.StatusCode == HttpStatusCode.Forbidden)

        {

            return new ApiResponse<T>(

                false,

                default,

                new ApiError("forbidden", "Bu işlem için yetkiniz yok.", null),

                TraceId: traceFromEnvelope);

        }



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



    private static string TryExtractTraceId(string? json)

    {

        if (string.IsNullOrWhiteSpace(json))

            return "";

        try

        {

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("traceId", out var t))

                return t.GetString() ?? "";

        }

        catch

        {

            // ignored

        }



        return "";

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



    /// <summary>Yanlış şifre / kayıt hatası gibi durumlarda 401 beklenir; oturumu düşürme ve login sayfasına zorla gitme.</summary>

    private static bool IsUnauthorizedOnAnonymousAuthEndpoint(HttpResponseMessage resp)

    {

        var path = resp.RequestMessage?.RequestUri?.AbsolutePath ?? "";

        return IsUnauthorizedOnAnonymousAuthPath(path);

    }



    private static bool IsUnauthorizedOnAnonymousAuthPath(string path)

    {

        if (path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))

            return true;

        if (path.Contains("/auth/register", StringComparison.OrdinalIgnoreCase))

            return true;

        if (path.Contains("/auth/change-password", StringComparison.OrdinalIgnoreCase))

            return true;

        if (path.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase))

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


