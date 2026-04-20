using System.Collections.Specialized;
using B2B.Mobile.Core;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Features.Cart.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using MauiShell = Microsoft.Maui.Controls.Shell;

namespace B2B.Mobile.Core.Shell;

public partial class MainFlyoutViewModel : ObservableObject
{
    public enum MainFlyoutTab
    {
        Products,
        Cart,
        Order,
        Admin,
        Profile
    }

    private readonly CartService _cart;
    private readonly IAuthSession _auth;
    private readonly CatalogNotifications _catalogEvents;

    [ObservableProperty] private MainFlyoutTab activeTab = MainFlyoutTab.Products;
    [ObservableProperty] private bool canShowAdminHub;
    [ObservableProperty] private string cartSummary = "Sepet boş.";

    public bool IsProductsFlyout => ActiveTab == MainFlyoutTab.Products;
    public bool IsCartFlyout => ActiveTab == MainFlyoutTab.Cart;
    public bool IsOrderFlyout => ActiveTab == MainFlyoutTab.Order;
    public bool IsAdminHubFlyout => ActiveTab == MainFlyoutTab.Admin;
    public bool IsProfileFlyout => ActiveTab == MainFlyoutTab.Profile;

    /// <summary>Kısayol satırında bulunduğunuz sekme için “kendine git” düğmesini göstermeyin.</summary>
    public bool QuickNavShowProducts => ActiveTab != MainFlyoutTab.Products;
    public bool QuickNavShowCart => ActiveTab != MainFlyoutTab.Cart;
    public bool QuickNavShowOrder => ActiveTab != MainFlyoutTab.Order;
    public bool QuickNavShowProfile => ActiveTab != MainFlyoutTab.Profile;
    public bool QuickNavShowAdminHub => CanShowAdminHub && ActiveTab != MainFlyoutTab.Admin;

    public MainFlyoutViewModel(CartService cart, IAuthSession auth, CatalogNotifications catalogEvents)
    {
        _cart = cart;
        _auth = auth;
        _catalogEvents = catalogEvents;
        if (_cart.Lines is INotifyCollectionChanged n)
            n.CollectionChanged += (_, __) => OnCartLinesChanged();
        _catalogEvents.SessionChanged += (_, __) =>
            MainThread.BeginInvokeOnMainThread(() => _ = RefreshAdminHubVisibilityAsync());
    }

    partial void OnActiveTabChanged(MainFlyoutTab value)
    {
        OnPropertyChanged(nameof(IsProductsFlyout));
        OnPropertyChanged(nameof(IsCartFlyout));
        OnPropertyChanged(nameof(IsOrderFlyout));
        OnPropertyChanged(nameof(IsAdminHubFlyout));
        OnPropertyChanged(nameof(IsProfileFlyout));
        RaiseQuickNavVisibility();
    }

    partial void OnCanShowAdminHubChanged(bool value) => RaiseQuickNavVisibility();

    private void RaiseQuickNavVisibility()
    {
        OnPropertyChanged(nameof(QuickNavShowProducts));
        OnPropertyChanged(nameof(QuickNavShowCart));
        OnPropertyChanged(nameof(QuickNavShowOrder));
        OnPropertyChanged(nameof(QuickNavShowProfile));
        OnPropertyChanged(nameof(QuickNavShowAdminHub));
    }

    private void OnCartLinesChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (ActiveTab == MainFlyoutTab.Cart)
                RefreshCartSummary();
        });
    }

    /// <summary>Shell’deki aktif sekme rotası: products, cart, order, adminHub, profile.</summary>
    public async Task SyncTabAsync(string? route)
    {
        ActiveTab = NormalizeRoute(route);
        if (ActiveTab == MainFlyoutTab.Cart)
            RefreshCartSummary();
        await RefreshAdminHubVisibilityAsync();
    }

    private async Task RefreshAdminHubVisibilityAsync()
    {
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            CanShowAdminHub = JwtRoleReader.IsAdmin(token);
        }
        catch
        {
            CanShowAdminHub = false;
        }
    }

    private void RefreshCartSummary()
    {
        if (_cart.Lines.Count == 0)
        {
            CartSummary = "Sepet boş.";
            return;
        }

        CartSummary = $"{_cart.Lines.Count} kalem · Toplam {_cart.Total:0.##}";
    }

    private static MainFlyoutTab NormalizeRoute(string? route)
    {
        return route?.Trim().ToLowerInvariant() switch
        {
            "cart" => MainFlyoutTab.Cart,
            "order" => MainFlyoutTab.Order,
            "adminhub" => MainFlyoutTab.Admin,
            "profile" => MainFlyoutTab.Profile,
            _ => MainFlyoutTab.Products
        };
    }

    [RelayCommand]
    private static async Task GoToProductsAsync()
    {
        MauiShell.Current.FlyoutIsPresented = false;
        await MauiShell.Current.GoToAsync("//main/products");
    }

    [RelayCommand]
    private static async Task GoToCartAsync()
    {
        MauiShell.Current.FlyoutIsPresented = false;
        await MauiShell.Current.GoToAsync("//main/cart");
    }

    [RelayCommand]
    private static async Task GoToOrderAsync()
    {
        MauiShell.Current.FlyoutIsPresented = false;
        await MauiShell.Current.GoToAsync("//main/order");
    }

    [RelayCommand]
    private async Task GoToAdminHubAsync()
    {
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            if (!JwtRoleReader.IsAdmin(token))
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var page = MauiShell.Current?.CurrentPage;
                    if (page is not null)
                    {
                        await page.DisplayAlertAsync(
                            "Erişim yok",
                            "Yönetim alanı yalnızca yöneticiler içindir.",
                            "Tamam");
                    }
                });
                return;
            }

            MauiShell.Current.FlyoutIsPresented = false;
            await MauiShell.Current.GoToAsync("//main/adminHub");
        }
        catch
        {
            // Shell / iletişim kutusu hatası
        }
    }

    [RelayCommand]
    private static async Task GoToProfileAsync()
    {
        MauiShell.Current.FlyoutIsPresented = false;
        await MauiShell.Current.GoToAsync("//main/profile");
    }
}
