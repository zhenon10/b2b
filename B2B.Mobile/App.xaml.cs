using B2B.Mobile.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

	public App(IServiceProvider services)
	{
		InitializeComponent();
        Services = services;

        // Surface unexpected managed exceptions on-device instead of "crash".
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                if (ex is null) return;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ShowUnexpectedErrorAlertAsync(ex.Message);
                });
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                var ex = e.Exception;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ShowUnexpectedErrorAlertAsync(ex.Message);
                });
                e.SetObserved();
            }
            catch { }
        };
	}

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        _ = AppLinkHandler.TryNavigateFromUriAsync(uri, Services);
    }

	protected override Window CreateWindow(IActivationState? activationState)
	{
        var window = new Window(Services.GetRequiredService<AppShell>());
        var resumeLock = Services.GetRequiredService<AppResumeLockService>();
        window.Deactivated += (_, _) => resumeLock.MarkPaused();
        window.Resumed += (_, _) => _ = resumeLock.OnAppResumedAsync();
		return window;
	}

    private static async Task ShowUnexpectedErrorAlertAsync(string message)
    {
        if (Shell.Current is { } shell)
        {
            await shell.DisplayAlert("Beklenmeyen hata", message, "Tamam");
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
            await page.DisplayAlert("Beklenmeyen hata", message, "Tamam");
    }
}