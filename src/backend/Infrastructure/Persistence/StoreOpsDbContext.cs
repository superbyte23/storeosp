using Microsoft.EntityFrameworkCore;
using StoreOps.Domain.Entities;

namespace StoreOps.Infrastructure.Persistence;

public sealed class StoreOpsDbContext(DbContextOptions<StoreOpsDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasIndex(category => category.Name).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(product => product.Sku).IsUnique();
        modelBuilder.Entity<Product>().Property(product => product.UnitPrice).HasPrecision(12, 2);
        modelBuilder.Entity<Product>().HasOne(product => product.Category)
            .WithMany(category => category.Products)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Order>().Property(order => order.Status).HasConversion<string>();
        modelBuilder.Entity<OrderItem>().Property(item => item.UnitPrice).HasPrecision(12, 2);
        modelBuilder.Entity<OrderItem>().HasOne(item => item.Order)
            .WithMany(order => order.Items)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<OrderItem>().HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Email).HasMaxLength(254).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(254).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(200).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(role => new { role.UserId, role.Role });
            entity.Property(role => role.Role).HasMaxLength(32);
            entity.HasOne(role => role.User)
                .WithMany(user => user.Roles)
                .HasForeignKey(role => role.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);
            entity.Property(token => token.ConcurrencyStamp).HasMaxLength(32).IsConcurrencyToken();
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.UserId, token.FamilyId, token.RevokedAtUtc });
            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
