using Microsoft.Extensions.Logging;
using System.Text;
using StoreOps.Application.Contracts.Auth;
using StoreOps.Application.Services;
using StoreOps.Domain.Entities;

namespace StoreOps.Infrastructure.Auth;

public sealed class AuthService(
    UserRepository users,
    PasswordHasher passwordHasher,
    JwtTokenService tokens,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<TokenResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            request.Email.Length > 254 ||
            string.IsNullOrEmpty(request.Password) ||
            Encoding.UTF8.GetByteCount(request.Password) > 72)
            return null;

        var user = await users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive ||
            !passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        var refreshToken = tokens.CreateRefreshToken();
        await users.AddRefreshTokenAsync(
            user.Id,
            tokens.HashRefreshToken(refreshToken),
            tokens.GetRefreshTokenExpiry(),
            cancellationToken);

        return CreateTokenResponse(user, refreshToken);
    }

    public async Task<TokenResponse?> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) ||
            request.RefreshToken.Length > 256)
            return null;

        var replacement = tokens.CreateRefreshToken();
        var result = await users.RotateRefreshTokenAsync(
            tokens.HashRefreshToken(request.RefreshToken),
            tokens.HashRefreshToken(replacement),
            tokens.GetRefreshTokenExpiry(),
            cancellationToken);

        if (result.Status == RefreshRotationStatus.ReuseDetected)
        {
            logger.LogWarning(
                "Refresh token reuse detected; active refresh tokens were revoked for user {UserId}.",
                result.UserId);
            return null;
        }

        return result.Status == RefreshRotationStatus.Rotated && result.User is not null
            ? CreateTokenResponse(result.User, replacement)
            : null;
    }

    public async Task RevokeAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) ||
            request.RefreshToken.Length > 256)
            return;

        await users.RevokeRefreshTokenAsync(
            tokens.HashRefreshToken(request.RefreshToken),
            cancellationToken);
    }

    private TokenResponse CreateTokenResponse(User user, string refreshToken) =>
        new(tokens.CreateAccessToken(user), refreshToken, tokens.ExpiresIn);
}
