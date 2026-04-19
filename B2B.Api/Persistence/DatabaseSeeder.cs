using B2B.Api.Security;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace B2B.Api.Persistence;

public static class DatabaseSeeder
{
    public enum SeedMode
    {
        Off = 0,
        RolesOnly = 1,
        RolesAndAdmin = 2,
        Demo = 3
    }

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
        var db = services.GetRequiredService<B2BDbContext>();
        var config = services.GetRequiredService<IConfiguration>();

        var modeRaw = (config["Seed:Mode"] ?? "Off").Trim();
        if (!Enum.TryParse<SeedMode>(modeRaw, ignoreCase: true, out var mode))
        {
            mode = SeedMode.Off;
        }

        if (mode == SeedMode.Off)
        {
            logger.LogInformation("Database seed skipped (Seed:Mode=Off).");
            return;
        }

        // Roles: Admin (yönetici) + Dealer (bayi)
        var rolesToEnsure = new[]
        {
            ("Admin", "ADMIN"),
            ("Dealer", "DEALER")
        };

        foreach (var (name, normalized) in rolesToEnsure)
        {
            var exists = await db.Roles.AnyAsync(r => r.NormalizedName == normalized, ct);
            if (!exists)
            {
                db.Roles.Add(new Role
                {
                    RoleId = Guid.NewGuid(),
                    Name = name,
                    NormalizedName = normalized,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(ct);

        await MigrateLegacySellerBuyerRolesAsync(db, logger, ct);

        if (mode == SeedMode.RolesOnly)
        {
            logger.LogInformation("Database seed completed (roles only).");
            return;
        }

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@b2b.local";
        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Seed:AdminPassword is required when Seed:Mode is RolesAndAdmin or Demo. " +
                "Set it via User Secrets (Development), environment variable Seed__AdminPassword, or Key Vault.");
        }

        var admin = await EnsureUserAsync(db, adminEmail, "Admin", adminPassword, ct);

        if (mode == SeedMode.RolesAndAdmin)
        {
            logger.LogInformation("Database seed completed (roles + admin). Admin={AdminEmail}", admin.Email);
            return;
        }

        // Demo mode: admin owns catalog + demo bayi hesabı
        var dealerEmail = config["Seed:DealerEmail"] ?? config["Seed:BuyerEmail"] ?? "buyer@b2b.local";
        var dealerPassword = config["Seed:DealerPassword"] ?? config["Seed:BuyerPassword"];
        if (string.IsNullOrWhiteSpace(dealerPassword))
        {
            throw new InvalidOperationException(
                "Seed:DealerPassword or Seed:BuyerPassword is required when Seed:Mode is Demo. " +
                "Set via User Secrets, environment variables, or Key Vault.");
        }

        _ = await EnsureUserAsync(db, dealerEmail, "Dealer", dealerPassword, ct);

        // Örnek kategoriler + ürünler yönetici (toptancı) hesabına bağlı
        var anyProducts = await db.Products.AnyAsync(p => p.SellerUserId == admin.UserId, ct);
        if (!anyProducts)
        {
            var catA = new Category
            {
                CategoryId = Guid.NewGuid(),
                Name = "Elektronik",
                SortOrder = 1,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            var catB = new Category
            {
                CategoryId = Guid.NewGuid(),
                Name = "Ofis",
                SortOrder = 2,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Categories.AddRange(catA, catB);

            db.Products.AddRange(
                new Product
                {
                    ProductId = Guid.NewGuid(),
                    SellerUserId = admin.UserId,
                    CategoryId = catA.CategoryId,
                    Sku = "SKU-0001",
                    Name = "Sample Product 1",
                    Description = "Seeded sample product",
                    CurrencyCode = "USD",
                    DealerPrice = 7.99m,
                    MsrpPrice = 9.99m,
                    StockQuantity = 250,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    RowVer = Array.Empty<byte>()
                },
                new Product
                {
                    ProductId = Guid.NewGuid(),
                    SellerUserId = admin.UserId,
                    CategoryId = catB.CategoryId,
                    Sku = "SKU-0002",
                    Name = "Sample Product 2",
                    Description = "Seeded sample product",
                    CurrencyCode = "USD",
                    DealerPrice = 14.99m,
                    MsrpPrice = 19.99m,
                    StockQuantity = 100,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    RowVer = Array.Empty<byte>()
                }
            );

            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Database seed completed (demo). Admin={AdminEmail}, Dealer={DealerEmail}",
            admin.Email,
            dealerEmail);
    }

    /// <summary>
    /// Eski Seller/Buyer rol atamalarını Dealer'a taşır (JWT ve yetkilendirme sadeleştirmesi).
    /// </summary>
    private static async Task MigrateLegacySellerBuyerRolesAsync(B2BDbContext db, ILogger logger, CancellationToken ct)
    {
        var dealer = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.NormalizedName == "DEALER", ct);
        if (dealer is null) return;

        var legacyRoleIds = await db.Roles.AsNoTracking()
            .Where(r => r.NormalizedName == "SELLER" || r.NormalizedName == "BUYER")
            .Select(r => r.RoleId)
            .ToListAsync(ct);

        if (legacyRoleIds.Count == 0) return;

        var affectedUserIds = await db.UserRoles.AsNoTracking()
            .Where(ur => legacyRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in affectedUserIds)
        {
            var hasDealer = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == dealer.RoleId, ct);
            if (!hasDealer)
            {
                db.UserRoles.Add(new UserRole { UserId = userId, RoleId = dealer.RoleId });
            }
        }

        await db.UserRoles.Where(ur => legacyRoleIds.Contains(ur.RoleId)).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Legacy Seller/Buyer user roles migrated to Dealer for {Count} users.", affectedUserIds.Count);
    }

    private static async Task<User> EnsureUserAsync(B2BDbContext db, string email, string roleName, string password, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();

        var user = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user is null)
        {
            var (hash, salt) = PasswordHasher.HashPassword(password);
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                DisplayName = roleName,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                ApprovedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                RowVer = Array.Empty<byte>()
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        var normalizedRole = roleName.Trim().ToUpperInvariant();
        var role = await db.Roles.FirstAsync(r => r.NormalizedName == normalizedRole, ct);

        var hasRole = await db.UserRoles.AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == role.RoleId, ct);
        if (!hasRole)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId
            });
            await db.SaveChangesAsync(ct);
        }

        return user;
    }
}

