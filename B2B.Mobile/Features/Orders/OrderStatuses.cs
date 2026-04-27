namespace B2B.Mobile.Features.Orders;

/// <summary>API <c>OrderStatus</c> (0–4) için Türkçe etiketler.</summary>
public static class OrderStatuses
{
    public static string ToTrLabel(int s) => s switch
    {
        0 => "Sipariş Alındı",
        1 => "Onaylandı",
        2 => "Kargoda",
        3 => "Tamamlandı",
        4 => "İptal",
        _ => s.ToString()
    };
}
