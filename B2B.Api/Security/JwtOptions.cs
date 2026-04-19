namespace B2B.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    /// <summary>Alias for Jwt:SigningKey (e.g. tests or hosting conventions).</summary>
    public string? Key { get; set; }
    /// <summary>HMAC signing secret; never commit real values—use env / Key Vault / User Secrets.</summary>
    public string? SigningKey { get; set; }
    public int AccessTokenMinutes { get; set; } = 60;
}

