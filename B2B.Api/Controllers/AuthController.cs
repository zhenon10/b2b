using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using B2B.Api.Infrastructure;
using B2B.Contracts;
using B2B.Api.Security;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly B2BDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly RefreshTokenService _refreshTokens;
    private readonly AuthOptions _auth;

    public AuthController(
        B2BDbContext db,
        JwtTokenService jwt,
        RefreshTokenService refreshTokens,
        IOptions<AuthOptions> authOptions)
    {
        _db = db;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _auth = authOptions.Value;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register(RegisterRequest request, CancellationToken ct)
    {
        if (!_auth.AllowPublicRegistration)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<RegisterResponse>.Fail(
                new ApiError(
                    "registration_disabled",
                    "Genel kayıt kapalı. Bayi hesabı için yönetici onayı veya davetiye gerekir.",
                    null),
                HttpContext.TraceId()));
        }

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        var exists = await _db.Users.AsNoTracking().AnyAsync(x => x.NormalizedEmail == normalizedEmail, ct);
        if (exists)
        {
            return BadRequest(ApiResponse<RegisterResponse>.Fail(
                new ApiError("email_taken", "Registration failed.", null),
                HttpContext.TraceId()
            ));
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);
        var now = DateTime.UtcNow;
        var approvedAt = _auth.AutoApproveRegisteredDealers ? now : (DateTime?)null;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            ApprovedAtUtc = approvedAt,
            CreatedAtUtc = now
        };

        var dealerRole = await EnsureRoleAsync("Dealer", ct);
        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = dealerRole.RoleId });

        await _db.SaveChangesAsync(ct);

        const string pendingMsg = "Kayıt alındı. Yönetici onayından sonra giriş yapabilirsiniz.";
        if (approvedAt is null)
        {
            return Ok(ApiResponse<RegisterResponse>.Ok(
                new RegisterResponse(null, pendingMsg, null),
                HttpContext.TraceId()));
        }

        var token = _jwt.CreateAccessToken(user, roles: ["Dealer"]);
        var refresh = await _refreshTokens.CreateForUserAsync(user.UserId, ct);
        return Ok(ApiResponse<RegisterResponse>.Ok(
            new RegisterResponse(token, "Kayıt tamamlandı.", refresh),
            HttpContext.TraceId()));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(
                new ApiError("invalid_credentials", "Invalid email or password.", null),
                HttpContext.TraceId()
            ));
        }

        if (user.PasswordSalt is null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(
                new ApiError("invalid_credentials", "Invalid email or password.", null),
                HttpContext.TraceId()
            ));
        }

        if (user.ApprovedAtUtc is null)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(
                new ApiError(
                    "pending_approval",
                    "Hesabınız henüz onaylanmadı. Yönetici onayından sonra tekrar deneyin.",
                    null),
                HttpContext.TraceId()));
        }

        var roles = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == user.UserId)
            .Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.RoleId, (_, r) => r.Name)
            .ToListAsync(ct);

        var token = _jwt.CreateAccessToken(user, roles);
        var refresh = await _refreshTokens.CreateForUserAsync(user.UserId, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(new AuthResponse(token, refresh), HttpContext.TraceId()));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var rotated = await _refreshTokens.RotateAsync(request.RefreshToken, ct);
        if (rotated is null)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(
                new ApiError("invalid_refresh", "Yenileme jetonu geçersiz veya süresi dolmuş.", null),
                HttpContext.TraceId()));
        }

        var (user, roles, newRefresh) = rotated.Value;
        var access = _jwt.CreateAccessToken(user, roles);
        return Ok(ApiResponse<AuthResponse>.Ok(new AuthResponse(access, newRefresh), HttpContext.TraceId()));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> Me(CancellationToken ct)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized(ApiResponse<ProfileResponse>.Fail(
                new ApiError("invalid_token", "Oturum geçersiz.", null),
                HttpContext.TraceId()));
        }

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            return NotFound(ApiResponse<ProfileResponse>.Fail(
                new ApiError("not_found", "Kullanıcı bulunamadı.", null),
                HttpContext.TraceId()));
        }

        var roles = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(_db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.RoleId, (_, r) => r.Name)
            .ToListAsync(ct);

        var dto = new ProfileResponse(
            user.UserId,
            user.Email,
            user.DisplayName,
            roles,
            user.ApprovedAtUtc);

        return Ok(ApiResponse<ProfileResponse>.Ok(dto, HttpContext.TraceId()));
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("invalid_token", "Oturum geçersiz.", null),
                HttpContext.TraceId()));
        }

        var newPwd = request.NewPassword?.Trim() ?? "";
        if (newPwd.Length < 8)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("weak_password", "Yeni şifre en az 8 karakter olmalıdır.", null),
                HttpContext.TraceId()));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Kullanıcı bulunamadı.", null),
                HttpContext.TraceId()));
        }

        if (user.PasswordSalt is null ||
            !PasswordHasher.VerifyPassword(request.CurrentPassword ?? "", user.PasswordHash, user.PasswordSalt))
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("invalid_current_password", "Mevcut şifre hatalı.", null),
                HttpContext.TraceId()));
        }

        var (hash, salt) = PasswordHasher.HashPassword(newPwd);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { ok = true }, HttpContext.TraceId()));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        var raw = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }

    private async Task<Role> EnsureRoleAsync(string roleName, CancellationToken ct)
    {
        var normalized = roleName.ToUpperInvariant();
        var existing = await _db.Roles.FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
        if (existing is not null) return existing;

        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            Name = roleName,
            NormalizedName = normalized,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return role;
    }
}

