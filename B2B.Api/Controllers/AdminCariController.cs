using B2B.Api.Infrastructure;
using B2B.Api.Security;
using B2B.Contracts;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/admin/cari")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminCariController : ControllerBase
{
    private readonly B2BDbContext _db;

    public AdminCariController(B2BDbContext db) => _db = db;

    [HttpGet]
    [EnableRateLimiting("read")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerAccountSummary>>>> List(
        [FromQuery] Guid buyerUserId,
        CancellationToken ct)
    {
        if (buyerUserId == Guid.Empty)
        {
            return BadRequest(ApiResponse<IReadOnlyList<CustomerAccountSummary>>.Fail(
                new ApiError("validation_error", "buyerUserId is required.", null),
                HttpContext.TraceId()));
        }

        var rows = await (
            from a in _db.CustomerAccounts.AsNoTracking()
            join seller in _db.Users.AsNoTracking() on a.SellerUserId equals seller.UserId
            where a.BuyerUserId == buyerUserId
            orderby a.Balance descending, a.SellerUserId
            select new CustomerAccountSummary(
                a.SellerUserId,
                seller.DisplayName,
                a.CurrencyCode,
                a.Balance)
        ).ToListAsync(ct);

        return Ok(ApiResponse<IReadOnlyList<CustomerAccountSummary>>.Ok(rows, HttpContext.TraceId()));
    }

    [HttpGet("entries")]
    [EnableRateLimiting("read")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerAccountEntryDto>>>> Entries(
        [FromQuery] Guid buyerUserId,
        [FromQuery] Guid sellerUserId,
        [FromQuery] string currencyCode,
        [FromQuery] PageRequest page,
        CancellationToken ct)
    {
        if (buyerUserId == Guid.Empty || sellerUserId == Guid.Empty)
        {
            return BadRequest(ApiResponse<PagedResult<CustomerAccountEntryDto>>.Fail(
                new ApiError("validation_error", "buyerUserId and sellerUserId are required.", null),
                HttpContext.TraceId()));
        }

        page = page.Normalize();
        var currency = (currencyCode ?? "").Trim().ToUpperInvariant();

        var accountId = await _db.CustomerAccounts.AsNoTracking()
            .Where(a =>
                a.BuyerUserId == buyerUserId &&
                a.SellerUserId == sellerUserId &&
                a.CurrencyCode == currency)
            .Select(a => a.CustomerAccountId)
            .FirstOrDefaultAsync(ct);

        if (accountId == Guid.Empty)
        {
            var empty = new PagedResult<CustomerAccountEntryDto>(
                Array.Empty<CustomerAccountEntryDto>(),
                new PageMeta(page.Page, page.PageSize, 0, 0));
            return Ok(ApiResponse<PagedResult<CustomerAccountEntryDto>>.Ok(empty, HttpContext.TraceId()));
        }

        var q =
            from e in _db.CustomerAccountEntries.AsNoTracking()
            join o in _db.Orders.AsNoTracking() on e.OrderId equals o.OrderId into orders
            from o in orders.DefaultIfEmpty()
            where e.CustomerAccountId == accountId
            orderby e.CreatedAtUtc descending
            select new CustomerAccountEntryDto(
                e.CreatedAtUtc,
                e.Type,
                e.CurrencyCode,
                e.Amount,
                e.OrderId,
                o != null ? o.OrderNumber : null);

        var total = await q.LongCountAsync(ct);
        var items = await q.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        var result = new PagedResult<CustomerAccountEntryDto>(
            items,
            new PageMeta(page.Page, page.PageSize, items.Count, total));

        return Ok(ApiResponse<PagedResult<CustomerAccountEntryDto>>.Ok(result, HttpContext.TraceId()));
    }
}

