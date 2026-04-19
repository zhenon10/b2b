namespace B2B.Mobile.Core.Auth;

/// <summary>
/// Giriş ekranına dönüş nedenine göre isteğe bağlı kullanıcı bilgisi.
/// </summary>
public enum LoginSessionEndKind
{
    /// <summary>Çıkış, şifre sonrası, kayıttan «Girişe dön» vb.</summary>
    Standard,

    /// <summary>API 401 — oturum süresi veya geçersiz token.</summary>
    SessionExpired,
}
