namespace B2B.Mobile.Features.Products.Services;

/// <summary>
/// Ürünler sekmesine barkod sonucunu URI sorgu parametresi olmadan iletir.
/// Shell'de <c>scanned</c> sorgusunun rotada kalıp geri dönüşte tekrar uygulanmasını önler.
/// </summary>
public sealed class ProductScanReturnBuffer
{
    private readonly object _lock = new();
    private string? _pendingCode;

    public void SetPendingCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        lock (_lock)
            _pendingCode = code.Trim();
    }

    public string? GetAndClearPendingCode()
    {
        lock (_lock)
        {
            var c = _pendingCode;
            _pendingCode = null;
            return c;
        }
    }
}
