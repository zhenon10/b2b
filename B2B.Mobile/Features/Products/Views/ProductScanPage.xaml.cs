using B2B.Mobile.Features.Products.Services;
using Microsoft.Extensions.DependencyInjection;
using ZXing.Net.Maui;

namespace B2B.Mobile.Features.Products.Views;

public partial class ProductScanPage : ContentPage, IQueryAttributable
{
    private bool _handled;
    private string _returnTarget = "catalog";

    /// <summary>Ardışık karelerde aynı değer gelmeden kabul etme (SKU/Code128 vb. için).</summary>
    private string? _stableCandidate;
    private int _stableHits;
    private DateTimeOffset _lastFrameUtc;

    private const int RequiredStableHits = 2;
    private const int MaxGapMsResetStable = 800;

    public ProductScanPage()
    {
        InitializeComponent();

        // OneDimensional: EAN/UPC/Code128/Code39 vb.
        // TryInverted: parlak/hologram şerit yansıması olan etiketlerde 1D okumayı kolaylaştırır.
        Camera.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.OneDimensional | BarcodeFormat.QrCode | BarcodeFormat.DataMatrix,
            AutoRotate = true,
            TryHarder = true,
            TryInverted = true,
            Multiple = false
        };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("returnTo", out var raw) && raw is string s && !string.IsNullOrWhiteSpace(s))
            _returnTarget = s.Trim();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _handled = false;
        _stableCandidate = null;
        _stableHits = 0;
        _lastFrameUtc = DateTimeOffset.MinValue;
        Camera.IsTorchOn = false;
        TorchButton.Text = "Fener";

        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(400);
            if (!IsLoaded || _handled) return;
            try
            {
                Camera.AutoFocus();
            }
            catch
            {
                // Bazı cihazlarda kamera henüz hazır değilse yok say.
            }
        });
    }

    private void OnGuideTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            Camera.AutoFocus();
        }
        catch
        {
            // ignore
        }
    }

    private bool TryConfirmStableRead(string code, out string confirmed)
    {
        confirmed = "";
        var trimmed = code.Trim();
        if (trimmed.Length < 2)
            return false;

        var now = DateTimeOffset.UtcNow;
        if (_lastFrameUtc != DateTimeOffset.MinValue &&
            (now - _lastFrameUtc).TotalMilliseconds > MaxGapMsResetStable)
        {
            _stableCandidate = null;
            _stableHits = 0;
        }

        _lastFrameUtc = now;

        if (string.Equals(_stableCandidate, trimmed, StringComparison.Ordinal))
            _stableHits++;
        else
        {
            _stableCandidate = trimmed;
            _stableHits = 1;
        }

        if (_stableHits < RequiredStableHits)
            return false;

        confirmed = trimmed;
        return true;
    }

    /// <summary>
    /// EAN-8 / EAN-13 / UPC-A GS1 kontrol basamağı geçerliyse tek karede kabul (odak titremesinde çift kare şartı yok).
    /// </summary>
    private bool TryAcceptDecodedValue(string raw, out string confirmed)
    {
        if (RetailBarcodeNormalizer.TryGetCanonicalRetailCode(raw, out var retail))
        {
            confirmed = retail;
            return true;
        }

        return TryConfirmStableRead(raw, out confirmed);
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_handled) return;
        var first = e.Results?.FirstOrDefault();
        if (first is null || string.IsNullOrWhiteSpace(first.Value)) return;

        if (!TryAcceptDecodedValue(first.Value, out var code))
            return;

        _handled = true;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (string.Equals(_returnTarget, "sku", StringComparison.OrdinalIgnoreCase))
            {
                await Shell.Current.GoToAsync("..", new Dictionary<string, object>
                {
                    ["scanned"] = code
                });
                return;
            }

            var buffer = App.Services.GetRequiredService<ProductScanReturnBuffer>();
            buffer.SetPendingCode(code);
            await Shell.Current.GoToAsync("..");
        });
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        Camera.IsTorchOn = !Camera.IsTorchOn;
        TorchButton.Text = Camera.IsTorchOn ? "Fener kapat" : "Fener";
    }
}
