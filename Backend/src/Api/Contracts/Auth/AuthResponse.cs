namespace Lessie.Api.Contracts.Auth;

public sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn);
