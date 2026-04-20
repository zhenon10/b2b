using B2B.Contracts;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Auth;

namespace B2B.Mobile.Features.Auth.Services;

public sealed class AuthService
{
    private readonly ApiClient _api;
    private readonly IAuthSession _session;

    public AuthService(ApiClient api, IAuthSession session)
    {
        _api = api;
        _session = session;
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct)
    {
        var resp = await _api.PostAsync<LoginRequest, AuthResponse>("/api/v1/auth/login", new(email, password), ct);
        if (resp.Success && resp.Data is not null)
        {
            await _session.SetAccessTokenAsync(resp.Data.AccessToken);
            await _session.SetRefreshTokenAsync(resp.Data.RefreshToken);
        }

        return resp;
    }

    public async Task<ApiResponse<RegisterResponse>> RegisterAsync(string email, string password, string? displayName, CancellationToken ct)
    {
        var resp = await _api.PostAsync<RegisterRequest, RegisterResponse>("/api/v1/auth/register", new(email, password, displayName), ct);
        if (resp.Success && resp.Data is not null && !string.IsNullOrWhiteSpace(resp.Data.AccessToken))
        {
            await _session.SetAccessTokenAsync(resp.Data.AccessToken);
            if (!string.IsNullOrWhiteSpace(resp.Data.RefreshToken))
                await _session.SetRefreshTokenAsync(resp.Data.RefreshToken);
        }

        return resp;
    }

    public Task LogoutAsync() => _session.ClearAsync();

    public Task<ApiResponse<ProfileResponse>> GetProfileAsync(CancellationToken ct) =>
        ApiTransientRetry.ExecuteAsync(() => _api.GetAsync<ProfileResponse>("/api/v1/auth/me", ct), ct);

    public Task<ApiResponse<object>> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct) =>
        _api.PostAsync<ChangePasswordRequest, object>(
            "/api/v1/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword),
            ct);
}
