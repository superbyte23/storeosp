using Microsoft.AspNetCore.Authorization;
using StoreOps.Application.Contracts.Auth;
using StoreOps.Application.Services;

namespace StoreOps.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService auth,
            CancellationToken cancellationToken) =>
        {
            var tokens = await auth.LoginAsync(request, cancellationToken);
            return tokens is null
                ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid email or password.")
                : Results.Ok(tokens);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/refresh", async (
            RefreshRequest request,
            IAuthService auth,
            CancellationToken cancellationToken) =>
        {
            var tokens = await auth.RefreshAsync(request, cancellationToken);
            return tokens is null
                ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                    title: "Refresh token is invalid or expired.")
                : Results.Ok(tokens);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/logout", async (
            RefreshRequest request,
            IAuthService auth,
            CancellationToken cancellationToken) =>
        {
            await auth.RevokeAsync(request, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization();

        return group;
    }
}
