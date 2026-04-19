using System.Threading;
using B2B.Mobile.Core;
using Microsoft.Maui.ApplicationModel;

namespace B2B.Mobile.Core.Auth;

public sealed class ShellSessionSignOutHandler : ISessionSignOutHandler
{
    private readonly IAuthSession _auth;
    private readonly CatalogNotifications _catalog;
    private readonly LoginPresentationState _loginPresentation;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ShellSessionSignOutHandler(
        IAuthSession auth,
        CatalogNotifications catalog,
        LoginPresentationState loginPresentation)
    {
        _auth = auth;
        _catalog = catalog;
        _loginPresentation = loginPresentation;
    }

    public async Task SignOutAndNavigateToLoginAsync(
        CancellationToken ct = default,
        LoginSessionEndKind endKind = LoginSessionEndKind.Standard)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _auth.ClearAsync().ConfigureAwait(false);
            _catalog.NotifySessionChanged();
        }
        finally
        {
            _gate.Release();
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (endKind == LoginSessionEndKind.SessionExpired)
                {
                    _loginPresentation.SetInfoBannerForNextLogin(
                        "Oturumunuz sona erdi veya geçersiz. Lütfen yeniden giriş yapın.");
                }

                var shell = Microsoft.Maui.Controls.Shell.Current;
                if (shell is null)
                    return;
                await shell.GoToAsync("//login", animate: false).ConfigureAwait(true);
            }
            catch
            {
                // Shell hazır değilse veya rota hatası: sessiz geç
            }
        }).ConfigureAwait(false);
    }
}
