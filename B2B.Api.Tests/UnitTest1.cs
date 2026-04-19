using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using B2B.Api.Contracts;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace B2B.Api.Tests;

public sealed class ApiScenariosTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiScenariosTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Returns_Ok_Envelope()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_Login_And_Call_Me()
    {
        var client = _factory.CreateClient();

        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = "buyer@example.com",
            Password = "P@ssw0rd!",
            DisplayName = "Buyer One"
        });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);

        var regPayload = await reg.Content.ReadFromJsonAsync<ApiResponse<RegisterResult>>();
        regPayload!.Success.Should().BeTrue();
        regPayload.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "buyer@example.com",
            Password = "P@ssw0rd!"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginPayload = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        loginPayload!.Success.Should().BeTrue();

        // sanity check: token should validate with the same test key
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.ValidateToken(
            loginPayload.Data!.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "B2B.Api.Tests",
                ValidateAudience = true,
                ValidAudience = "B2B.Api.Tests.Client",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestKey)),
                ValidateLifetime = false
            },
            out _);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload.Data!.AccessToken);
        var me = await client.GetAsync("/api/v1/auth/me");
        if (me.StatusCode != HttpStatusCode.OK)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(loginPayload.Data!.AccessToken);
            var kid = jwt.Header.TryGetValue("kid", out var kidVal) ? kidVal?.ToString() : "";
            var alg = jwt.Header.Alg;
            var body = await me.Content.ReadAsStringAsync();
            var authHeader = me.Headers.WwwAuthenticate.ToString();
            throw new Exception($"Expected 200 but got {(int)me.StatusCode}. WWW-Authenticate='{authHeader}'. alg='{alg}' kid='{kid}'. TokenIssuer='{jwt.Issuer}'. TokenAud='{string.Join(",", jwt.Audiences)}'. Body='{body}'");
        }
    }

    [Fact]
    public async Task Register_Returns_Forbidden_When_Public_Registration_Disabled()
    {
        await using var restricted = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:AllowPublicRegistration"] = "false"
                });
            });
        });

        var client = restricted.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = "closed@example.com",
            Password = "P@ssw0rd!"
        });

        reg.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await reg.Content.ReadFromJsonAsync<ApiResponse<RegisterResult>>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeFalse();
        payload.Error!.Code.Should().Be("registration_disabled");
    }

    [Fact]
    public async Task Login_Returns_Pending_Until_Admin_Approves()
    {
        await using var strict = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:AutoApproveRegisteredDealers"] = "false"
                });
            });
        });

        var client = strict.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = "pending@example.com",
            Password = "P@ssw0rd!"
        });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);
        var regPayload = await reg.Content.ReadFromJsonAsync<ApiResponse<RegisterResult>>();
        regPayload!.Success.Should().BeTrue();
        regPayload.Data!.AccessToken.Should().BeNullOrWhiteSpace();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "pending@example.com",
            Password = "P@ssw0rd!"
        });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var loginPayload = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        loginPayload!.Error!.Code.Should().Be("pending_approval");

        using (var scope = strict.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();
            var u = await db.Users.FirstAsync(x => x.NormalizedEmail == "PENDING@EXAMPLE.COM");
            u.ApprovedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var login2 = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "pending@example.com",
            Password = "P@ssw0rd!"
        });
        login2.StatusCode.Should().Be(HttpStatusCode.OK);
        var okPayload = await login2.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        okPayload!.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Products_List_And_GetDetail()
    {
        // Seed a seller + products directly via DbContext
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();
            if (!await db.Products.AnyAsync())
            {
                var seller = await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == "SELLER@EXAMPLE.COM");
                if (seller is null)
                {
                    seller = new User
                    {
                        UserId = Guid.NewGuid(),
                        Email = "seller@example.com",
                        NormalizedEmail = "SELLER@EXAMPLE.COM",
                        PasswordHash = new byte[] { 1 },
                        PasswordSalt = new byte[] { 2 },
                        IsActive = true,
                        ApprovedAtUtc = DateTime.UtcNow,
                        CreatedAtUtc = DateTime.UtcNow,
                        RowVer = Array.Empty<byte>()
                    };
                    db.Users.Add(seller);
                    await db.SaveChangesAsync();
                }

                db.Products.Add(new Product
                {
                    ProductId = Guid.NewGuid(),
                    SellerUserId = seller.UserId,
                    Sku = "SKU-1",
                    Name = "Bolt",
                    CurrencyCode = "USD",
                    DealerPrice = 1.00m,
                    MsrpPrice = 1.25m,
                    StockQuantity = 100,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    RowVer = Array.Empty<byte>()
                });
                await db.SaveChangesAsync();
            }
        }

        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>("/api/v1/products?page=1&pageSize=10");
        list!.Success.Should().BeTrue();
        list.Data!.Items.Should().NotBeEmpty();

        var first = list.Data.Items[0];
        var detail = await client.GetFromJsonAsync<ApiResponse<ProductDetailDto>>($"/api/v1/products/{first.ProductId}");
        detail!.Success.Should().BeTrue();
        detail.Data!.ProductId.Should().Be(first.ProductId);
    }

    [Fact]
    public async Task Submit_Order_And_List_Orders()
    {
        // Seed seller + product
        Guid sellerId;
        Guid productId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();

            var seller = await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == "SELLER2@EXAMPLE.COM");
            if (seller is null)
            {
                seller = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = "seller2@example.com",
                    NormalizedEmail = "SELLER2@EXAMPLE.COM",
                    PasswordHash = new byte[] { 1 },
                    PasswordSalt = new byte[] { 2 },
                    IsActive = true,
                    ApprovedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    RowVer = Array.Empty<byte>()
                };
                db.Users.Add(seller);
            }

            var product = new Product
            {
                ProductId = Guid.NewGuid(),
                SellerUserId = seller.UserId,
                Sku = "SKU-2",
                Name = "Nut",
                CurrencyCode = "USD",
                DealerPrice = 0.50m,
                MsrpPrice = 0.75m,
                StockQuantity = 100,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            sellerId = seller.UserId;
            productId = product.ProductId;
        }

        // Register buyer + auth
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new { Email = "buyer2@example.com", Password = "P@ssw0rd!" });
        var regPayload = await reg.Content.ReadFromJsonAsync<ApiResponse<RegisterResult>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regPayload!.Data!.AccessToken);

        // sanity check: token validates with test key
        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(
            regPayload.Data!.AccessToken!,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "B2B.Api.Tests",
                ValidateAudience = true,
                ValidAudience = "B2B.Api.Tests.Client",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestKey)),
                ValidateLifetime = false
            },
            out _);

        // Submit order
        var submit = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            SellerUserId = sellerId,
            CurrencyCode = "USD",
            Items = new[] { new { ProductId = productId, Quantity = 2 } }
        });
        if (submit.StatusCode != HttpStatusCode.OK)
        {
            var body = await submit.Content.ReadAsStringAsync();
            var authHeader = submit.Headers.WwwAuthenticate.ToString();
            throw new Exception($"Expected 200 but got {(int)submit.StatusCode}. WWW-Authenticate='{authHeader}'. Body='{body}'");
        }

        var submitPayload = await submit.Content.ReadFromJsonAsync<ApiResponse<SubmitOrderResponse>>();
        submitPayload!.Success.Should().BeTrue();
        submitPayload.Data!.GrandTotal.Should().Be(1.00m);

        // List orders
        var list = await client.GetFromJsonAsync<ApiResponse<PagedResult<OrderListItemDto>>>("/api/v1/orders?page=1&pageSize=10");
        list!.Success.Should().BeTrue();
        list.Data!.Items.Should().NotBeEmpty();
    }

    // Local DTOs matching API output shapes
    private sealed record RegisterResult(string? AccessToken, string Message);
    private sealed record AuthResponse(string AccessToken);
    private sealed record ProductListItemDto(
        Guid ProductId,
        Guid SellerUserId,
        string SellerDisplayName,
        Guid? CategoryId,
        string? CategoryName,
        string? PrimaryImageUrl,
        string Sku,
        string Name,
        string CurrencyCode,
        decimal DealerPrice,
        decimal MsrpPrice,
        int StockQuantity,
        bool IsActive);

    private sealed record ProductImageDto(string Url, int SortOrder, bool IsPrimary);
    private sealed record ProductSpecDto(string Key, string Value, int SortOrder);

    private sealed record ProductDetailDto(
        Guid ProductId,
        Guid SellerUserId,
        string SellerDisplayName,
        Guid? CategoryId,
        string? CategoryName,
        IReadOnlyList<ProductImageDto> Images,
        IReadOnlyList<ProductSpecDto> Specs,
        string Sku,
        string Name,
        string? Description,
        string CurrencyCode,
        decimal DealerPrice,
        decimal MsrpPrice,
        int StockQuantity,
        bool IsActive);
    private sealed record SubmitOrderResponse(Guid OrderId, long OrderNumber, decimal GrandTotal);
    private sealed record OrderListItemDto(
        Guid OrderId,
        long OrderNumber,
        Guid SellerUserId,
        string? SellerDisplayName,
        string CurrencyCode,
        decimal GrandTotal,
        byte Status,
        DateTime CreatedAtUtc);
}
