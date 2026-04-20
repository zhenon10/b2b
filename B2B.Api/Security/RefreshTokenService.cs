using System.Security.Cryptography;
using System.Text;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace B2B.Api.Security;

public sealed class RefreshTokenService
{
    private readonly B2BDbContext _db;
    private readonly JwtOptions _jwt;

    public RefreshTokenService(B2BDbContext db, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    public static byte[] HashPlaintext(string plain) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(plain));

    public async Task<string> CreateForUserAsync(Guid userId, CancellationToken ct)
    {
        var plain = NewPlainToken();
        var entity = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashPlaintext(plain),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);
        return plain;
    }

    /// <summary>Eski token iptal + yeni opaque token üretir.</summary>
    public async Task<(User User, List<string> Roles, string PlainRefresh)?> RotateAsync(
        string? plaintext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return null;

        var hash = HashPlaintext(plaintext.Trim());
        var existing = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (existing is null
            || existing.RevokedAtUtc is not null
            || existing.ExpiresAtUtc < DateTime.UtcNow
            || !existing.User.IsActive
            || existing.User.ApprovedAtUtc is null)
        {
            return null;
        }

        var roles = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == existing.UserId)
            .Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.RoleId, (_, r) => r.Name)
            .ToListAsync(ct);

        var newPlain = NewPlainToken();
        var newEntity = new RefreshToken
        {
            RefreshTokenId = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = HashPlaintext(newPlain),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedAtUtc = DateTime.UtcNow
        };

        existing.RevokedAtUtc = DateTime.UtcNow;
        _db.RefreshTokens.Add(newEntity);
        await _db.SaveChangesAsync(ct);

        return (existing.User, roles, newPlain);
    }

    private static string NewPlainToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }
}
