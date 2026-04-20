using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using B2B.Contracts;
using B2B.Domain.Entities;
using B2B.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Api.Tests;

/// <summary>
/// P1-4: kritik akışlar — yetki (403), sipariş idempotency, doğrulama (400).
/// </summary>
public sealed class CriticalFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CriticalFlowIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Dealer_token_cannot_list_admin_pending_dealers_returns_403()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = "dealer403@example.com",
            Password = "P@ssw0rd!",
            DisplayName = "Dealer"
        });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);
        var regPayload = await reg.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        regPayload!.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", regPayload.Data!.AccessToken);

        var adminList = await client.GetAsync("/api/v1/admin/users/pending-dealers");
        adminList.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_order_twice_with_same_idempotency_key_returns_same_order()
    {
        Guid sellerId;
        Guid productId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();
            var seller = await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == "SELLERIDEM@EXAMPLE.COM");
            if (seller is null)
            {
                seller = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = "selleridem@example.com",
                    NormalizedEmail = "SELLERIDEM@EXAMPLE.COM",
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
                Sku = "SKU-IDEM",
                Name = "Idem Nut",
                CurrencyCode = "USD",
                DealerPrice = 2.00m,
                MsrpPrice = 2.50m,
                StockQuantity = 50,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            sellerId = seller.UserId;
            productId = product.ProductId;
        }

        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = "buyeridem@example.com",
            Password = "P@ssw0rd!"
        });
        var regPayload = await reg.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", regPayload!.Data!.AccessToken);

        const string idemKey = "order-submit-idem-1";
        var body = new
        {
            SellerUserId = sellerId,
            CurrencyCode = "USD",
            Items = new[] { new { ProductId = productId, Quantity = 1 } }
        };

        using (var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(body) })
        {
            req1.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
            var r1 = await client.SendAsync(req1);
            r1.StatusCode.Should().Be(HttpStatusCode.OK);
            var p1 = await r1.Content.ReadFromJsonAsync<ApiResponse<SubmitOrderResponse>>();
            p1!.Success.Should().BeTrue();
            p1.Data!.OrderId.Should().NotBeEmpty();

            using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(body) };
            req2.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
            var r2 = await client.SendAsync(req2);
            r2.StatusCode.Should().Be(HttpStatusCode.OK);
            var p2 = await r2.Content.ReadFromJsonAsync<ApiResponse<SubmitOrderResponse>>();
            p2!.Data!.OrderId.Should().Be(p1.Data.OrderId);
            p2.Data.OrderNumber.Should().Be(p1.Data.OrderNumber);
            p2.Data.GrandTotal.Should().Be(p1.Data.GrandTotal);
        }
    }

    [Fact]
    public async Task Submit_order_with_empty_items_returns_400()
    {
        Guid sellerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();
            var seller = await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == "SELLEREMPTY@EXAMPLE.COM");
            if (seller is null)
            {
                seller = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = "sellerempty@example.com",
                    NormalizedEmail = "SELLEREMPTY@EXAMPLE.COM",
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

            sellerId = seller.UserId;
        }

        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = "buyerempty@example.com",
            Password = "P@ssw0rd!"
        });
        var regPayload = await reg.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", regPayload!.Data!.AccessToken);

        var submit = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            SellerUserId = sellerId,
            CurrencyCode = "USD",
            Items = Array.Empty<object>()
        });

        submit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await submit.Content.ReadFromJsonAsync<ApiResponse<object>>();
        payload!.Success.Should().BeFalse();
        // Model validation may run before controller (FluentValidation / binding); both indicate invalid body.
        payload.Error!.Code.Should().BeOneOf("empty_order", "validation_failed");
    }

}
