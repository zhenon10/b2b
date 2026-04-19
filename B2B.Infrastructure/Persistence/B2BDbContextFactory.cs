using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace B2B.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations (dotnet-ef).
/// </summary>
public sealed class B2BDbContextFactory : IDesignTimeDbContextFactory<B2BDbContext>
{
    public B2BDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<B2BDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=B2B;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new B2BDbContext(options);
    }
}

