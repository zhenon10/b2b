using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
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
using B2B.Mobile.Features.Notifications.Services;
using B2B.Mobile.Features.Notifications.ViewModels;
using B2B.Mobile.Features.Notifications.Views;
using ZXing.Net.Maui.Controls;
#if IOS
using Plugin.Firebase.Bundled.Platforms.iOS;
#elif ANDROID
using Plugin.Firebase.Bundled.Platforms.Android;
using Plugin.Firebase.CloudMessaging;
#endif
using Plugin.Firebase.Bundled.Shared;

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

        builder.ConfigureLifecycleEvents(events =>
        {
#if IOS
            events.AddiOS(iOS => iOS.WillFinishLaunching((_, __) =>
            {
                CrossFirebase.Initialize(CreateCrossFirebaseSettings());
                FirebaseCloudMessagingImplementation.Initialize();
                return false;
            }));
#elif ANDROID
            events.AddAndroid(android => android.OnCreate((activity, _) =>
            {
                CrossFirebase.Initialize(activity, () => Platform.CurrentActivity, CreateCrossFirebaseSettings());
            }));
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
            // Frankfurter: `api.frankfurter.app` no longer serves `/v1/...` (404). Use the current dev API host.
            http.BaseAddress = new Uri("https://api.frankfurter.dev/");
            http.Timeout = TimeSpan.FromSeconds(12);
        });

        builder.Services.AddHttpClient("altinapi", http =>
        {
            http.BaseAddress = new Uri("https://altinapi.com/api/v1/");
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
        builder.Services.AddSingleton<NotificationsService>();

#if IOS || ANDROID
        builder.Services.AddSingleton(_ => CrossFirebaseCloudMessaging.Current);
        builder.Services.AddSingleton<Core.Push.PushTokenSyncService>();
        builder.Services.AddSingleton<Core.Push.PushTokensService>();
#endif

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
        builder.Services.AddTransient<NotificationsViewModel>();

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
        builder.Services.AddTransient<NotificationsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

    private static CrossFirebaseSettings CreateCrossFirebaseSettings()
    {
        // We only need Cloud Messaging for now.
        return new CrossFirebaseSettings(
            isAnalyticsEnabled: false,
            isAuthEnabled: false,
            isCloudMessagingEnabled: true,
            isDynamicLinksEnabled: false,
            isFirestoreEnabled: false,
            isFunctionsEnabled: false,
            isRemoteConfigEnabled: false,
            isStorageEnabled: false,
            appCheckOptions: Plugin.Firebase.AppCheck.AppCheckOptions.Disabled,
            googleRequestIdToken: ""
        );
    }
}
