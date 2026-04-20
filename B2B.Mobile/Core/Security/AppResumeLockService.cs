using B2B.Mobile.Core.Auth;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Core.Security;

/// <summary>Arka plandan dönüşte isteğe bağlı PIN doğrulaması (SecureStorage).</summary>
public sealed class AppResumeLockService
{
    public const string PinStorageKey = "b2b_resume_lock_pin";

    private readonly IAuthSession _auth;
    private readonly ISessionSignOutHandler _signOut;
    private DateTime _lastPausedUtc = DateTime.MinValue;
    private bool _resumePromptInFlight;

    public AppResumeLockService(IAuthSession auth, ISessionSignOutHandler signOut)
    {
        _auth = auth;
        _signOut = signOut;
    }

    public void MarkPaused() => _lastPausedUtc = DateTime.UtcNow;

    public async Task OnAppResumedAsync()
    {
        if (_resumePromptInFlight)
            return;

        if (_lastPausedUtc == DateTime.MinValue)
            return;

        try
        {
            if (!Preferences.Default.Get(MobilePreferenceKeys.ResumeLockEnabled, false))
                return;

            var storedPin = await SecureStorage.Default.GetAsync(PinStorageKey).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(storedPin))
                return;

            var token = await _auth.GetAccessTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
                return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = global::Microsoft.Maui.Controls.Shell.Current?.CurrentPage;
                if (page is null)
                    return;

                _resumePromptInFlight = true;
                try
                {
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        var pin = await page.DisplayPromptAsync(
                            "Uygulama kilidi",
                            "Devam etmek için PIN girin.",
                            accept: "Tamam",
                            cancel: "Çıkış yap",
                            placeholder: "",
                            maxLength: 8,
                            keyboard: Keyboard.Numeric,
                            initialValue: "");

                        if (pin is null)
                        {
                            await _signOut.SignOutAndNavigateToLoginAsync(CancellationToken.None);
                            return;
                        }

                        if (string.Equals(pin.Trim(), storedPin.Trim(), StringComparison.Ordinal))
                            return;

                        await page.DisplayAlertAsync("Hatalı PIN", "Tekrar deneyin.", "Tamam");
                    }

                    await page.DisplayAlertAsync(
                        "Çok fazla deneme",
                        "Güvenlik nedeniyle oturum kapatılıyor.",
                        "Tamam");
                    await _signOut.SignOutAndNavigateToLoginAsync(CancellationToken.None);
                }
                finally
                {
                    _resumePromptInFlight = false;
                }
            });
        }
        catch
        {
            _resumePromptInFlight = false;
        }
    }
}
