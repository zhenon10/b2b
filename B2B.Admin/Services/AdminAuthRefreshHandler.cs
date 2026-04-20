using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using B2B.Contracts;
using Microsoft.AspNetCore.Components;

namespace B2B.Admin.Services;

/// <summary>
/// 401 yanıtında refresh token ile erişim jetonunu yeniler ve isteği bir kez daha dener (gövde önbellekli).
/// Yenileme başarısızsa oturumu temizler ve giriş sayfasına yönlendirir; başarılı yenilemede bilgi toast’ı gösterir.
/// </summary>
public sealed class AdminAuthRefreshHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthSession _session;
    private readonly IHttpClientFactory _httpFactory;
    private readonly AdminUiNotify _uiNotify;
    private readonly NavigationManager _nav;

    public AdminAuthRefreshHandler(
        AuthSession session,
        IHttpClientFactory httpFactory,
        AdminUiNotify uiNotify,
        NavigationManager nav)
    {
        _session = session;
        _httpFactory = httpFactory;
        _uiNotify = uiNotify;
        _nav = nav;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[]? bodyBuffer = null;
        MediaTypeHeaderValue? contentType = null;
        if (request.Content is not null)
        {
            bodyBuffer = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            contentType = request.Content.Headers.ContentType;
            request.Content.Dispose();
            request.Content = new ByteArrayContent(bodyBuffer);
            if (contentType is not null)
                request.Content.Headers.ContentType = contentType;
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var path = request.RequestUri?.AbsolutePath ?? "";
        if (IsAnonymousAuthPath(path))
            return response;

        if (!await TryRefreshAsync(cancellationToken).ConfigureAwait(false))
        {
            await OnRefreshFailedAsync().ConfigureAwait(false);
            return response;
        }

        var token = await _session.GetAccessTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            await OnRefreshFailedAsync().ConfigureAwait(false);
            return response;
        }

        using (response)
        {
            var retry = CloneRequest(request, bodyBuffer, contentType);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var retryResponse = await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);

            if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                await OnRefreshFailedAsync("Oturum doğrulanamadı.").ConfigureAwait(false);
                return retryResponse;
            }

            _uiNotify.Raise(new AdminToast(
                "Oturum",
                "Oturumunuz yenilendi; işleminize devam ediliyor.",
                AdminToastKind.Info));

            return retryResponse;
        }
    }

    private async Task OnRefreshFailedAsync(string? detail = null)
    {
        try
        {
            await _session.ClearAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        var msg = detail ?? "Oturum süresi doldu veya yenilenemedi.";
        _uiNotify.Raise(new AdminToast("Oturum gerekli", msg, AdminToastKind.Warning));

        try
        {
            _nav.NavigateTo("/login?session=expired", forceLoad: false);
        }
        catch
        {
            // navigasyon (ör. prerender) — toast yine gösterilir
        }
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refresh = await _session.GetRefreshTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(refresh))
            return false;

        var http = _httpFactory.CreateClient("apiInternal");
        http.DefaultRequestHeaders.Authorization = null;

        using var resp = await http.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequestBody(refresh.Trim()),
            cancellationToken).ConfigureAwait(false);

        var text = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text) || !resp.IsSuccessStatusCode)
            return false;

        ApiResponse<AuthTokens>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ApiResponse<AuthTokens>>(text, JsonOptions);
        }
        catch
        {
            return false;
        }

        if (parsed is not { Success: true, Data: { } data }
            || string.IsNullOrWhiteSpace(data.AccessToken)
            || string.IsNullOrWhiteSpace(data.RefreshToken))
            return false;

        await _session.SetAccessTokenAsync(data.AccessToken).ConfigureAwait(false);
        await _session.SetRefreshTokenAsync(data.RefreshToken).ConfigureAwait(false);
        return true;
    }

    private static HttpRequestMessage CloneRequest(
        HttpRequestMessage original,
        byte[]? bodyBuffer,
        MediaTypeHeaderValue? contentType)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var h in original.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        if (bodyBuffer is not null)
        {
            clone.Content = new ByteArrayContent(bodyBuffer);
            if (contentType is not null)
                clone.Content.Headers.ContentType = contentType;
        }

        return clone;
    }

    private static bool IsAnonymousAuthPath(string path)
    {
        if (path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.Contains("/auth/register", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.Contains("/auth/change-password", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private sealed record RefreshRequestBody(string RefreshToken);
    private sealed record AuthTokens(string AccessToken, string RefreshToken);
}
