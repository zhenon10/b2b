using System.Globalization;

namespace B2B.Mobile.UI.Converters;

/// <summary>Ürün görsel URL'si; boş veya geçersizse Material ikon yer tutucu. UriImageSource önbelleği açık.</summary>
public sealed class UrlToImageSourceConverter : IValueConverter
{
    /// <summary>HTTP önbelleği için geçerlilik; süre dolunca yeniden indirilir.</summary>
    public static TimeSpan DefaultCacheValidity { get; set; } = TimeSpan.FromDays(7);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return Placeholder();

        var trimmed = s.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return Placeholder();

        return new UriImageSource
        {
            Uri = uri,
            CachingEnabled = true,
            CacheValidity = DefaultCacheValidity
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static FontImageSource Placeholder() =>
        new()
        {
            FontFamily = "MaterialIcons",
            Glyph = "\uE54E",
            Size = 40,
            Color = global::Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#6C757D")
                : Color.FromArgb("#ADB5BD")
        };
}
