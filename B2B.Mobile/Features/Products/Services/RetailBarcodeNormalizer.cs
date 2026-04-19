namespace B2B.Mobile.Features.Products.Services;

/// <summary>
/// EAN/UPC gibi perakende kodları normalize eder ve GS1 kontrol basamağı ile doğrular.
/// Geçerli EAN/UPC tek karede kabul edilebilir (odak titremesinde çift-kare şartını kaldırır).
/// </summary>
public static class RetailBarcodeNormalizer
{
    public static string DigitsOnly(string s) =>
        string.IsNullOrEmpty(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

    /// <summary>Geçerliyse yalnızca rakamlardan oluşan canonical değeri döner.</summary>
    public static bool TryGetCanonicalRetailCode(string raw, out string canonical)
    {
        canonical = "";
        var digits = DigitsOnly(raw);
        if (digits.Length < 8)
            return false;

        if (digits.Length == 13 && IsValidEan13(digits))
        {
            canonical = digits;
            return true;
        }

        if (digits.Length == 8 && IsValidEan8(digits))
        {
            canonical = digits;
            return true;
        }

        if (digits.Length == 12 && IsValidUpcA(digits))
        {
            canonical = digits;
            return true;
        }

        return false;
    }

    public static bool IsValidEan13(string digits)
    {
        if (digits.Length != 13 || !digits.All(char.IsDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var n = digits[i] - '0';
            sum += (i % 2 == 0) ? n : n * 3;
        }

        var check = (10 - (sum % 10)) % 10;
        return check == (digits[12] - '0');
    }

    public static bool IsValidEan8(string digits)
    {
        if (digits.Length != 8 || !digits.All(char.IsDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 7; i++)
        {
            var n = digits[i] - '0';
            // Soldan indeks 0,1,2… için sağdan konuma göre ağırlık 3,1,3,1… (EAN-13 ile aynı mantık,7 hane).
            sum += (i % 2 == 0) ? n * 3 : n;
        }

        var check = (10 - (sum % 10)) % 10;
        return check == (digits[7] - '0');
    }

    public static bool IsValidUpcA(string digits)
    {
        if (digits.Length != 12 || !digits.All(char.IsDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 11; i++)
        {
            var n = digits[i] - '0';
            sum += (i % 2 == 0) ? n * 3 : n;
        }

        var check = (10 - (sum % 10)) % 10;
        return check == (digits[11] - '0');
    }
}
