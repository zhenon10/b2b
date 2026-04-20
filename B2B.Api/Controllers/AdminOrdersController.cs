using B2B.Api.Infrastructure;
using B2B.Contracts;
using B2B.Api.Security;
using B2B.Domain.Enums;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly B2BDbContext _db;

    public AdminOrdersController(B2BDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminOrderListItem>>>> List(
        [FromQuery] PageRequest page,
        [FromQuery] OrderStatus? status,
        CancellationToken ct)
    {
        page = page.Normalize();

        var q =
            from o in _db.Orders.AsNoTracking()
            join buyer in _db.Users.AsNoTracking() on o.BuyerUserId equals buyer.UserId
            join seller in _db.Users.AsNoTracking() on o.SellerUserId equals seller.UserId
            select new { o, buyer, seller };

        if (status.HasValue)
            q = q.Where(x => x.o.Status == status.Value);

        q = q.OrderByDescending(x => x.o.CreatedAtUtc).ThenByDescending(x => x.o.OrderNumber);

        var total = await q.LongCountAsync(ct);
        var rows = await q
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(x => new AdminOrderListItem(
                x.o.OrderId,
                x.o.OrderNumber,
                x.o.BuyerUserId,
                x.buyer.Email,
                x.buyer.DisplayName,
                x.o.SellerUserId,
                x.seller.DisplayName,
                x.o.CurrencyCode,
                x.o.GrandTotal,
                x.o.Status,
                x.o.CreatedAtUtc))
            .ToListAsync(ct);

        var result = new PagedResult<AdminOrderListItem>(
            rows,
            new PageMeta(page.Page, page.PageSize, rows.Count, total));

        return Ok(ApiResponse<PagedResult<AdminOrderListItem>>.Ok(result, HttpContext.TraceId()));
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<ApiResponse<AdminOrderDetail>>> Get(Guid orderId, CancellationToken ct)
    {
        var row = await (
            from o in _db.Orders.AsNoTracking()
            join buyer in _db.Users.AsNoTracking() on o.BuyerUserId equals buyer.UserId
            join seller in _db.Users.AsNoTracking() on o.SellerUserId equals seller.UserId
            where o.OrderId == orderId
            select new { o, buyer, seller }
        ).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return NotFound(ApiResponse<AdminOrderDetail>.Fail(
                new ApiError("not_found", "Sipariş bulunamadı.", null),
                HttpContext.TraceId()));
        }

        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.LineNumber)
            .Select(i => new AdminOrderLine(
                i.LineNumber,
                i.ProductSku,
                i.ProductName,
                i.UnitPrice,
                i.Quantity))
            .ToListAsync(ct);

        var dto = new AdminOrderDetail(
            row.o.OrderId,
            row.o.OrderNumber,
            row.o.BuyerUserId,
            row.buyer.Email,
            row.buyer.DisplayName,
            row.o.SellerUserId,
            row.seller.DisplayName,
            row.o.CurrencyCode,
            row.o.Subtotal,
            row.o.GrandTotal,
            row.o.Status,
            row.o.CreatedAtUtc,
            row.o.UpdatedAtUtc,
            lines);

        return Ok(ApiResponse<AdminOrderDetail>.Ok(dto, HttpContext.TraceId()));
    }
}
