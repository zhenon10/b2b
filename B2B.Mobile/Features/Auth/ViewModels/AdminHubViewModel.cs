using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.AdminNotifications.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class AdminHubViewModel : ObservableObject
{
    private readonly IAuthSession _session;
    private readonly AdminNotificationsService _adminNotifications;

    public AdminHubViewModel(IAuthSession session, AdminNotificationsService adminNotifications)
    {
        _session = session;
        _adminNotifications = adminNotifications;
    }

    private async Task EnsureAdminThenAsync(Func<Task> go)
    {
        var token = await _session.GetAccessTokenAsync();
        if (JwtRoleReader.IsAdmin(token))
        {
            await go();
            return;
        }

        var page = Shell.Current?.CurrentPage;
        if (page is not null)
        {
            await page.DisplayAlertAsync(
                "Erişim yok",
                "Bu işlem yalnızca yöneticiler içindir.",
                "Tamam");
        }
    }

    [RelayCommand]
    private Task OpenCategoriesAsync() =>
        EnsureAdminThenAsync(() => Shell.Current.GoToAsync("categoryAdmin"));

    [RelayCommand]
    private Task OpenNewProductAsync() =>
        EnsureAdminThenAsync(() => Shell.Current.GoToAsync("productEdit"));

    [RelayCommand]
    private Task OpenPendingDealersAsync() =>
        EnsureAdminThenAsync(() => Shell.Current.GoToAsync("pendingDealers"));

    [RelayCommand]
    private Task OpenAdminOrdersAsync() =>
        EnsureAdminThenAsync(() => Shell.Current.GoToAsync("adminOrders"));

    [RelayCommand]
    private Task OpenBroadcastNotificationsAsync() =>
        EnsureAdminThenAsync(() => Shell.Current.GoToAsync("adminBroadcast"));

    [RelayCommand]
    private Task ClearAllNotificationsAsync() =>
        EnsureAdminThenAsync(async () =>
        {
            var page = Shell.Current?.CurrentPage;
            if (page is null) return;

            var ok = await page.DisplayAlertAsync(
                "Tüm bildirimleri sil",
                "Tüm kullanıcıların bildirim geçmişi silinecek. Bu işlem geri alınamaz.",
                "Sil",
                "Vazgeç");
            if (!ok) return;

            var resp = await _adminNotifications.ClearAllAsync(CancellationToken.None);
            if (!resp.Success)
            {
                await page.DisplayAlertAsync("Hata", resp.Error?.Message ?? "Silme işlemi başarısız.", "Tamam");
                return;
            }

            await page.DisplayAlertAsync("Tamam", "Bildirimler silindi.", "Tamam");
        });
}
