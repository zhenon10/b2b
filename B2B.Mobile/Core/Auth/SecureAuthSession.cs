namespace B2B.Mobile.Core.Auth;

public sealed class SecureAuthSession : IAuthSession
{
    private const string TokenKey = "auth.access_token";

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

    public Task ClearAsync()
    {
        try { SecureStorage.Default.Remove(TokenKey); } catch { /* ignored */ }
        return Task.CompletedTask;
    }
}

