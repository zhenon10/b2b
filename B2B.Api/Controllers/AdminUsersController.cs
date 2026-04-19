using B2B.Api.Contracts;
using B2B.Api.Infrastructure;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly B2BDbContext _db;

    public AdminUsersController(B2BDbContext db)
    {
        _db = db;
    }

    public sealed record PendingDealerDto(Guid UserId, string Email, string? DisplayName, DateTime CreatedAtUtc);

    [HttpGet("pending-dealers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PendingDealerDto>>>> GetPendingDealers(CancellationToken ct)
    {
        var dealerRoleId = await _db.Roles.AsNoTracking()
            .Where(r => r.NormalizedName == "DEALER")
            .Select(r => r.RoleId)
            .FirstOrDefaultAsync(ct);

        if (dealerRoleId == Guid.Empty)
        {
            return Ok(ApiResponse<IReadOnlyList<PendingDealerDto>>.Ok(
                Array.Empty<PendingDealerDto>(),
                HttpContext.TraceId()));
        }

        var pending = await _db.Users.AsNoTracking()
            .Where(u => u.ApprovedAtUtc == null && u.IsActive)
            .Where(u => _db.UserRoles.Any(ur => ur.UserId == u.UserId && ur.RoleId == dealerRoleId))
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => new PendingDealerDto(u.UserId, u.Email, u.DisplayName, u.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<PendingDealerDto>>.Ok(pending, HttpContext.TraceId()));
    }

    [HttpPost("{userId:guid}/approve")]
    public async Task<ActionResult<ApiResponse<object>>> ApproveDealer(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Kullanıcı bulunamadı.", null),
                HttpContext.TraceId()));
        }

        if (user.ApprovedAtUtc is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("already_approved", "Bu kullanıcı zaten onaylı.", null),
                HttpContext.TraceId()));
        }

        var dealerRoleId = await _db.Roles.AsNoTracking()
            .Where(r => r.NormalizedName == "DEALER")
            .Select(r => r.RoleId)
            .FirstOrDefaultAsync(ct);
        if (dealerRoleId == Guid.Empty)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("dealer_role_missing", "Bayi rolü bulunamadı.", null),
                HttpContext.TraceId()));
        }

        var isDealer = await _db.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == dealerRoleId, ct);
        if (!isDealer)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("not_dealer", "Yalnızca bayi hesapları bu yolla onaylanır.", null),
                HttpContext.TraceId()));
        }

        user.ApprovedAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { user.UserId, user.ApprovedAtUtc }, HttpContext.TraceId()));
    }
}
