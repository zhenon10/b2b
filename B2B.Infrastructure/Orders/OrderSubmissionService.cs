using System.Security.Cryptography;
using System.Text;
using B2B.Application.Orders;
using B2B.Contracts;
using B2B.Domain.Entities;
using B2B.Domain.Enums;
using B2B.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2B.Infrastructure.Orders;

public sealed class OrderSubmissionService : IOrderSubmissionService
{
    private readonly B2BDbContext _db;

    public OrderSubmissionService(B2BDbContext db) => _db = db;

    public async Task<SubmitOrderResult> SubmitAsync(SubmitOrderCommand cmd, CancellationToken ct)
    {
        var request = cmd.Request;

        if (request.Items.Count == 0)
        {
            var body = ApiResponse<SubmitOrderResponse>.Fail(
                new ApiError("empty_order", "Order must contain at least one item.", null),
                cmd.TraceId);
            return new SubmitOrderResult(400, body);
        }

        var requestHash = ComputeRequestHash(cmd.BuyerUserId, request);

        if (!string.IsNullOrWhiteSpace(cmd.IdempotencyKey))
        {
            var existing = await _db.OrderSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.BuyerUserId == cmd.BuyerUserId && x.IdempotencyKey == cmd.IdempotencyKey,
                    ct);

            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    var body = ApiResponse<SubmitOrderResponse>.Fail(
                        new ApiError("idempotency_conflict", "Idempotency-Key was already used with a different request payload.", null),
                        cmd.TraceId);
                    return new SubmitOrderResult(409, body);
                }

                var existingOrderInfo = await _db.Orders.AsNoTracking()
                    .Where(o => o.OrderId == existing.OrderId)
                    .Select(o => new { o.OrderId, o.OrderNumber, o.GrandTotal })
                    .FirstOrDefaultAsync(ct);

