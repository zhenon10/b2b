using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using B2B.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace B2B.Api.Security;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly JwtKeyMaterial _keyMaterial;

    public JwtTokenService(IOptions<JwtOptions> options, JwtKeyMaterial keyMaterial)
    {
        _options = options.Value;
        _keyMaterial = keyMaterial;
    }

    public string CreateAccessToken(User user, IReadOnlyList<string> roles)
    {
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = _keyMaterial.GetSigningKey();
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

