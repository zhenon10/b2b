using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using B2B.Application.Orders;
using B2B.Api.Infrastructure;
using B2B.Contracts;
using B2B.Api.Security;
using B2B.Domain.Entities;
using B2B.Domain.Enums;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly B2BDbContext _db;
    private readonly IOrderSubmissionService _submitOrders;

    public OrdersController(B2BDbContext db, IOrderSubmissionService submitOrders)
    {
        _db = db;
        _submitOrders = submitOrders;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.DealerOnly)]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<SubmitOrderResponse>>> Submit(SubmitOrderRequest request, CancellationToken ct)
    {
        var buyerUserIdStr =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(buyerUserIdStr, out var buyerUserId))
        {
            return Unauthorized(ApiResponse<SubmitOrderResponse>.Fail(
                new ApiError("unauthorized", "Missing user identity.", null),
                HttpContext.TraceId()
            ));
        }

        var idempotencyKey = GetIdempotencyKey();

        var result = await _submitOrders.SubmitAsync(
            new SubmitOrderCommand(buyerUserId, request, idempotencyKey, HttpContext.TraceId()),
            ct);

        return StatusCode(result.HttpStatusCode, result.Response);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DealerOnly)]
    public async Task<ActionResult<ApiResponse<PagedResult<DealerOrderListItem>>>> List([FromQuery] PageRequest page, CancellationToken ct)
    {
        page = page.Normalize();

        var buyerUserIdStr =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(buyerUserIdStr, out var buyerUserId))
        {
            return Unauthorized(ApiResponse<PagedResult<DealerOrderListItem>>.Fail(
                new ApiError("unauthorized", "Missing user identity.", null),
                HttpContext.TraceId()
            ));
        }

        var query =
            from o in _db.Orders.AsNoTracking()
            join seller in _db.Users.AsNoTracking() on o.SellerUserId equals seller.UserId
            where o.BuyerUserId == buyerUserId
            orderby o.CreatedAtUtc descending, o.OrderNumber descending
            select new { o, seller };

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(x => new DealerOrderListItem(
                x.o.OrderId,
                x.o.OrderNumber,
                x.o.SellerUserId,
                x.seller.DisplayName,
                x.o.CurrencyCode,
                x.o.GrandTotal,
                x.o.Status,
                x.o.CreatedAtUtc))
            .ToListAsync(ct);

        var result = new PagedResult<DealerOrderListItem>(items, new PageMeta(page.Page, page.PageSize, items.Count, total));

        return Ok(ApiResponse<PagedResult<DealerOrderListItem>>.Ok(result, HttpContext.TraceId()));
    }

    /// <summary>Bayi: yalnızca kendi siparişinin kalemlerini ve durumunu döner.</summary>
    [HttpGet("{orderId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.DealerOnly)]
    public async Task<ActionResult<ApiResponse<DealerOrderDetail>>> GetMyOrder(Guid orderId, CancellationToken ct)
    {
        var buyerUserIdStr =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(buyerUserIdStr, out var buyerUserId))
        {
            return Unauthorized(ApiResponse<DealerOrderDetail>.Fail(
                new ApiError("unauthorized", "Missing user identity.", null),
                HttpContext.TraceId()));
        }

        var row = await (
            from o in _db.Orders.AsNoTracking()
            join seller in _db.Users.AsNoTracking() on o.SellerUserId equals seller.UserId
            where o.OrderId == orderId && o.BuyerUserId == buyerUserId
            select new { o, seller }
        ).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return NotFound(ApiResponse<DealerOrderDetail>.Fail(
                new ApiError("not_found", "Sipariş bulunamadı.", null),
                HttpContext.TraceId()));
        }

        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.LineNumber)
            .Select(i => new DealerOrderLine(
                i.LineNumber,
                i.ProductSku,
                i.ProductName,
                i.UnitPrice,
                i.Quantity))
            .ToListAsync(ct);

        var dto = new DealerOrderDetail(
            row.o.OrderId,
            row.o.OrderNumber,
            row.o.SellerUserId,
            row.seller.DisplayName,
            row.o.CurrencyCode,
            row.o.Subtotal,
            row.o.GrandTotal,
            row.o.Status,
            row.o.CreatedAtUtc,
            row.o.UpdatedAtUtc,
            lines);

        return Ok(ApiResponse<DealerOrderDetail>.Ok(dto, HttpContext.TraceId()));
    }

    [HttpPatch("{orderId:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest req, CancellationToken ct)
    {
        var result = await _submitOrders.UpdateStatusAsync(
            new UpdateOrderStatusCommand(orderId, req.Status, HttpContext.TraceId()),
            ct);

        return StatusCode(result.HttpStatusCode, result.Response);
    }

    [HttpPost("{orderId:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.DealerOnly)]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(Guid orderId, CancellationToken ct)
    {
        var buyerUserIdStr =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(buyerUserIdStr, out var buyerUserId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                new ApiError("unauthorized", "Missing user identity.", null),
                HttpContext.TraceId()
            ));
        }

        var result = await _submitOrders.CancelAsync(
            new CancelOrderCommand(buyerUserId, orderId, HttpContext.TraceId()),
            ct);

        return StatusCode(result.HttpStatusCode, result.Response);
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

}

