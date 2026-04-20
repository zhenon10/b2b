using B2B.Mobile.Core;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Core.Security;
using B2B.Mobile.Features.Auth.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly ISessionSignOutHandler _signOut;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? apiTraceId;
    [ObservableProperty] private string? successMessage;

    [ObservableProperty] private string profileEmail = "";
    [ObservableProperty] private string profileDisplayName = "";
    [ObservableProperty] private string profileRoles = "";
    [ObservableProperty] private string accountStatus = "";

    [ObservableProperty] private string currentPassword = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private string confirmPassword = "";

    [ObservableProperty] private bool resumeLockEnabled;
    [ObservableProperty] private string resumeLockPinDraft = "";

    public ProfileViewModel(AuthService auth, ISessionSignOutHandler signOut)
    {
        _auth = auth;
        _signOut = signOut;
    }

    private void LoadResumeLockPrefs()
    {
        try
        {
            ResumeLockEnabled = Preferences.Default.Get(MobilePreferenceKeys.ResumeLockEnabled, false);
        }
        catch
        {
            ResumeLockEnabled = false;
        }

        ResumeLockPinDraft = "";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        ApiTraceId = null;
        SuccessMessage = null;
        LoadResumeLockPrefs();

        try
        {
            var resp = await _auth.GetProfileAsync(CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Profil yüklenemedi.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                ProfileEmail = "";
                ProfileDisplayName = "";
                ProfileRoles = "";
                AccountStatus = "";
                return;
            }

            var p = resp.Data;
            ProfileEmail = p.Email;
            ProfileDisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? "—" : p.DisplayName;
            ProfileRoles = FormatRoles(p.Roles);
            AccountStatus = p.ApprovedAtUtc.HasValue ? "Hesap onaylı" : "Onay bekleniyor";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (IsBusy) return;
        Error = null;
        ApiTraceId = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        {
            Error = "Yeni şifre en az 8 karakter olmalıdır.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            Error = "Yeni şifre ile tekrarı eşleşmiyor.";
            return;
        }

        IsBusy = true;
        try
        {
            var resp = await _auth.ChangePasswordAsync(CurrentPassword, NewPassword, CancellationToken.None);
            if (!resp.Success)
            {
                Error = UserFacingApiMessage.Message(resp.Error, "Şifre değiştirilemedi.");
                ApiTraceId = string.IsNullOrWhiteSpace(resp.TraceId) ? null : resp.TraceId;
                return;
            }

            CurrentPassword = "";
            NewPassword = "";
            ConfirmPassword = "";
            SuccessMessage = null;

            await Shell.Current.DisplayAlertAsync(
                "Şifre güncellendi",
                "Güvenlik nedeniyle yeni şifrenizle tekrar giriş yapmalısınız.",
                "Tamam");

            await _signOut.SignOutAndNavigateToLoginAsync(CancellationToken.None);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveResumeLockAsync()
    {
        Error = null;
        ApiTraceId = null;
        try
        {
            if (ResumeLockEnabled)
            {
                var pin = (ResumeLockPinDraft ?? "").Trim();
                if (pin.Length < 4 || pin.Length > 8)
                {
                    Error = "PIN 4–8 karakter olmalıdır.";
                    return;
                }

                await SecureStorage.Default.SetAsync(AppResumeLockService.PinStorageKey, pin);
                Preferences.Default.Set(MobilePreferenceKeys.ResumeLockEnabled, true);
                ResumeLockPinDraft = "";
                await Shell.Current.DisplayAlertAsync("Kaydedildi", "Uygulama kilidi etkin. Arka plandan dönüşte PIN istenecek.", "Tamam");
            }
            else
            {
                Preferences.Default.Set(MobilePreferenceKeys.ResumeLockEnabled, false);
                SecureStorage.Default.Remove(AppResumeLockService.PinStorageKey);
                ResumeLockPinDraft = "";
                await Shell.Current.DisplayAlertAsync("Kaydedildi", "Uygulama kilidi kapatıldı.", "Tamam");
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private Task LogoutAsync() =>
        _signOut.SignOutAndNavigateToLoginAsync(CancellationToken.None);

    [RelayCommand]
    private static Task OpenSettingsAsync() => Shell.Current.GoToAsync("settings");

    private static string FormatRoles(IReadOnlyList<string>? roles)
    {
        if (roles is null || roles.Count == 0)
            return "—";

        return string.Join(", ", roles.Select(static r => r switch
        {
            "Admin" => "Yönetici",
            "Dealer" => "Bayi",
            _ => r
        }));
    }
}
