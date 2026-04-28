using System.Security.Claims;
using B2B.Api.Infrastructure;
using B2B.Contracts;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly B2BDbContext _db;

    public NotificationsController(B2BDbContext db) => _db = db;

    [HttpGet]
    [EnableRateLimiting("read")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationListItem>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationListItem>>>> List([FromQuery] PageRequest page, CancellationToken ct)
    {
        page = page.Normalize();
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized(ApiResponse<PagedResult<NotificationListItem>>.Fail(
                new ApiError("invalid_token", "Oturum geçersiz.", null),
                HttpContext.TraceId()));
        }

        var q = _db.Notifications.AsNoTracking();
        var total = await q.LongCountAsync(ct);

        var pageQuery = q
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.NotificationId)
            .Skip(page.Skip)
            .Take(page.PageSize);

        var notifications = await pageQuery.ToListAsync(ct);
        var ids = notifications.Select(x => x.NotificationId).ToList();
        var readSet = await _db.NotificationReads.AsNoTracking()
            .Where(r => r.UserId == userId && ids.Contains(r.NotificationId))
            .Select(r => r.NotificationId)
            .ToListAsync(ct);

        var readHash = readSet.ToHashSet();
        var items = notifications.Select(n => new NotificationListItem(
            n.NotificationId,
            n.Title,
            n.Body,
            n.DataJson,
            n.CreatedAtUtc,
            readHash.Contains(n.NotificationId)
        )).ToList();

        var result = new PagedResult<NotificationListItem>(
            items,
            new PageMeta(page.Page, page.PageSize, items.Count, total)
        );

        return Ok(ApiResponse<PagedResult<NotificationListItem>>.Ok(result, HttpContext.TraceId()));
    }

    [HttpPost("{notificationId:guid}/read")]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(Guid notificationId, CancellationToken ct)
    {
        if (!TryGetUserId(User, out var userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("invalid_token", "Oturum geçersiz.", null),
                HttpContext.TraceId()));
        }

        var exists = await _db.Notifications.AsNoTracking().AnyAsync(x => x.NotificationId == notificationId, ct);
        if (!exists)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Bildirim bulunamadı.", null),
                HttpContext.TraceId()));
        }

        var already = await _db.NotificationReads.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.NotificationId == notificationId, ct);
        if (already)
            return Ok(ApiResponse<object>.Ok(new { ok = true }, HttpContext.TraceId()));

        _db.NotificationReads.Add(new NotificationRead
        {
            NotificationReadId = Guid.NewGuid(),
            NotificationId = notificationId,
            UserId = userId,
            ReadAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { ok = true }, HttpContext.TraceId()));
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

