using System.Globalization;

namespace B2B.Mobile.UI.Converters;

/// <summary>Aktif ve stok 1–5 arası (dahil).</summary>
public sealed class ActiveAndLowStockConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        var active = values[0] is bool b && b;
        var stock = values[1] is int s ? s : 0;
        return active && stock is >= 1 and <= 5;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
