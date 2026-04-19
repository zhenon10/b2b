namespace B2B.Mobile.Core.Auth;

/// <summary>
/// Bir sonraki giriş ekranı gösteriminde tek seferlik bilgi bandı (Shell sorgu parametresi yerine basit durum).
/// </summary>
public sealed class LoginPresentationState
{
    private string? _infoBanner;

    public void SetInfoBannerForNextLogin(string? message) => _infoBanner = message;

    public string? TryConsumeInfoBanner()
    {
        var m = _infoBanner;
        _infoBanner = null;
        return m;
    }
}
