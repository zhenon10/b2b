using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace B2B.Admin.Services;

public sealed class AuthSession
{
    private const string TokenKey = "access_token";
    private const string RefreshKey = "refresh_token";
    private readonly ProtectedSessionStorage _storage;

    public AuthSession(ProtectedSessionStorage storage)
    {
        _storage = storage;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var result = await _storage.GetAsync<string>(TokenKey);
        return result.Success ? result.Value : null;
    }

    public async Task SetAccessTokenAsync(string token) =>
        await _storage.SetAsync(TokenKey, token);

    public async Task<string?> GetRefreshTokenAsync()
    {
        var result = await _storage.GetAsync<string>(RefreshKey);
        return result.Success ? result.Value : null;
    }

    public async Task SetRefreshTokenAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            await _storage.DeleteAsync(RefreshKey);
            return;
        }

        await _storage.SetAsync(RefreshKey, token);
    }

    public async Task ClearAsync()
    {
        await _storage.DeleteAsync(TokenKey);
        await _storage.DeleteAsync(RefreshKey);
    }
}

