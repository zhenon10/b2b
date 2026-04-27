using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Core;

/// <summary>
/// API kök adresi çözüm sırası:
/// 1) Ortam <c>B2B_API_BASE</c>
/// 2) Android manifest meta-data <c>B2B_API_BASE</c> (<c>b2b_api.xml</c>)
/// 3) Uygulama tercihi <see cref="MobilePreferenceKeys.ApiBaseUrlOverride"/> (Ayarlar)
/// 4) Yapılandırma <c>Api:BaseUrl</c> (appsettings + <c>B2B__Api__BaseUrl</c> ortam değişkeni)
/// 5) DEBUG: sabit LAN varsayılanı; RELEASE: yapılandırma zorunlu
/// </summary>
public static class ApiBaseUrlResolver
{
    private const string EnvVarName = "B2B_API_BASE";
    private const string AndroidMetaKey = "B2B_API_BASE";
    private const string ConfigKey = "Api:BaseUrl";

    /// <summary>Kullanıcı girişini kök URL’ye çevirir (sonuna <c>/</c> ekler).</summary>
    public static bool TryNormalizeUserBaseUrl(string? raw, out string normalized) =>
        TryParseBase(raw, out normalized);

    public static string Resolve(IConfiguration configuration)
    {
        if (TryParseBase(Environment.GetEnvironmentVariable(EnvVarName), out var fromEnv))
            return fromEnv;

#if ANDROID
        if (TryAndroidMeta(out var fromMeta))
            return fromMeta;
#endif

        if (TryPreferencesOverride(out var fromPrefs))
            return fromPrefs;

        if (TryParseBase(configuration[ConfigKey], out var fromCfg))
            return fromCfg;

#if DEBUG
        return Normalize("http://100.125.160.95:8080");
#else
        throw new InvalidOperationException(
            "Api:BaseUrl yapılandırılmadı. appsettings.Production.json, ortam B2B_API_BASE veya B2B__Api__BaseUrl, ya da Android meta-data kullanın.");
#endif
    }

    private static bool TryPreferencesOverride(out string normalized)
    {
        normalized = "";
        try
        {
            var v = Preferences.Default.Get(MobilePreferenceKeys.ApiBaseUrlOverride, "");
            return TryParseBase(v, out normalized);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseBase(string? raw, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        normalized = Normalize(raw);
        return true;
    }

    private static string Normalize(string url)
    {
        var t = url.Trim();
        if (t.Length == 0)
            return "http://127.0.0.1/";
        return t.EndsWith('/') ? t : t + "/";
    }

#if ANDROID
    private static bool TryAndroidMeta(out string normalized)
    {
        normalized = "";
        try
        {
            var ctx = global::Android.App.Application.Context;
            if (ctx is null) return false;

            var flags = global::Android.Content.PM.PackageInfoFlags.MetaData;
            var info = ctx.PackageManager?.GetApplicationInfo(ctx.PackageName!, flags);
            var bundle = info?.MetaData;
            if (bundle is null || !bundle.ContainsKey(AndroidMetaKey))
                return false;

            var s = bundle.GetString(AndroidMetaKey);
            return TryParseBase(s, out normalized);
        }
        catch
        {
            return false;
        }
    }
#endif
}
