namespace B2B.Mobile.Features.Orders;

/// <summary>API <c>OrderStatus</c> (0–4) için Türkçe etiketler.</summary>
public static class OrderStatuses
{
    public static string ToTrLabel(int s) => s switch
    {
        0 => "Taslak",
        1 => "Verildi",
        2 => "Ödendi",
        3 => "Kargoda",
        4 => "İptal",
        _ => s.ToString()
    };
}
