using System.Globalization;
using B2B.Mobile.Features.Orders;

namespace B2B.Mobile.UI.Converters;

public sealed class OrderStatusLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value switch
        {
            int i => i,
            long l => (int)l,
            byte b => b,
            _ => (int?)null
        };
        return code is { } s ? OrderStatuses.ToTrLabel(s) : "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
