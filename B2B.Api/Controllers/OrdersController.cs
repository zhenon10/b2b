using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using B2B.Api.Infrastructure;
using B2B.Contracts;
using B2B.Api.Security;
using B2B.Domain.Entities;
using B2B.Domain.Enums;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly B2BDbContext _db;

    public OrdersController(B2BDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.DealerOnly)]
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

        if (request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<SubmitOrderResponse>.Fail(
                new ApiError("empty_order", "Order must contain at least one item.", null),
                HttpContext.TraceId()
            ));
        }

        var idempotencyKey = GetIdempotencyKey();
        var requestHash = ComputeRequestHash(buyerUserId, request);

        if (idempotencyKey is not null)
        {
            var existing = await _db.OrderSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BuyerUserId == buyerUserId && x.IdempotencyKey == idempotencyKey, ct);

            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    return Conflict(ApiResponse<SubmitOrderResponse>.Fail(
                        new ApiError("idempotency_conflict", "Idempotency-Key was already used with a different request payload.", null),
                        HttpContext.TraceId()
                    ));
                }

                var existingOrderInfo = await _db.Orders.AsNoTracking()
                    .Where(o => o.OrderId == existing.OrderId)
                    .Select(o => new { o.OrderId, o.OrderNumber, o.GrandTotal })
                    .FirstOrDefaultAsync(ct);

                if (existingOrderInfo is not null)
                {
                    return Ok(ApiResponse<SubmitOrderResponse>.Ok(
                        new SubmitOrderResponse(existingOrderInfo.OrderId, existingOrderInfo.OrderNumber, existingOrderInfo.GrandTotal),
                        HttpContext.TraceId()
                    ));
                }
            }
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var strategy = _db.Database.CreateExecutionStrategy();
        ApiResponse<SubmitOrderResponse>? response = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var products = await _db.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync(ct);

            if (products.Count != productIds.Count)
            {
                response = ApiResponse<SubmitOrderResponse>.Fail(
                    new ApiError("invalid_products", "One or more products are invalid.", null),
                    HttpContext.TraceId());
                return;
            }

            if (products.Any(p => !p.IsActive))
            {
                response = ApiResponse<SubmitOrderResponse>.Fail(
                    new ApiError("inactive_product", "One or more products are inactive.", null),
                    HttpContext.TraceId());
                return;
            }

            // enforce single seller / requested seller
            if (products.Any(p => p.SellerUserId != request.SellerUserId))
            {
                response = ApiResponse<SubmitOrderResponse>.Fail(
                    new ApiError("invalid_seller", "All items must belong to the selected seller.", null),
                    HttpContext.TraceId());
                return;
            }

            var currency = request.CurrencyCode.Trim().ToUpperInvariant();
            if (products.Any(p => p.CurrencyCode != currency))
            {
                response = ApiResponse<SubmitOrderResponse>.Fail(
                    new ApiError("currency_mismatch", "All items must have the same currency.", null),
                    HttpContext.TraceId());
                return;
            }

            // Stock check + decrement (retry-safe when idempotencyKey used)
            foreach (var line in request.Items)
            {
                var p = products.Single(x => x.ProductId == line.ProductId);
                if (line.Quantity <= 0)
                {
                    response = ApiResponse<SubmitOrderResponse>.Fail(
                        new ApiError("invalid_quantity", "Quantity must be positive.", null),
                        HttpContext.TraceId());
                    return;
                }

                if (p.StockQuantity < line.Quantity)
                {
                    var details = new Dictionary<string, string[]>
                    {
                        ["productId"] = [p.ProductId.ToString()],
                        ["sku"] = [p.Sku],
                        ["available"] = [p.StockQuantity.ToString()],
                        ["requested"] = [line.Quantity.ToString()]
                    };
                    response = ApiResponse<SubmitOrderResponse>.Fail(
                        new ApiError("insufficient_stock", $"Insufficient stock for product '{p.Sku}'.", details),
                        HttpContext.TraceId());
                    return;
                }

                p.StockQuantity -= line.Quantity;
                p.UpdatedAtUtc = DateTime.UtcNow;
            }

            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                BuyerUserId = buyerUserId,
                SellerUserId = request.SellerUserId,
                Status = OrderStatus.Placed,
                CurrencyCode = currency,
                CreatedAtUtc = DateTime.UtcNow
            };

            var items = request.Items
                .Select((i, idx) =>
                {
                    var p = products.Single(x => x.ProductId == i.ProductId);
                    return new OrderItem
                    {
                        OrderItemId = Guid.NewGuid(),
                        OrderId = order.OrderId,
                        ProductId = p.ProductId,
                        LineNumber = idx + 1,
                        ProductSku = p.Sku,
                        ProductName = p.Name,
                        UnitPrice = p.DealerPrice,
                        Quantity = i.Quantity
                    };
                })
                .ToList();

            var subtotal = items.Sum(x => x.UnitPrice * x.Quantity);
            order.Subtotal = subtotal;
            order.TaxTotal = 0;
            order.ShippingTotal = 0;
            order.GrandTotal = subtotal;

            _db.Orders.Add(order);
            _db.OrderItems.AddRange(items);

            if (idempotencyKey is not null)
            {
                _db.OrderSubmissions.Add(new OrderSubmission
                {
                    OrderSubmissionId = Guid.NewGuid(),
                    BuyerUserId = buyerUserId,
                    SellerUserId = request.SellerUserId,
                    IdempotencyKey = idempotencyKey,
                    RequestHash = requestHash,
                    OrderId = order.OrderId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            response = ApiResponse<SubmitOrderResponse>.Ok(
                new SubmitOrderResponse(order.OrderId, order.OrderNumber, order.GrandTotal),
                HttpContext.TraceId());
        });

        if (response is null)
        {
            // Should never happen, but keep API contract predictable.
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<SubmitOrderResponse>.Fail(
                new ApiError("server_error", "An unexpected error occurred.", null),
                HttpContext.TraceId()
            ));
        }

        // Map to the right status codes based on error codes we used above.
        return response.Success
            ? Ok(response)
            : response.Error?.Code switch
            {
                "invalid_products" or "invalid_seller" or "currency_mismatch" or "invalid_quantity" => BadRequest(response),
                "inactive_product" or "insufficient_stock" => Conflict(response),
                _ => BadRequest(response)
            };
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
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest req, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("not_found", "Order not found.", null),
                HttpContext.TraceId()
            ));
        }

        if (!IsValidTransition(order.Status, req.Status))
        {
            var details = new Dictionary<string, string[]>
            {
                ["from"] = [order.Status.ToString()],
                ["to"] = [req.Status.ToString()]
            };
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("invalid_status_transition", "Invalid order status transition.", details),
                HttpContext.TraceId()
            ));
        }

        order.Status = req.Status;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { orderId = order.OrderId, status = order.Status }, HttpContext.TraceId()));
    }

    [HttpPost("{orderId:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.DealerOnly)]
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

        var strategy = _db.Database.CreateExecutionStrategy();
        ApiResponse<object>? response = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
            if (order is null)
            {
                response = ApiResponse<object>.Fail(
                    new ApiError("not_found", "Order not found.", null),
                    HttpContext.TraceId());
                return;
            }

            if (order.BuyerUserId != buyerUserId)
            {
                response = ApiResponse<object>.Fail(
                    new ApiError("forbidden", "Not allowed.", null),
                    HttpContext.TraceId());
                return;
            }

            if (order.Status is OrderStatus.Shipped or OrderStatus.Cancelled)
            {
                var details = new Dictionary<string, string[]>
                {
                    ["status"] = [order.Status.ToString()]
                };
                response = ApiResponse<object>.Fail(
                    new ApiError("cannot_cancel", "Order cannot be cancelled at this stage.", details),
                    HttpContext.TraceId());
                return;
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAtUtc = DateTime.UtcNow;

            // Restock items
            var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _db.Products.Where(p => productIds.Contains(p.ProductId)).ToListAsync(ct);
            foreach (var item in items)
            {
                var p = products.Single(x => x.ProductId == item.ProductId);
                p.StockQuantity += item.Quantity;
                p.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            response = ApiResponse<object>.Ok(new { orderId = order.OrderId, status = order.Status }, HttpContext.TraceId());
        });

        if (response is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(
                new ApiError("server_error", "An unexpected error occurred.", null),
                HttpContext.TraceId()
            ));
        }

        return response.Success
            ? Ok(response)
            : response.Error?.Code switch
            {
                "not_found" => NotFound(response),
                "cannot_cancel" => Conflict(response),
                "forbidden" => Forbid(),
                _ => BadRequest(response)
            };
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

    private static string ComputeRequestHash(Guid buyerUserId, SubmitOrderRequest req)
    {
        var currency = req.CurrencyCode.Trim().ToUpperInvariant();
        var items = req.Items
            .OrderBy(i => i.ProductId)
            .Select(i => $"{i.ProductId:N}:{i.Quantity}")
            .ToArray();

        var material = $"{buyerUserId:N}|{req.SellerUserId:N}|{currency}|{string.Join(",", items)}";
        var bytes = Encoding.UTF8.GetBytes(material);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // 64 chars
    }

    private static bool IsValidTransition(OrderStatus from, OrderStatus to)
    {
        if (from == to) return true;

        return from switch
        {
            OrderStatus.Placed => to is OrderStatus.Paid or OrderStatus.Cancelled,
            OrderStatus.Paid => to is OrderStatus.Shipped or OrderStatus.Cancelled,
            OrderStatus.Shipped => false,
            OrderStatus.Cancelled => false,
            OrderStatus.Draft => to is OrderStatus.Placed or OrderStatus.Cancelled,
            _ => false
        };
    }
}

