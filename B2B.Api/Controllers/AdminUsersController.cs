using System.IdentityModel.Tokens.Jwt;

using System.Security.Claims;

using B2B.Api.Infrastructure;

using B2B.Contracts;

using B2B.Api.Security;

using B2B.Domain.Entities;

using B2B.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;



namespace B2B.Api.Controllers;



[ApiController]

[Route("api/v1/admin/users")]

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]

public sealed class AdminUsersController : ControllerBase

{

    private readonly B2BDbContext _db;



    public AdminUsersController(B2BDbContext db)

    {

        _db = db;

    }



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

        if (!TryGetUserId(User, out var adminUserId))

        {

            return Unauthorized(ApiResponse<object>.Fail(

                new ApiError("invalid_token", "Oturum geçersiz.", null),

                HttpContext.TraceId()));

        }



        var idemKey = GetIdempotencyKey();

        if (idemKey is not null)

        {

            var existing = await _db.AdminDealerApprovalIdempotencies.AsNoTracking()

                .FirstOrDefaultAsync(

                    x => x.AdminUserId == adminUserId && x.IdempotencyKey == idemKey,

                    ct);

            if (existing is not null)

            {

                if (existing.TargetUserId != userId)

                {

                    return Conflict(ApiResponse<object>.Fail(

                        new ApiError(

                            "idempotency_conflict",

                            "Idempotency-Key farklı bir bayi onayı için zaten kullanıldı.",

                            null),

                        HttpContext.TraceId()));

                }



                return Ok(ApiResponse<object>.Ok(

                    new { userId = existing.TargetUserId, approvedAt = existing.ApprovedAtUtc },

                    HttpContext.TraceId()));

            }

        }



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



        var approvedAt = DateTime.UtcNow;

        user.ApprovedAtUtc = approvedAt;

        user.UpdatedAtUtc = approvedAt;



        if (idemKey is not null)

        {

            _db.AdminDealerApprovalIdempotencies.Add(new AdminDealerApprovalIdempotency

            {

                Id = Guid.NewGuid(),

                AdminUserId = adminUserId,

                IdempotencyKey = idemKey,

                TargetUserId = userId,

                ApprovedAtUtc = approvedAt

            });

        }



        await _db.SaveChangesAsync(ct);



        return Ok(ApiResponse<object>.Ok(new { user.UserId, user.ApprovedAtUtc }, HttpContext.TraceId()));

    }



    private string? GetIdempotencyKey()

    {

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values))

            return null;



        var raw = values.ToString().Trim();

        if (string.IsNullOrWhiteSpace(raw))

            return null;



        return raw.Length > 128 ? raw[..128] : raw;

    }



    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)

    {

        userId = Guid.Empty;

        var raw = user.FindFirstValue(JwtRegisteredClaimNames.Sub)

                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out userId);

    }

}


