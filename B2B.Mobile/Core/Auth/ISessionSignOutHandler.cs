namespace B2B.Mobile.Core.Auth;

/// <summary>
/// Token temizliği, oturum bildirimi ve giriş ekranına dönüş (401, çıkış, şifre sonrası vb.).
/// </summary>
public interface ISessionSignOutHandler
{
    Task SignOutAndNavigateToLoginAsync(
        CancellationToken ct = default,
        LoginSessionEndKind endKind = LoginSessionEndKind.Standard);
}
