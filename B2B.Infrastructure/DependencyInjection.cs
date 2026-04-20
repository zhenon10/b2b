using B2B.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? environmentName)
    {
        var connectionString = configuration.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Tests override DbContext registration in the test host.
            if (string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
                return services;

            throw new InvalidOperationException(
                "ConnectionStrings:SqlServer is not configured. " +
                "Set ConnectionStrings__SqlServer as an environment variable (recommended) or configure it in appsettings.");
        }

        services.AddDbContext<B2BDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure();
            }));

        return services;
    }
}

