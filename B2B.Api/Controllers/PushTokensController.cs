using System.Security.Claims;
using B2B.Api.Infrastructure;
using B2B.Contracts;
using B2B.Infrastructure.Persistence;
using B2B.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/push-tokens")]
public sealed class PushTokensController : ControllerBase
{
    private readonly B2BDbContext _db;

    public PushTokensController(B2BDbContext db) => _db = db;

    [HttpPost]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<RegisterPushTokenResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RegisterPushTokenResponse>>> Register([FromBody] RegisterPushTokenRequest req, CancellationToken ct)
    {
        var token = (req.Token ?? "").Trim();
        var platform = string.IsNullOrWhiteSpace(req.Platform) ? "Android" : req.Platform.Trim();

        if (token.Length < 20)
        {
            return BadRequest(ApiResponse<RegisterPushTokenResponse>.Fail(
                new ApiError("invalid_token", "Push token geçersiz.", null),
                HttpContext.TraceId()));
        }

        Guid? userId = null;
        if (TryGetUserId(User, out var uid))
            userId = uid;

        var existing = await _db.DevicePushTokens
            .FirstOrDefaultAsync(x => x.Platform == platform && x.Token == token, ct);

        if (existing is null)
        {
            existing = new DevicePushToken
            {
                DevicePushTokenId = Guid.NewGuid(),
                UserId = userId,
                Platform = platform,
                Token = token,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };
            _db.DevicePushTokens.Add(existing);
        }
        else
        {
            existing.LastSeenAtUtc = DateTime.UtcNow;
            existing.IsActive = true;
            if (existing.UserId is null && userId is not null)
                existing.UserId = userId;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<RegisterPushTokenResponse>.Ok(
            new RegisterPushTokenResponse(existing.DevicePushTokenId, existing.IsActive),
            HttpContext.TraceId()));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        var raw =
            user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub) ??
            user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}

