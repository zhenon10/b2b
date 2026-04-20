namespace B2B.Mobile.Core;

/// <summary>Maui <see cref="Microsoft.Maui.Storage.Preferences"/> anahtarları.</summary>
public static class MobilePreferenceKeys
{
    public const string ApiBaseUrlOverride = "b2b.api_base_url";
    public const string CartLinesV1 = "b2b.cart.lines.v1";
    /// <summary>Uygulama arka plandan dönünce PIN ile kilitleme.</summary>
    public const string ResumeLockEnabled = "b2b.resume_lock_enabled";
}
