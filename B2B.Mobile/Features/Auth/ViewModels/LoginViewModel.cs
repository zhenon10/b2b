using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.Auth.Services;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private const string PrefRememberMe = "auth.remember_me";
    private const string PrefRememberedEmail = "auth.remembered_email";

    private readonly AuthService _auth;
    private readonly CatalogNotifications _catalogEvents;
    private readonly LoginPresentationState _loginPresentation;

    public LoginViewModel(
        AuthService auth,
        CatalogNotifications catalogEvents,
        LoginPresentationState loginPresentation)
    {
        _auth = auth;
        _catalogEvents = catalogEvents;
        _loginPresentation = loginPresentation;

        RememberMe = Preferences.Default.Get(PrefRememberMe, false);
        if (RememberMe)
            Email = Preferences.Default.Get(PrefRememberedEmail, "");
    }

    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private bool rememberMe;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    /// <summary>401 sonrası vb. tek seferlik bilgi (hata değil).</summary>
    [ObservableProperty] private string? infoBanner;

    /// <summary>Sayfa görünürken çağrılır; API’den gelen oturum sonu bandını yerleştirir.</summary>
    public void ApplyLoginPresentationHints() =>
        InfoBanner = _loginPresentation.TryConsumeInfoBanner();

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        InfoBanner = null;

        try
        {
            var resp = await _auth.LoginAsync(Email.Trim(), Password, CancellationToken.None);
            if (!resp.Success)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Giriş başarısız.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            if (RememberMe)
            {
                Preferences.Default.Set(PrefRememberMe, true);
                Preferences.Default.Set(PrefRememberedEmail, Email.Trim());
            }
            else
            {
                Preferences.Default.Set(PrefRememberMe, false);
                Preferences.Default.Remove(PrefRememberedEmail);
            }

            _catalogEvents.NotifySessionChanged();
            await Shell.Current.GoToAsync("//main/products");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task GoToRegisterAsync() => Shell.Current.GoToAsync("register");
}

