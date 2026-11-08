using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using StoreOps.Domain.Entities;
using StoreOps.Infrastructure.Auth;
using StoreOps.Infrastructure.Persistence;

namespace StoreOps.Infrastructure.Seeding;

public static class StoreOpsSeeder
{
    private const string InitialMigrationId = "20261108075422_InitialCreate";

    public static async Task SeedStoreOpsAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreOpsDbContext>();
        await AdoptEnsureCreatedDatabaseAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.Categories.AnyAsync(cancellationToken))
        {
            var pantry = new Category { Name = "Pantry" };
            var beverages = new Category { Name = "Beverages" };
            db.Categories.AddRange(pantry, beverages);
            db.Products.AddRange(
                new Product { Sku = "PAN-001", Name = "Organic rolled oats", Category = pantry, UnitPrice = 6.50m, StockQuantity = 42 },
                new Product { Sku = "PAN-002", Name = "Raw honey", Category = pantry, UnitPrice = 9.75m, StockQuantity = 18 },
                new Product { Sku = "BEV-001", Name = "Cold brew coffee", Category = beverages, UnitPrice = 4.25m, StockQuantity = 7 },
                new Product { Sku = "BEV-002", Name = "Sparkling water", Category = beverages, UnitPrice = 2.00m, StockQuantity = 3 });
            await db.SaveChangesAsync(cancellationToken);
        }

        var adminEmail = configuration["BootstrapAdmin:Email"];
        var adminPassword = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(adminEmail) != string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException(
                "Configure both BootstrapAdmin:Email and BootstrapAdmin:Password, or neither.");
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            if (adminEmail.Length > 254 || adminPassword.Length < 12 ||
                System.Text.Encoding.UTF8.GetByteCount(adminPassword) > 72)
                throw new InvalidOperationException(
                    "Bootstrap admin email must be at most 254 characters and password must be 12–72 UTF-8 bytes.");

            var repository = scope.ServiceProvider.GetRequiredService<UserRepository>();
            if (await repository.FindByEmailAsync(adminEmail, cancellationToken) is null)
            {
                var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
                await repository.SeedBootstrapAdminAsync(
                    adminEmail,
                    hasher.Hash(adminPassword),
                    cancellationToken);
            }
        }
    }

    private static async Task AdoptEnsureCreatedDatabaseAsync(
        StoreOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
            await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ('Categories', 'Products', 'Orders', 'OrderItems')
                """;
            var existingTables = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (existingTables == 0)
                return;
            if (existingTables != 4)
                throw new InvalidOperationException(
                    "The existing SQLite database has an incomplete StoreOps schema; refusing to alter it automatically.");

            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
            var historyExists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
            if (historyExists)
                return;

            command.CommandText = """
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                )
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText =
                "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@id, @version)";
            var migrationId = command.CreateParameter();
            migrationId.ParameterName = "@id";
            migrationId.Value = InitialMigrationId;
            command.Parameters.Add(migrationId);
            var productVersion = command.CreateParameter();
            productVersion.ParameterName = "@version";
            productVersion.Value = "10.0.14";
            command.Parameters.Add(productVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }
}
