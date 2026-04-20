using B2B.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace B2B.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Deterministic test JWT configuration (no leakage from appsettings.json)
    public const string TestIssuer = "B2B.Api.Tests";
    public const string TestAudience = "B2B.Api.Tests.Client";
    public const string TestKey = "TEST_SIGNING_KEY__THIS_MUST_BE_AT_LEAST_32_CHARS";

    private const string TestDbName = "B2B_Test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = $"Server=(localdb)\\MSSQLLocalDB;Database={TestDbName};Trusted_Connection=True;TrustServerCertificate=True",
                // Explicit JWT values for tests (also include alias keys)
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:Key"] = TestKey,
                ["Jwt:SigningKey"] = TestKey,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "14",
                // Minimal seed for tests: roles only (Register assigns Dealer role)
                ["Seed:Mode"] = "RolesOnly",
                ["Auth:AllowPublicRegistration"] = "true",
                ["Auth:AutoApproveRegisteredDealers"] = "true"
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<B2BDbContext>));
            services.RemoveAll(typeof(B2BDbContext));

            services.AddDbContext<B2BDbContext>(options =>
                options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={TestDbName};Trusted_Connection=True;TrustServerCertificate=True"));

            // Ensure schema exists
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();
                db.Database.EnsureDeleted();
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}

