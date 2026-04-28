using System.Globalization;

namespace B2B.Mobile.UI.Converters;

public sealed class InverseBoolToOpacityConverter : IValueConverter
{
    public double TrueOpacity { get; set; } = 0.2;  // when value == true
    public double FalseOpacity { get; set; } = 1.0; // when value == false

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool bb && bb;
        return b ? TrueOpacity : FalseOpacity;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

