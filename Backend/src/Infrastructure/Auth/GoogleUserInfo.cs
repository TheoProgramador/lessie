namespace Lessie.Infrastructure.Auth;

internal sealed record GoogleUserInfo(string GoogleId, string Email, string Name, string? PictureUrl);
