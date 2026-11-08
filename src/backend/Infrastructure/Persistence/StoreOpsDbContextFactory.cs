using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StoreOps.Infrastructure.Persistence;

public sealed class StoreOpsDbContextFactory : IDesignTimeDbContextFactory<StoreOpsDbContext>
{
    public StoreOpsDbContext CreateDbContext(string[] args)
    {
        var apiDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Api"));
        var defaultDatabasePath = Path.Combine(apiDirectory, "data", "storeops.db");
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__StoreOps")
            ?? $"Data Source={defaultDatabasePath}";
        var options = new DbContextOptionsBuilder<StoreOpsDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new StoreOpsDbContext(options);
    }
}
