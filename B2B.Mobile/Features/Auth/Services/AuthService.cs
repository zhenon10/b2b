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

    public sealed record LoginRequest(string Email, string Password);
    public sealed record LoginResponse(string AccessToken);

    public sealed record RegisterRequest(string Email, string Password, string? DisplayName);
    public sealed record RegisterResponse(string? AccessToken, string Message);

    public sealed record ProfileResponse(
        Guid UserId,
        string Email,
        string? DisplayName,
        List<string>? Roles,
        DateTime? ApprovedAtUtc);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    // Backend endpoints are expected at:
    // - POST /api/v1/auth/login
    // - POST /api/v1/auth/register
    // - GET  /api/v1/auth/me
    // - POST /api/v1/auth/change-password
    public async Task<ApiResponse<LoginResponse>> LoginAsync(string email, string password, CancellationToken ct)
    {
        var resp = await _api.PostAsync<LoginRequest, LoginResponse>("/api/v1/auth/login", new(email, password), ct);
        if (resp.Success && resp.Data is not null)
            await _session.SetAccessTokenAsync(resp.Data.AccessToken);
        return resp;
    }

    public async Task<ApiResponse<RegisterResponse>> RegisterAsync(string email, string password, string? displayName, CancellationToken ct)
    {
        var resp = await _api.PostAsync<RegisterRequest, RegisterResponse>("/api/v1/auth/register", new(email, password, displayName), ct);
        if (resp.Success && resp.Data is not null && !string.IsNullOrWhiteSpace(resp.Data.AccessToken))
            await _session.SetAccessTokenAsync(resp.Data.AccessToken);
        return resp;
    }

    public Task LogoutAsync() => _session.ClearAsync();

    public Task<ApiResponse<ProfileResponse>> GetProfileAsync(CancellationToken ct) =>
        _api.GetAsync<ProfileResponse>("/api/v1/auth/me", ct);

    public Task<ApiResponse<object>> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct) =>
        _api.PostAsync<ChangePasswordRequest, object>(
            "/api/v1/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword),
            ct);
}

