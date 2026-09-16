using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RestaurantManagement.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migration tooling.
/// </summary>
public class RestaurantDbContextFactory : IDesignTimeDbContextFactory<RestaurantDbContext>
{
    public RestaurantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RestaurantDbContext>();
        var connectionString = DatabasePathResolver.ResolveConnectionString(string.Empty);

        optionsBuilder.UseSqlite(connectionString);

        return new RestaurantDbContext(optionsBuilder.Options);
    }
}
