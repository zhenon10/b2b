namespace B2B.Mobile.Core.Auth;

/// <summary>
/// Shell URI parçalarına göre yalnızca yöneticiye açık sayfaların tespiti (client-side yedek koruma).
/// </summary>
public static class AdminRouteGuard
{
    private static readonly string[] LocationMarkers =
    [
        "adminHub",
        "categoryAdmin",
        "categoryEdit",
        "adminOrders",
        "pendingDealers",
        "productEdit",
        "adminBroadcast",
    ];

    public static bool UriLooksLikeAdminOnly(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return false;

        foreach (var m in LocationMarkers)
        {
            if (location.Contains(m, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
