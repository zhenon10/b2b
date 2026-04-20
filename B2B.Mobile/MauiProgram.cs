using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using B2B.Mobile.Core;
using B2B.Mobile.Core.Api;
using B2B.Mobile.Core.Auth;
using B2B.Mobile.Core.Connectivity;
using B2B.Mobile.Core.Security;
using B2B.Mobile.Core.Finance;
using B2B.Mobile.Core.Shell;
using B2B.Mobile.Features.Auth.Services;
using B2B.Mobile.Features.Auth.ViewModels;
using B2B.Mobile.Features.Auth.Views;
using B2B.Mobile.Features.Cart.Services;
using B2B.Mobile.Features.Cart.ViewModels;
using B2B.Mobile.Features.Cart.Views;
using B2B.Mobile.Features.Orders.Services;
using B2B.Mobile.Features.Orders.ViewModels;
using B2B.Mobile.Features.Orders.Views;
using B2B.Mobile.Features.Products.Services;
using B2B.Mobile.Features.Products.ViewModels;
using B2B.Mobile.Features.Products.Views;
using ZXing.Net.Maui.Controls;

namespace B2B.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

        var appDir = AppContext.BaseDirectory;
        builder.Configuration
            .AddJsonFile(Path.Combine(appDir, "appsettings.json"), optional: true, reloadOnChange: false);
#if DEBUG
        builder.Configuration.AddJsonFile(Path.Combine(appDir, "appsettings.Development.json"), optional: true, reloadOnChange: false);
#else
        builder.Configuration.AddJsonFile(Path.Combine(appDir, "appsettings.Production.json"), optional: true, reloadOnChange: false);
#endif
        builder.Configuration.AddEnvironmentVariables("B2B_");

		builder
			.UseMauiApp<App>()
            .UseBarcodeReader()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
			})
			.ConfigureMauiHandlers(h =>
			{
#if ANDROID
				h.AddHandler<Shell, Platforms.Android.B2BShellRenderer>();
#elif IOS
				h.AddHandler<Shell, Platforms.iOS.B2BShellRenderer>();
#elif MACCATALYST
				h.AddHandler<Shell, Platforms.MacCatalyst.B2BShellRenderer>();
#endif
			});

        var apiBaseUrl = ApiBaseUrlResolver.Resolve(builder.Configuration);
        builder.Services.AddSingleton(new ApplicationApiSessionState(apiBaseUrl));

        builder.Services.AddSingleton<IAuthSession, SecureAuthSession>();
        builder.Services.AddSingleton<IAccessTokenRefresher, AccessTokenRefresher>();
        builder.Services.AddSingleton<LoginPresentationState>();
        builder.Services.AddSingleton<ISessionSignOutHandler, ShellSessionSignOutHandler>();
        builder.Services.AddSingleton<CatalogNotifications>();
        builder.Services.AddSingleton<CartService>();

        builder.Services.AddHttpClient("api", http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            // Fail fast on unreachable API (Wi‑Fi / firewall / wrong port).
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddHttpClient("fx", http =>
        {
            http.BaseAddress = new Uri("https://api.frankfurter.app/");
            http.Timeout = TimeSpan.FromSeconds(12);
        });

        builder.Services.AddSingleton<ExchangeRateService>();
        builder.Services.AddSingleton<ConnectivityService>();
        builder.Services.AddSingleton<AppResumeLockService>();
        builder.Services.AddSingleton<MainHeaderViewModel>();
        builder.Services.AddSingleton<MainFlyoutViewModel>();
        builder.Services.AddSingleton<AppShellViewModel>();

        builder.Services.AddSingleton<ApiClient>(sp =>
            new ApiClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"),
                sp.GetRequiredService<IAuthSession>(),
                sp.GetRequiredService<IAccessTokenRefresher>(),
                sp.GetRequiredService<ISessionSignOutHandler>(),
                sp.GetRequiredService<ILogger<ApiClient>>()
            ));

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ProductCatalogFilter>();
        builder.Services.AddSingleton<CategoriesService>();
        builder.Services.AddSingleton<CategoriesFlyoutViewModel>();
        builder.Services.AddSingleton<ProductScanReturnBuffer>();
        builder.Services.AddSingleton<ProductsService>();
        builder.Services.AddSingleton<ImageUploadService>();
        builder.Services.AddSingleton<OrdersService>();
        builder.Services.AddSingleton<AdminOrdersService>();
        builder.Services.AddSingleton<AdminUsersService>();

        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<ProductsViewModel>();
        builder.Services.AddTransient<ProductDetailViewModel>();
        builder.Services.AddTransient<ProductEditViewModel>();
        builder.Services.AddTransient<CategoryAdminViewModel>();
        builder.Services.AddTransient<CategoryEditViewModel>();
        builder.Services.AddTransient<CartViewModel>();
        builder.Services.AddTransient<OrderViewModel>();
        builder.Services.AddTransient<AdminOrdersViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<AdminHubViewModel>();
        builder.Services.AddTransient<PendingDealersViewModel>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ProductsPage>();
        builder.Services.AddTransient<ProductDetailPage>();
        builder.Services.AddTransient<ProductEditPage>();
        builder.Services.AddTransient<CategoryAdminPage>();
        builder.Services.AddTransient<CategoryEditPage>();
        builder.Services.AddTransient<CartPage>();
        builder.Services.AddTransient<OrderPage>();
        builder.Services.AddTransient<AdminOrdersPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AdminHubPage>();
        builder.Services.AddTransient<PendingDealersPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
