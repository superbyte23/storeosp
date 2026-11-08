namespace StoreOps.Application.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
public sealed record RefreshRequest(string RefreshToken);
