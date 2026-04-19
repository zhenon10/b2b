using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.Auth.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly ISessionSignOutHandler _signOut;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? successMessage;

    [ObservableProperty] private string profileEmail = "";
    [ObservableProperty] private string profileDisplayName = "";
    [ObservableProperty] private string profileRoles = "";
    [ObservableProperty] private string accountStatus = "";

    [ObservableProperty] private string currentPassword = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private string confirmPassword = "";

    public ProfileViewModel(AuthService auth, ISessionSignOutHandler signOut)
    {
        _auth = auth;
        _signOut = signOut;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        SuccessMessage = null;

        try
        {
            var resp = await _auth.GetProfileAsync(CancellationToken.None);
            if (!resp.Success || resp.Data is null)
            {
                Error = resp.Error?.Message ?? "Profil yüklenemedi.";
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
                Error = resp.Error?.Message ?? "Şifre değiştirilemedi.";
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
    private Task LogoutAsync() =>
        _signOut.SignOutAndNavigateToLoginAsync(CancellationToken.None);

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
