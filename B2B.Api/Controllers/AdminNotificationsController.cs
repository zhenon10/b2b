using System.Security.Claims;
using B2B.Api.Infrastructure;
using B2B.Api.Push;
using B2B.Api.Security;
using B2B.Contracts;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/admin/notifications")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminNotificationsController : ControllerBase
{
    private readonly B2BDbContext _db;
    private readonly IPushSender _push;

    public AdminNotificationsController(B2BDbContext db, IPushSender push)
    {
        _db = db;
        _push = push;
    }

    [HttpPost]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateAdminNotificationRequest req, CancellationToken ct)
    {
        if (!TryGetUserId(User, out var adminUserId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("invalid_token", "Oturum geçersiz.", null),
                HttpContext.TraceId()));
        }

        var title = (req.Title ?? "").Trim();
        var body = (req.Body ?? "").Trim();
        if (title.Length == 0 || body.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("invalid_request", "Başlık ve mesaj zorunlu.", null),
                HttpContext.TraceId()));
        }

        var n = new Notification
        {
            NotificationId = Guid.NewGuid(),
            CreatedByUserId = adminUserId,
            Target = "All",
            Title = title,
            Body = body,
            DataJson = string.IsNullOrWhiteSpace(req.DataJson) ? null : req.DataJson.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);

        var tokens = await _db.DevicePushTokens.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(ct);

        if (tokens.Count > 0)
        {
            var pushResult = await _push.SendBroadcastAsync(n, tokens, ct);

            foreach (var r in pushResult.Results)
            {
                _db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    NotificationDeliveryId = Guid.NewGuid(),
                    NotificationId = n.NotificationId,
                    UserId = r.UserId,
                    DevicePushTokenId = r.DevicePushTokenId,
                    Status = r.Success ? "Sent" : "Failed",
                    Error = r.Error,
                    SentAtUtc = DateTime.UtcNow
                });
            }

            if (pushResult.Results.Any(x => x.DeactivateToken))
            {
                var deactivateIds = pushResult.Results.Where(x => x.DeactivateToken).Select(x => x.DevicePushTokenId).ToList();
                var dbTokens = await _db.DevicePushTokens.Where(x => deactivateIds.Contains(x.DevicePushTokenId)).ToListAsync(ct);
                foreach (var t in dbTokens)
                    t.IsActive = false;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Ok(ApiResponse<object>.Ok(new { notificationId = n.NotificationId }, HttpContext.TraceId()));
    }

    [HttpDelete]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ClearAll(CancellationToken ct)
    {
        // Notifications -> cascades to NotificationDeliveries + NotificationReads (FK cascade)
        var total = await _db.Notifications.CountAsync(ct);
        if (total > 0)
        {
            await _db.Notifications.ExecuteDeleteAsync(ct);
        }

        return Ok(ApiResponse<object>.Ok(new { deletedNotifications = total }, HttpContext.TraceId()));
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

