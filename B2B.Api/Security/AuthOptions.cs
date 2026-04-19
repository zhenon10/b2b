namespace B2B.Api.Security;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When false, POST /api/v1/auth/register returns 403. Prefer false in production;
    /// use invitations or admin-created accounts instead.
    /// </summary>
    public bool AllowPublicRegistration { get; set; }

    /// <summary>
    /// When true, newly registered Dealers get ApprovedAtUtc set immediately (integration tests / optional dev only).
    /// </summary>
    public bool AutoApproveRegisteredDealers { get; set; }
}
