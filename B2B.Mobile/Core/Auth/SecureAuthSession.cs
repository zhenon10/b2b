using B2B.Mobile.Core;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Core.Auth;

public sealed class SecureAuthSession : IAuthSession
{
    private const string TokenKey = "auth.access_token";
    private const string RefreshKey = "auth.refresh_token";

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }
        catch
        {
            // fall back to Preferences
        }

        try
        {
            var token = Preferences.Default.Get(MobilePreferenceKeys.AuthAccessTokenFallback, "");
            return string.IsNullOrWhiteSpace(token) ? null : token;
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
                try { Preferences.Default.Remove(MobilePreferenceKeys.AuthAccessTokenFallback); } catch { }
                return;
            }

            await SecureStorage.Default.SetAsync(TokenKey, token);
            try { Preferences.Default.Remove(MobilePreferenceKeys.AuthAccessTokenFallback); } catch { }
        }
        catch
        {
            // SecureStorage may be unavailable on some OEMs; fall back to Preferences.
            try { Preferences.Default.Set(MobilePreferenceKeys.AuthAccessTokenFallback, token ?? ""); } catch { }
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(RefreshKey);
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }
        catch
        {
            // fall back to Preferences
        }

        try
        {
            var token = Preferences.Default.Get(MobilePreferenceKeys.AuthRefreshTokenFallback, "");
            return string.IsNullOrWhiteSpace(token) ? null : token;
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
                try { Preferences.Default.Remove(MobilePreferenceKeys.AuthRefreshTokenFallback); } catch { }
                return;
            }

            await SecureStorage.Default.SetAsync(RefreshKey, token);
            try { Preferences.Default.Remove(MobilePreferenceKeys.AuthRefreshTokenFallback); } catch { }
        }
        catch
        {
            try { Preferences.Default.Set(MobilePreferenceKeys.AuthRefreshTokenFallback, token ?? ""); } catch { }
        }
    }

    public Task ClearAsync()
    {
        try { SecureStorage.Default.Remove(TokenKey); } catch { /* ignored */ }
        try { SecureStorage.Default.Remove(RefreshKey); } catch { /* ignored */ }
        try { Preferences.Default.Remove(MobilePreferenceKeys.AuthAccessTokenFallback); } catch { /* ignored */ }
        try { Preferences.Default.Remove(MobilePreferenceKeys.AuthRefreshTokenFallback); } catch { /* ignored */ }
        return Task.CompletedTask;
    }
}

