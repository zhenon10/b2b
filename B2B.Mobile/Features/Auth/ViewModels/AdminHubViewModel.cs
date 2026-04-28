using B2B.Mobile.Core.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class AdminHubViewModel : ObservableObject
{
    private readonly IAuthSession _session;

    public AdminHubViewModel(IAuthSession session) => _session = session;

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
}
