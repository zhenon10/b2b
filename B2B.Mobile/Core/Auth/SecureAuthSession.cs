namespace B2B.Mobile.Core.Auth;

public sealed class SecureAuthSession : IAuthSession
{
    private const string TokenKey = "auth.access_token";
    private const string RefreshKey = "auth.refresh_token";

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAccessTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                SecureStorage.Default.Remove(TokenKey);
                return;
            }

            await SecureStorage.Default.SetAsync(TokenKey, token);
        }
        catch
        {
            // ignored (device may not support secure storage)
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(RefreshKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetRefreshTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                SecureStorage.Default.Remove(RefreshKey);
                return;
            }

            await SecureStorage.Default.SetAsync(RefreshKey, token);
        }
        catch
        {
            // ignored
        }
    }

    public Task ClearAsync()
    {
        try { SecureStorage.Default.Remove(TokenKey); } catch { /* ignored */ }
        try { SecureStorage.Default.Remove(RefreshKey); } catch { /* ignored */ }
        return Task.CompletedTask;
    }
}

