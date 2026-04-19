using B2B.Api.Contracts;
using B2B.Api.Infrastructure;
using B2B.Domain.Enums;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Roles = "Admin")]
public sealed class AdminOrdersController : ControllerBase
{
    private readonly B2BDbContext _db;

    public AdminOrdersController(B2BDbContext db) => _db = db;

    public sealed record AdminOrderListItemDto(
        Guid OrderId,
        long OrderNumber,
        Guid BuyerUserId,
        string BuyerEmail,
        string? BuyerDisplayName,
        Guid SellerUserId,
        string? SellerDisplayName,
        string CurrencyCode,
        decimal GrandTotal,
        OrderStatus Status,
        DateTime CreatedAtUtc);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminOrderListItemDto>>>> List(
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
            .Select(x => new AdminOrderListItemDto(
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

        var result = new PagedResult<AdminOrderListItemDto>(
            rows,
            new PageMeta(page.Page, page.PageSize, rows.Count, total));

        return Ok(ApiResponse<PagedResult<AdminOrderListItemDto>>.Ok(result, HttpContext.TraceId()));
    }

    public sealed record AdminOrderLineDto(
        int LineNumber,
        string ProductSku,
        string ProductName,
        decimal UnitPrice,
        int Quantity);

    public sealed record AdminOrderDetailDto(
        Guid OrderId,
        long OrderNumber,
        Guid BuyerUserId,
        string BuyerEmail,
        string? BuyerDisplayName,
        Guid SellerUserId,
        string? SellerDisplayName,
        string CurrencyCode,
        decimal Subtotal,
        decimal GrandTotal,
        OrderStatus Status,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc,
        IReadOnlyList<AdminOrderLineDto> Items);

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<ApiResponse<AdminOrderDetailDto>>> Get(Guid orderId, CancellationToken ct)
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
            return NotFound(ApiResponse<AdminOrderDetailDto>.Fail(
                new ApiError("not_found", "Sipariş bulunamadı.", null),
                HttpContext.TraceId()));
        }

        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .OrderBy(i => i.LineNumber)
            .Select(i => new AdminOrderLineDto(
                i.LineNumber,
                i.ProductSku,
                i.ProductName,
                i.UnitPrice,
                i.Quantity))
            .ToListAsync(ct);

        var dto = new AdminOrderDetailDto(
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

        return Ok(ApiResponse<AdminOrderDetailDto>.Ok(dto, HttpContext.TraceId()));
    }
}
