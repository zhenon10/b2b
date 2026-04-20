using System.Net.Http.Json;
using System.Text.Json;
using B2B.Contracts;
using B2B.Mobile.Core.Api;

namespace B2B.Mobile.Core.Auth;

public sealed class AccessTokenRefresher : IAccessTokenRefresher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IAuthSession _session;

    public AccessTokenRefresher(IHttpClientFactory httpFactory, IAuthSession session)
    {
        _httpFactory = httpFactory;
        _session = session;
    }

    public async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        var refresh = await _session.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refresh))
            return false;

        var http = _httpFactory.CreateClient("api");
        http.DefaultRequestHeaders.Authorization = null;

        using var resp = await http.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(refresh.Trim()),
            ct);

        var text = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(text) || !resp.IsSuccessStatusCode)
            return false;

        ApiResponse<AuthResponse>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ApiResponse<AuthResponse>>(text, JsonOptions);
        }
        catch
        {
            return false;
        }

        if (parsed is not { Success: true, Data: { } data }
            || string.IsNullOrWhiteSpace(data.AccessToken)
            || string.IsNullOrWhiteSpace(data.RefreshToken))
            return false;

        await _session.SetAccessTokenAsync(data.AccessToken);
        await _session.SetRefreshTokenAsync(data.RefreshToken);
        return true;
    }

}
