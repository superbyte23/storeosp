using StoreOps.Application.Contracts.Auth;

namespace StoreOps.Application.Services;

public interface IAuthService
{
    Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<TokenResponse?> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);
    Task RevokeAsync(RefreshRequest request, CancellationToken cancellationToken);
}
