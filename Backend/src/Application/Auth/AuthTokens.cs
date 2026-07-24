namespace Lessie.Application.Auth;

public sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);
