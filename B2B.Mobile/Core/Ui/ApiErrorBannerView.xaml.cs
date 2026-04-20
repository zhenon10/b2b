using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;

namespace B2B.Mobile.Core.Ui;

public partial class ApiErrorBannerView : ContentView
{
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message),
        typeof(string),
        typeof(ApiErrorBannerView),
        default(string));

    public static readonly BindableProperty TraceIdProperty = BindableProperty.Create(
        nameof(TraceId),
        typeof(string),
        typeof(ApiErrorBannerView),
        default(string));

    public ApiErrorBannerView() => InitializeComponent();

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string? TraceId
    {
        get => (string?)GetValue(TraceIdProperty);
        set => SetValue(TraceIdProperty, value);
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TraceId))
            return;
        await Clipboard.Default.SetTextAsync(TraceId);
        var page = global::Microsoft.Maui.Controls.Shell.Current?.CurrentPage;
        if (page is not null)
            await page.DisplayAlertAsync("Panoya kopyalandı", "Destek kodu panoya alındı.", "Tamam");
    }
}