                if (existingOrderInfo is not null)
                {
                    var body = ApiResponse<SubmitOrderResponse>.Ok(
                        new SubmitOrderResponse(existingOrderInfo.OrderId, existingOrderInfo.OrderNumber, existingOrderInfo.GrandTotal),
                        cmd.TraceId);
                    return new SubmitOrderResult(200, body);
                }
            }
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var strategy = _db.Database.CreateExecutionStrategy();
        SubmitOrderResult? result = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var products = await _db.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync(ct);

            if (products.Count != productIds.Count)
            {
                result = Fail(400, "invalid_products", "One or more products are invalid.", null, cmd.TraceId);
                return;
            }

            if (products.Any(p => !p.IsActive))
            {
                result = Fail(409, "inactive_product", "One or more products are inactive.", null, cmd.TraceId);
                return;
            }

            if (products.Any(p => p.SellerUserId != request.SellerUserId))
            {
                result = Fail(400, "invalid_seller", "All items must belong to the selected seller.", null, cmd.TraceId);
                return;
            }

            var currency = request.CurrencyCode.Trim().ToUpperInvariant();
            if (products.Any(p => p.CurrencyCode != currency))
            {
                result = Fail(400, "currency_mismatch", "All items must have the same currency.", null, cmd.TraceId);
                return;
            }

            foreach (var line in request.Items)
            {
                var p = products.Single(x => x.ProductId == line.ProductId);
                if (line.Quantity <= 0)
                {
                    result = Fail(400, "invalid_quantity", "Quantity must be positive.", null, cmd.TraceId);
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
                    result = Fail(409, "insufficient_stock", $"Insufficient stock for product '{p.Sku}'.", details, cmd.TraceId);
                    return;
                }

                p.StockQuantity -= line.Quantity;
                p.UpdatedAtUtc = DateTime.UtcNow;
            }

            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                BuyerUserId = cmd.BuyerUserId,
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

            if (!string.IsNullOrWhiteSpace(cmd.IdempotencyKey))
            {
                _db.OrderSubmissions.Add(new OrderSubmission
                {
                    OrderSubmissionId = Guid.NewGuid(),
                    BuyerUserId = cmd.BuyerUserId,
                    SellerUserId = request.SellerUserId,
                    IdempotencyKey = cmd.IdempotencyKey!,
                    RequestHash = requestHash,
                    OrderId = order.OrderId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var body = ApiResponse<SubmitOrderResponse>.Ok(
                new SubmitOrderResponse(order.OrderId, order.OrderNumber, order.GrandTotal),
                cmd.TraceId);
            result = new SubmitOrderResult(200, body);
        });

        if (result is null)
        {
            var body = ApiResponse<SubmitOrderResponse>.Fail(
                new ApiError("server_error", "An unexpected error occurred.", null),
                cmd.TraceId);
            return new SubmitOrderResult(500, body);
        }

        return result;
    }

    public async Task<UpdateOrderStatusResult> UpdateStatusAsync(UpdateOrderStatusCommand cmd, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == cmd.OrderId, ct);
        if (order is null)
        {
            return new UpdateOrderStatusResult(
                404,
                ApiResponse<object>.Fail(
                    new ApiError("not_found", "Order not found.", null),
                    cmd.TraceId));
        }

        if (!IsValidTransition(order.Status, cmd.Status))
        {
            var details = new Dictionary<string, string[]>
            {
                ["from"] = [order.Status.ToString()],
                ["to"] = [cmd.Status.ToString()]
            };
            return new UpdateOrderStatusResult(
                400,
                ApiResponse<object>.Fail(
                    new ApiError("invalid_status_transition", "Invalid order status transition.", details),
                    cmd.TraceId));
        }

        order.Status = cmd.Status;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new UpdateOrderStatusResult(
            200,
            ApiResponse<object>.Ok(new { orderId = order.OrderId, status = order.Status }, cmd.TraceId));
    }

    public async Task<CancelOrderResult> CancelAsync(CancelOrderCommand cmd, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        CancelOrderResult? result = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == cmd.OrderId, ct);
            if (order is null)
            {
                result = new CancelOrderResult(
                    404,
                    ApiResponse<object>.Fail(new ApiError("not_found", "Order not found.", null), cmd.TraceId));
                return;
            }

            if (order.BuyerUserId != cmd.BuyerUserId)
            {
                result = new CancelOrderResult(
                    403,
                    ApiResponse<object>.Fail(new ApiError("forbidden", "Not allowed.", null), cmd.TraceId));
                return;
            }

            if (order.Status is OrderStatus.Shipped or OrderStatus.Cancelled)
            {
                var details = new Dictionary<string, string[]>
                {
                    ["status"] = [order.Status.ToString()]
                };
                result = new CancelOrderResult(
                    409,
                    ApiResponse<object>.Fail(new ApiError("cannot_cancel", "Order cannot be cancelled at this stage.", details), cmd.TraceId));
                return;
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAtUtc = DateTime.UtcNow;

            var items = await _db.OrderItems.Where(i => i.OrderId == cmd.OrderId).ToListAsync(ct);
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

            result = new CancelOrderResult(
                200,
                ApiResponse<object>.Ok(new { orderId = order.OrderId, status = order.Status }, cmd.TraceId));
        });

        if (result is null)
        {
            return new CancelOrderResult(
                500,
                ApiResponse<object>.Fail(new ApiError("server_error", "An unexpected error occurred.", null), cmd.TraceId));
        }

        return result;
    }

    private static SubmitOrderResult Fail(int statusCode, string code, string message, IReadOnlyDictionary<string, string[]>? details, string traceId)
    {
        var body = ApiResponse<SubmitOrderResponse>.Fail(new ApiError(code, message, details), traceId);
        return new SubmitOrderResult(statusCode, body);
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

    private static string ComputeRequestHash(Guid buyerUserId, SubmitOrderRequest req)
    {
        var sb = new StringBuilder();
        sb.Append(buyerUserId.ToString("N")).Append('|');
        sb.Append(req.SellerUserId.ToString("N")).Append('|');
        sb.Append(req.CurrencyCode?.Trim().ToUpperInvariant() ?? "").Append('|');

        foreach (var item in req.Items.OrderBy(i => i.ProductId))
        {
            sb.Append(item.ProductId.ToString("N")).Append(':').Append(item.Quantity).Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

