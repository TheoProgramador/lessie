namespace Lessie.Application.Auth;

public interface IAuthService
{
    Task<AuthTokens> SignInWithGoogleAsync(string credential, ClientContext client, CancellationToken cancellationToken);
    Task<AuthTokens> SignInDevelopmentAdminAsync(ClientContext client, CancellationToken cancellationToken);
    Task<AuthTokens?> RefreshAsync(string refreshToken, ClientContext client, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task<UserProfile?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
