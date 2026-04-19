using System.Globalization;

namespace B2B.Mobile.UI.Converters;

/// <summary>Liste fiyatı bayi fiyatından büyükse çizili etiketi göster.</summary>
public sealed class MsrpVisibleWhenDiscountConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        if (values[0] is not decimal msrp || values[1] is not decimal dealer)
            return false;
        return msrp > 0 && msrp > dealer;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
