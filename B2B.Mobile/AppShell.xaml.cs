using B2B.Mobile.Core.Auth;
using B2B.Mobile.Core.Shell;
using B2B.Mobile.Features.Auth.Views;
using B2B.Mobile.Features.AdminNotifications.Views;
using B2B.Mobile.Features.Cari.Views;
using B2B.Mobile.Features.Products.ViewModels;
using B2B.Mobile.Features.Orders.Views;
using B2B.Mobile.Features.Products.Views;
using B2B.Mobile.Features.Notifications.Views;

namespace B2B.Mobile;

public partial class AppShell : Shell
{
    private readonly MainFlyoutViewModel _mainFlyoutVm;
    private readonly IAuthSession _authSession;

    public AppShell(
        CategoriesFlyoutViewModel categoriesFlyout,
        MainFlyoutViewModel mainFlyoutVm,
        AppShellViewModel shellVm,
        IAuthSession authSession)
    {
        InitializeComponent();

        _mainFlyoutVm = mainFlyoutVm;
        _authSession = authSession;
        BindingContext = shellVm;
        MainFlyoutRoot.SetCategoriesBindingContext(categoriesFlyout);
        MainFlyoutRoot.BindingContext = mainFlyoutVm;

        Navigated += OnNavigated;
        UpdateFlyoutBehaviorForRoute(CurrentState?.Location?.OriginalString);
        Dispatcher.Dispatch(async () =>
        {
            await Task.Yield();
            await UpdateAdminTabVisibilityAsync();
            await _mainFlyoutVm.SyncTabAsync(GetResolvedMainTabRoute());
            await EnforceAdminRoutePolicyAsync();
        });

        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("productDetail", typeof(ProductDetailPage));
        Routing.RegisterRoute("productScan", typeof(ProductScanPage));
        Routing.RegisterRoute("productEdit", typeof(ProductEditPage));
        Routing.RegisterRoute("categoryAdmin", typeof(CategoryAdminPage));
        Routing.RegisterRoute("categoryEdit", typeof(CategoryEditPage));
        Routing.RegisterRoute("pendingDealers", typeof(PendingDealersPage));
        Routing.RegisterRoute("adminOrders", typeof(AdminOrdersPage));
        Routing.RegisterRoute("adminBroadcast", typeof(AdminNotificationComposerPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("notifications", typeof(NotificationsPage));
        Routing.RegisterRoute("cariEntries", typeof(CariEntriesPage));
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateFlyoutBehaviorForRoute(e.Current?.Location?.OriginalString);
        Dispatcher.Dispatch(async () =>
        {
            UpdateFlyoutBehaviorForRoute(CurrentState?.Location?.OriginalString);
            await UpdateAdminTabVisibilityAsync();
            await _mainFlyoutVm.SyncTabAsync(GetResolvedMainTabRoute());
            await EnforceAdminRoutePolicyAsync();
        });
    }

    /// <summary>
    /// Bayi veya süresi dolmuş JWT ile admin sekmesi / modal admin sayfalarına düşülürse ürünlere yönlendir.
    /// </summary>
    private async Task EnforceAdminRoutePolicyAsync()
    {
        try
        {
            var token = await _authSession.GetAccessTokenAsync();
            if (JwtRoleReader.IsAdmin(token))
                return;

            var loc = CurrentState?.Location?.OriginalString;
            if (!AdminRouteGuard.UriLooksLikeAdminOnly(loc))
                return;

            await GoToAsync("//main/products", animate: false);
            await _mainFlyoutVm.SyncTabAsync("products");
        }
        catch
        {
            // Shell geçişi başarısız: yoksay
        }
    }

    private async Task UpdateAdminTabVisibilityAsync()
    {
        try
        {
            var token = await _authSession.GetAccessTokenAsync();
            AdminShellTab.IsVisible = JwtRoleReader.IsAdmin(token);
        }
        catch
        {
            AdminShellTab.IsVisible = false;
        }
    }

    /// <summary>Yalnızca giriş yapıldıktan sonra (TabBar route=main) sol kategori menüsünü göster.</summary>
    /// <remarks>
    /// Sadece URI'ye bakmak yetmez: Shell bazen <c>e.Current.Location</c> içinde <c>//main</c> göndermez
    /// (ör. sekme içi göreli yollar). <see cref="Shell.CurrentItem"/> route <c>main</c> iken flyout açık kalmalı.
    /// </remarks>
    private void UpdateFlyoutBehaviorForRoute(string? location)
    {
        var loc = location ?? "";
        var onMain = string.Equals(CurrentItem?.Route, "main", StringComparison.OrdinalIgnoreCase)
            || loc.Contains("//main", StringComparison.OrdinalIgnoreCase)
            || loc.Contains("/main/", StringComparison.OrdinalIgnoreCase);
        FlyoutBehavior = onMain ? FlyoutBehavior.Flyout : FlyoutBehavior.Disabled;
    }

    /// <summary>TabBar’da seçili <see cref="ShellContent"/> rotası (products, cart, order, profile).</summary>
    private string? GetResolvedMainTabRoute()
    {
        try
        {
            var r = CurrentItem?.CurrentItem?.Route;
            if (!string.IsNullOrEmpty(r) && !string.Equals(r, "main", StringComparison.OrdinalIgnoreCase))
                return r;
        }
        catch
        {
            // Shell hiyerarşisi beklenmedik olabilir; konuma düş.
        }

        var loc = CurrentState?.Location?.OriginalString ?? "";
        foreach (var seg in new[] { "products", "cart", "order", "cari", "adminHub", "profile" })
        {
            if (loc.Contains($"/main/{seg}", StringComparison.OrdinalIgnoreCase)
                || loc.Contains($"//main/{seg}", StringComparison.OrdinalIgnoreCase)
                || loc.Contains($"/{seg}/", StringComparison.OrdinalIgnoreCase))
                return seg;
        }

        return "products";
    }
}
