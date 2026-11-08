using Microsoft.EntityFrameworkCore;
using StoreOps.Domain.Entities;
using StoreOps.Infrastructure.Persistence;

namespace StoreOps.Infrastructure.Auth;

public enum RefreshRotationStatus
{
    Rotated,
    Invalid,
    ReuseDetected
}

public sealed record RefreshRotationResult(
    RefreshRotationStatus Status,
    User? User,
    Guid? UserId = null);

public sealed class UserRepository(StoreOpsDbContext db)
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.Include(user => user.Roles)
            .SingleOrDefaultAsync(
                user => user.NormalizedEmail == NormalizeEmail(email),
                cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRefreshTokenAsync(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshRotationResult> RotateRefreshTokenAsync(
        string currentHash,
        string replacementHash,
        DateTime replacementExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await db.RefreshTokens
            .Include(token => token.User)
            .ThenInclude(user => user.Roles)
            .SingleOrDefaultAsync(token => token.TokenHash == currentHash, cancellationToken);

        if (current is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.Invalid, null);
        }

        if (current.RevokedAtUtc is not null)
        {
            await RevokeActiveTokensAsync(current.UserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.ReuseDetected, null, current.UserId);
        }

        var now = DateTime.UtcNow;
        if (current.ExpiresAtUtc <= now || !current.User.IsActive)
        {
            current.RevokedAtUtc = now;
            current.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.Invalid, null);
        }

        current.RevokedAtUtc = now;
        current.ReplacedByTokenHash = replacementHash;
        current.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = current.UserId,
            FamilyId = current.FamilyId,
            TokenHash = replacementHash,
            ExpiresAtUtc = replacementExpiresAtUtc,
            CreatedAtUtc = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.Rotated, current.User);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            await RevokeAllForUserAsync(current.UserId, cancellationToken);
            return new RefreshRotationResult(RefreshRotationStatus.ReuseDetected, null);
        }
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(token => token.TokenHash == tokenHash && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAtUtc, now)
                .SetProperty(token => token.ConcurrencyStamp, Guid.NewGuid().ToString("N")),
                cancellationToken);
    }

    public async Task SeedBootstrapAdminAsync(
        string email,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        var existing = await FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
            return;

        var admin = new User
        {
            Email = email.Trim(),
            NormalizedEmail = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Roles = { new UserRole { Role = "Admin" } }
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeActiveTokensAsync(Guid userId, CancellationToken cancellationToken) =>
        await RevokeAllForUserAsync(userId, cancellationToken);

    private async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAtUtc, now)
                .SetProperty(token => token.ConcurrencyStamp, Guid.NewGuid().ToString("N")),
                cancellationToken);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
