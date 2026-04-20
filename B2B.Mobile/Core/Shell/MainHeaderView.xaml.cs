using Microsoft.Extensions.DependencyInjection;

namespace B2B.Mobile.Core.Shell;

public partial class MainHeaderView : ContentView
{
    private ContentPage? _hostPage;
    private EventHandler? _hostAppearing;

    public static readonly BindableProperty LeftCaptionProperty = BindableProperty.Create(
        nameof(LeftCaption),
        typeof(string),
        typeof(MainHeaderView),
        default(string));

    public string? LeftCaption
    {
        get => (string?)GetValue(LeftCaptionProperty);
        set => SetValue(LeftCaptionProperty, value);
    }

    public MainHeaderView()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<MainHeaderViewModel>();
        OfflineBanner.BindingContext = vm;
        OfflineBanner.SetBinding(
            IsVisibleProperty,
            new Binding(nameof(MainHeaderViewModel.IsOffline), mode: BindingMode.OneWay));
        ConstrainedBanner.BindingContext = vm;
        ConstrainedBanner.SetBinding(
            IsVisibleProperty,
            new Binding(nameof(MainHeaderViewModel.ShowConstrainedHint), mode: BindingMode.OneWay));

        UsdLabel.BindingContext = vm;
        UsdLabel.SetBinding(Label.TextProperty, new Binding(nameof(MainHeaderViewModel.UsdTryDisplay)));

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_hostPage is not null)
            return;

        _hostPage = GetHostContentPage(this);
        if (_hostPage is null)
            return;

        _hostAppearing = (_, _) =>
            _ = App.Services.GetRequiredService<MainHeaderViewModel>().RefreshUsdTryAsync();
        _hostPage.Appearing += _hostAppearing;
        _ = App.Services.GetRequiredService<MainHeaderViewModel>().RefreshUsdTryAsync();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        DetachHost();
    }

    private void DetachHost()
    {
        if (_hostPage is not null && _hostAppearing is not null)
            _hostPage.Appearing -= _hostAppearing;

        _hostPage = null;
        _hostAppearing = null;
    }

    private static ContentPage? GetHostContentPage(Element? el)
    {
        while (el is not null)
        {
            if (el is ContentPage p)
                return p;
            el = el.Parent;
        }

        return null;
    }
}
