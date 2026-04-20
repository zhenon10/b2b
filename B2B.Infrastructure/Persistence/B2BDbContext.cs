using B2B.Domain.Entities;
using B2B.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace B2B.Infrastructure.Persistence;

public sealed class B2BDbContext : DbContext
{
    public B2BDbContext(DbContextOptions<B2BDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSpec> ProductSpecs => Set<ProductSpec>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderSubmission> OrderSubmissions => Set<OrderSubmission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AdminDealerApprovalIdempotency> AdminDealerApprovalIdempotencies => Set<AdminDealerApprovalIdempotency>();
    public DbSet<UploadAudit> UploadAudits => Set<UploadAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("Roles");
            b.HasKey(x => x.RoleId);

            b.Property(x => x.Name).HasMaxLength(50).IsRequired();
            b.Property(x => x.NormalizedName).HasMaxLength(50).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");

            b.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.UserId);

            b.Property(x => x.Email).HasMaxLength(320).IsRequired();
            b.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200);

            b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            b.Property(x => x.PasswordSalt).HasMaxLength(128);

            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.ApprovedAtUtc);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");
            b.Property(x => x.RowVer).IsRowVersion().IsConcurrencyToken();

            b.HasIndex(x => x.NormalizedEmail).IsUnique()
                .IncludeProperties(x => new { x.Email, x.DisplayName, x.IsActive });
        });

        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("UserRoles");
            b.HasKey(x => new { x.UserId, x.RoleId });

            b.HasOne(x => x.User)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Role)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(x => new { x.RoleId, x.UserId });
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("Categories");
            b.HasKey(x => x.CategoryId);

            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.SortOrder).IsRequired();
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");

            b.HasIndex(x => new { x.IsActive, x.SortOrder });
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasKey(x => x.ProductId);

            b.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description);

            b.Property(x => x.CurrencyCode).HasColumnType("char(3)").IsRequired();
            b.Property(x => x.DealerPrice).HasColumnType("decimal(19,4)").IsRequired();
            b.Property(x => x.MsrpPrice).HasColumnType("decimal(19,4)").IsRequired();
            b.Property(x => x.StockQuantity).IsRequired();

            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");
            b.Property(x => x.RowVer).IsRowVersion().IsConcurrencyToken();

            b.HasOne(x => x.SellerUser)
                .WithMany(x => x.ProductsAsSeller)
                .HasForeignKey(x => x.SellerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(x => x.CategoryId);
            b.HasIndex(x => new { x.SellerUserId, x.Sku }).IsUnique();
            b.HasIndex(x => new { x.SellerUserId, x.IsActive })
                .IncludeProperties(x => new { x.Name, x.DealerPrice, x.MsrpPrice, x.CurrencyCode, x.StockQuantity });
            b.HasIndex(x => x.Name)
                .HasDatabaseName("IX_Products_Name_Search")
                .IncludeProperties(x => new { x.SellerUserId, x.DealerPrice, x.MsrpPrice, x.CurrencyCode, x.IsActive });

            b.ToTable(t => t.HasCheckConstraint("CK_Products_DealerPrice_NonNegative", "DealerPrice >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_Products_MsrpPrice_NonNegative", "MsrpPrice >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_Products_Stock_NonNegative", "StockQuantity >= 0"));
        });

        modelBuilder.Entity<ProductImage>(b =>
        {
            b.ToTable("ProductImages");
            b.HasKey(x => x.ProductImageId);

            b.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            b.Property(x => x.SortOrder).IsRequired();
            b.Property(x => x.IsPrimary).HasDefaultValue(false);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");

            b.HasOne(x => x.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(x => new { x.ProductId, x.SortOrder });
            b.HasIndex(x => new { x.ProductId, x.IsPrimary });
        });

        modelBuilder.Entity<ProductSpec>(b =>
        {
            b.ToTable("ProductSpecs");
            b.HasKey(x => x.ProductSpecId);

            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.Value).HasMaxLength(500).IsRequired();
            b.Property(x => x.SortOrder).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");

            b.HasOne(x => x.Product)
                .WithMany(p => p.Specs)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(x => new { x.ProductId, x.SortOrder });
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(x => x.OrderId);

            b.Property(x => x.OrderNumber)
                .UseIdentityColumn(1, 1)
                .ValueGeneratedOnAdd();
            b.HasAlternateKey(x => x.OrderNumber);

            b.Property(x => x.Status)
                .HasConversion<byte>()
                .IsRequired();

            b.Property(x => x.CurrencyCode).HasColumnType("char(3)").IsRequired();
            b.Property(x => x.Subtotal).HasColumnType("decimal(19,4)").IsRequired();
            b.Property(x => x.TaxTotal).HasColumnType("decimal(19,4)").IsRequired();
            b.Property(x => x.ShippingTotal).HasColumnType("decimal(19,4)").IsRequired();
            b.Property(x => x.GrandTotal).HasColumnType("decimal(19,4)").IsRequired();

            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");
            b.Property(x => x.RowVer).IsRowVersion().IsConcurrencyToken();

            b.HasOne(x => x.BuyerUser)
                .WithMany(x => x.OrdersAsBuyer)
                .HasForeignKey(x => x.BuyerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.SellerUser)
                .WithMany(x => x.OrdersAsSeller)
                .HasForeignKey(x => x.SellerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(x => new { x.BuyerUserId, x.CreatedAtUtc })
                .HasDatabaseName("IX_Orders_Buyer_CreatedAt")
                .IsDescending(false, true)
                .IncludeProperties(x => new { x.Status, x.OrderNumber, x.GrandTotal, x.CurrencyCode });

            b.HasIndex(x => new { x.SellerUserId, x.Status, x.CreatedAtUtc })
                .HasDatabaseName("IX_Orders_Seller_Status_CreatedAt")
                .IsDescending(false, false, true)
                .IncludeProperties(x => new { x.OrderNumber, x.BuyerUserId, x.GrandTotal, x.CurrencyCode });

            b.ToTable(t => t.HasCheckConstraint(
                "CK_Orders_Totals_NonNegative",
                "Subtotal >= 0 AND TaxTotal >= 0 AND ShippingTotal >= 0 AND GrandTotal >= 0"
            ));
        });

        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("OrderItems");
            b.HasKey(x => x.OrderItemId);

            b.Property(x => x.LineNumber).IsRequired();
            b.Property(x => x.ProductSku).HasMaxLength(64).IsRequired();
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.Property(x => x.UnitPrice).HasColumnType("decimal(19,4)").IsRequired();
            b.Property(x => x.Quantity).IsRequired();
            b.Property(x => x.RowVer).IsRowVersion().IsConcurrencyToken();

            b.HasOne(x => x.Order)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Product)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(x => new { x.OrderId, x.LineNumber }).IsUnique();
            b.HasIndex(x => x.OrderId)
                .IncludeProperties(x => new
                {
                    x.ProductId,
                    x.ProductSku,
                    x.ProductName,
                    x.UnitPrice,
                    x.Quantity
                });
            b.HasIndex(x => new { x.ProductId, x.OrderId });

            b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Qty_Positive", "Quantity > 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_UnitPrice_NonNegative", "UnitPrice >= 0"));
        });

        modelBuilder.Entity<OrderSubmission>(b =>
        {
            b.ToTable("OrderSubmissions");
            b.HasKey(x => x.OrderSubmissionId);

            b.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");

            b.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(x => new { x.BuyerUserId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("UX_OrderSubmissions_Buyer_Key");

            b.HasIndex(x => x.OrderId)
                .HasDatabaseName("IX_OrderSubmissions_OrderId");
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(x => x.RefreshTokenId);

            b.Property(x => x.TokenHash).HasMaxLength(32).IsFixedLength().IsRequired();
            b.Property(x => x.ExpiresAtUtc).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");

            b.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UX_RefreshTokens_TokenHash");
            b.HasIndex(x => new { x.UserId, x.RevokedAtUtc, x.ExpiresAtUtc })
                .HasDatabaseName("IX_RefreshTokens_User_Active");
        });

        modelBuilder.Entity<AdminDealerApprovalIdempotency>(b =>
        {
            b.ToTable("AdminDealerApprovalIdempotencies");
            b.HasKey(x => x.Id);

            b.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            b.Property(x => x.ApprovedAtUtc).IsRequired();

            b.HasIndex(x => new { x.AdminUserId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("UX_AdminDealerApproval_Admin_Key");
        });

        modelBuilder.Entity<UploadAudit>(b =>
        {
            b.ToTable("UploadAudits");
            b.HasKey(x => x.UploadAuditId);

            b.Property(x => x.Kind).HasMaxLength(60).IsRequired();
            b.Property(x => x.FileExt).HasMaxLength(10).IsRequired();
            b.Property(x => x.StoredPath).HasMaxLength(500).IsRequired();
            b.Property(x => x.PublicUrl).HasMaxLength(1000).IsRequired();
            b.Property(x => x.FileSizeBytes).IsRequired();
            b.Property(x => x.Width).IsRequired();
            b.Property(x => x.Height).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("sysutcdatetime()");

            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.Kind, x.CreatedAtUtc });
        });

        base.OnModelCreating(modelBuilder);
    }
}

