using B2B.Api.Security;
using B2B.Contracts;
using B2B.Api.Infrastructure;
using B2B.Domain.Enums;
using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace B2B.Api.Controllers;

[ApiController]
[Route("api/v1/maintenance")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class MaintenanceController : ControllerBase
{
    private readonly B2BDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<ObjectStorageOptions> _storage;

    public MaintenanceController(B2BDbContext db, IWebHostEnvironment env, IOptions<ObjectStorageOptions> storage)
    {
        _db = db;
        _env = env;
        _storage = storage;
    }

    [HttpPost("cari/backfill")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<object>>> BackfillCustomerAccounts(
        [FromQuery] bool dryRun = true,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int batchSize = 500,
        CancellationToken ct = default)
    {
        // Safety rails
        if (batchSize is < 50 or > 5000)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("validation_error", "batchSize must be between 50 and 5000.", null),
                HttpContext.TraceIdentifier));
        }

        var from = fromUtc;
        var to = toUtc;
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError("validation_error", "fromUtc cannot be later than toUtc.", null),
                HttpContext.TraceIdentifier));
        }

        var scanned = 0L;
        var accountsCreated = 0L;
        var entriesCreated = 0L;
        var debitsCreated = 0L;
        var creditsCreated = 0L;

        IQueryable<Domain.Entities.Order> q = _db.Orders.AsNoTracking();
        if (from.HasValue) q = q.Where(o => o.CreatedAtUtc >= from.Value);
        if (to.HasValue) q = q.Where(o => o.CreatedAtUtc <= to.Value);

        // We only need statuses that may affect receivable.
        q = q.Where(o => o.Status != OrderStatus.Draft);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var skip = 0;
            while (true)
            {
                var batch = await q
                    .OrderBy(o => o.CreatedAtUtc).ThenBy(o => o.OrderNumber)
                    .Skip(skip)
                    .Take(batchSize)
                    .Select(o => new
                    {
                        o.OrderId,
                        o.OrderNumber,
                        o.BuyerUserId,
                        o.SellerUserId,
                        o.CurrencyCode,
                        o.GrandTotal,
                        o.Status,
                        o.CreatedAtUtc
                    })
                    .ToListAsync(ct);

                if (batch.Count == 0)
                    break;

                scanned += batch.Count;

                var orderIds = batch.Select(x => x.OrderId).ToList();
                var existingEntryPairs = await _db.CustomerAccountEntries.AsNoTracking()
                    .Where(e => e.OrderId != null && orderIds.Contains(e.OrderId.Value))
                    .Select(e => new { e.OrderId, e.Type })
                    .ToListAsync(ct);

                var existingByOrder = existingEntryPairs
                    .GroupBy(x => x.OrderId!.Value)
                    .ToDictionary(g => g.Key, g => g.Select(x => (CustomerAccountEntryType)x.Type).ToHashSet());

                // Cache accounts by composite key to reduce DB roundtrips inside the batch.
                var accountCache = new Dictionary<(Guid Buyer, Guid Seller, string Currency), Guid>();

                // For dry-run we don't need transactions or tracked entities.
                await using var tx = dryRun ? null : await _db.Database.BeginTransactionAsync(ct);

                foreach (var o in batch)
                {
                    var amount = o.GrandTotal;
                    if (amount <= 0) continue;

                    var types = existingByOrder.TryGetValue(o.OrderId, out var set) ? set : null;

                    bool hasDebit = types?.Contains(CustomerAccountEntryType.DebitOrderPlaced) == true;
                    bool hasPaidCredit = types?.Contains(CustomerAccountEntryType.CreditOrderPaid) == true;
                    bool hasCancelCredit = types?.Contains(CustomerAccountEntryType.CreditOrderCancelled) == true;

                    // Desired movements by status:
                    // - Placed: ensure Debit
                    // - Paid/Shipped: ensure Debit + PaidCredit (net 0)
                    // - Cancelled: do not reflect; if there is a Debit without any Credit, ensure CancelCredit (net 0)
                    var needDebit = o.Status == OrderStatus.Placed || o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped;
                    var needPaidCredit = o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped;
                    var needCancelCredit = o.Status == OrderStatus.Cancelled && hasDebit && !hasPaidCredit && !hasCancelCredit;

                    if (needDebit && hasDebit) needDebit = false;
                    if (needPaidCredit && hasPaidCredit) needPaidCredit = false;
                    if (!needCancelCredit) { /* already computed */ }

                    if (!needDebit && !needPaidCredit && !needCancelCredit)
                        continue;

                    if (dryRun)
                    {
                        if (needDebit) { entriesCreated++; debitsCreated++; }
                        if (needPaidCredit || needCancelCredit) { entriesCreated++; creditsCreated++; }
                        continue;
                    }

                    var currency = (o.CurrencyCode ?? "").Trim().ToUpperInvariant();
                    var accKey = (o.BuyerUserId, o.SellerUserId, currency);
                    if (!accountCache.TryGetValue(accKey, out var accountId))
                    {
                        accountId = await _db.CustomerAccounts
                            .Where(a => a.BuyerUserId == o.BuyerUserId && a.SellerUserId == o.SellerUserId && a.CurrencyCode == currency)
                            .Select(a => a.CustomerAccountId)
                            .FirstOrDefaultAsync(ct);

                        if (accountId == Guid.Empty)
                        {
                            var newAcc = new Domain.Entities.CustomerAccount
                            {
                                CustomerAccountId = Guid.NewGuid(),
                                BuyerUserId = o.BuyerUserId,
                                SellerUserId = o.SellerUserId,
                                CurrencyCode = currency,
                                Balance = 0,
                                CreatedAtUtc = DateTime.UtcNow
                            };
                            _db.CustomerAccounts.Add(newAcc);
                            accountId = newAcc.CustomerAccountId;
                            accountsCreated++;
                        }

                        accountCache[accKey] = accountId;
                    }

                    // Load tracked account row once per key to adjust Balance safely.
                    var account = await _db.CustomerAccounts.FirstAsync(a => a.CustomerAccountId == accountId, ct);

                    if (needDebit)
                    {
                        _db.CustomerAccountEntries.Add(new Domain.Entities.CustomerAccountEntry
                        {
                            CustomerAccountEntryId = Guid.NewGuid(),
                            CustomerAccountId = account.CustomerAccountId,
                            Type = CustomerAccountEntryType.DebitOrderPlaced,
                            CurrencyCode = currency,
                            Amount = amount,
                            OrderId = o.OrderId,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        account.Balance += amount;
                        account.UpdatedAtUtc = DateTime.UtcNow;
                        entriesCreated++;
                        debitsCreated++;
                    }

                    if (needPaidCredit)
                    {
                        if (account.Balance < amount) account.Balance = amount; // best-effort heal before subtract
                        _db.CustomerAccountEntries.Add(new Domain.Entities.CustomerAccountEntry
                        {
                            CustomerAccountEntryId = Guid.NewGuid(),
                            CustomerAccountId = account.CustomerAccountId,
                            Type = CustomerAccountEntryType.CreditOrderPaid,
                            CurrencyCode = currency,
                            Amount = amount,
                            OrderId = o.OrderId,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        account.Balance -= amount;
                        account.UpdatedAtUtc = DateTime.UtcNow;
                        entriesCreated++;
                        creditsCreated++;
                    }

                    if (needCancelCredit)
                    {
                        if (account.Balance < amount) account.Balance = amount; // best-effort heal before subtract
                        _db.CustomerAccountEntries.Add(new Domain.Entities.CustomerAccountEntry
                        {
                            CustomerAccountEntryId = Guid.NewGuid(),
                            CustomerAccountId = account.CustomerAccountId,
                            Type = CustomerAccountEntryType.CreditOrderCancelled,
                            CurrencyCode = currency,
                            Amount = amount,
                            OrderId = o.OrderId,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        account.Balance -= amount;
                        account.UpdatedAtUtc = DateTime.UtcNow;
                        entriesCreated++;
                        creditsCreated++;
                    }
                }

                if (!dryRun)
                {
                    await _db.SaveChangesAsync(ct);
                    await tx!.CommitAsync(ct);
                }

                skip += batch.Count;
            }
        });

        var body = new
        {
            dryRun,
            fromUtc = from,
            toUtc = to,
            batchSize,
            scannedOrders = scanned,
            accountsCreated,
            entriesCreated,
            debitsCreated,
            creditsCreated
        };

        return Ok(ApiResponse<object>.Ok(body, HttpContext.TraceIdentifier));
    }

    [HttpPost("product-images/reconcile")]
    [EnableRateLimiting("write")]
    [ProducesResponseType(typeof(ApiResponse<ReconcileProductImagesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReconcileProductImagesResponse>>> ReconcileProductImages(
        [FromQuery] bool dryRun = true,
        CancellationToken ct = default)
    {
        var provider = (_storage.Value.Provider ?? "Local").Trim();
        if (!provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<ReconcileProductImagesResponse>.Fail(
                new ApiError("not_supported", $"Reconcile is only supported for Local storage provider. Current: {provider}.", null),
                HttpContext.TraceIdentifier));
        }

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var uploadsRoot = Path.Combine(webRoot, "uploads", "products");

        // Only reconcile images that look like our own uploads path.
        // If Url is external (cdn, etc.), we won't touch it.
        var images = await _db.ProductImages
            .AsNoTracking()
            .Select(x => new { x.ProductImageId, x.ProductId, x.Url })
            .ToListAsync(ct);

        var broken = new List<BrokenProductImage>();
        foreach (var img in images)
        {
            if (!TryGetUploadsFileName(img.Url, out var fileName))
                continue;

            var absPath = Path.Combine(uploadsRoot, fileName);
            if (!System.IO.File.Exists(absPath))
            {
                broken.Add(new BrokenProductImage(img.ProductImageId, img.ProductId, img.Url));
            }
        }

        if (!dryRun && broken.Count > 0)
        {
            var ids = broken.Select(x => x.ProductImageId).ToList();
            // Use ExecuteDeleteAsync for efficiency; fall back to tracked delete if unavailable.
            await _db.ProductImages
                .Where(x => ids.Contains(x.ProductImageId))
                .ExecuteDeleteAsync(ct);
        }

        var resp = new ReconcileProductImagesResponse(
            DryRun: dryRun,
            WebRoot: webRoot,
            UploadsRoot: uploadsRoot,
            TotalImages: images.Count,
            BrokenCount: broken.Count,
            DeletedCount: dryRun ? 0 : broken.Count,
            Broken: broken
        );

        return Ok(ApiResponse<ReconcileProductImagesResponse>.Ok(resp, HttpContext.TraceIdentifier));
    }

    private static bool TryGetUploadsFileName(string? url, out string fileName)
    {
        fileName = "";
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Accept: http(s)://host/uploads/products/<file>
        // Accept: /uploads/products/<file>
        // Reject: anything else (external urls, different paths)
        var marker = "/uploads/products/";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        var tail = url[(idx + marker.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(tail)) return false;

        // strip querystring if present
        var q = tail.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0) tail = tail[..q];

        // stop at next slash (shouldn't happen, but be safe)
        var slash = tail.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0) tail = tail[..slash];

        // basic filename safety: no path traversal
        tail = tail.Replace("\\", "").Replace("/", "");
        if (tail.Contains("..", StringComparison.Ordinal)) return false;

        fileName = tail;
        return true;
    }
}

