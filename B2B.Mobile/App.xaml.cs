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
                    await Shell.Current.DisplayAlert("Beklenmeyen hata", ex.Message, "Tamam");
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
                    await Shell.Current.DisplayAlert("Beklenmeyen hata", ex.Message, "Tamam");
                });
                e.SetObserved();
            }
            catch { }
        };
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(Services.GetRequiredService<AppShell>());
	}
}