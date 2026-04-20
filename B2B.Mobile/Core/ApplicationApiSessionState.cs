namespace B2B.Mobile.Core;

/// <summary>Uygulama soğuk başlatıldığında çözümlenen API kökü (HttpClient ile aynı).</summary>
public sealed class ApplicationApiSessionState
{
    public ApplicationApiSessionState(string sessionResolvedBaseUrl) =>
        SessionResolvedBaseUrl = sessionResolvedBaseUrl;

    public string SessionResolvedBaseUrl { get; }
}
