using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace B2B.Api.Security;

/// <summary>
/// Centralizes JWT signing key creation to ensure token generation and validation
/// always use the same key material (UTF8 bytes from JwtOptions.SigningKey).
/// </summary>
public sealed class JwtKeyMaterial
{
    private readonly IOptionsMonitor<JwtOptions> _options;

    public JwtKeyMaterial(IOptionsMonitor<JwtOptions> options)
    {
        _options = options;
    }

    public SymmetricSecurityKey GetSigningKey()
    {
        var o = _options.CurrentValue;
        var key = !string.IsNullOrWhiteSpace(o.SigningKey) ? o.SigningKey : o.Key;
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be configured and at least 32 characters long.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}

