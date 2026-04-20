namespace B2B.Mobile.Core.Auth;

/// <summary>401 sonrası refresh token ile erişim jetonunu yeniler.</summary>
public interface IAccessTokenRefresher
{
    Task<bool> TryRefreshAsync(CancellationToken ct);
}
