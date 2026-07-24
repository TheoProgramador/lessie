using Lessie.Application.Auth;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Lessie.Infrastructure.Auth;

internal sealed class AuthService(
    LessieDbContext dbContext,
    GoogleTokenValidator googleTokenValidator,
    JwtTokenService jwtTokenService,
    IConfiguration configuration) : IAuthService
{
    public async Task<AuthTokens> SignInWithGoogleAsync(string credential, ClientContext client, CancellationToken cancellationToken)
    {
        var googleUser = await googleTokenValidator.ValidateAsync(credential, cancellationToken);
        if (googleUser is null)
        {
            throw new UnauthorizedAccessException("Invalid Google credential.");
        }

        var now = DateTimeOffset.UtcNow;
        var isInitialAdmin = IsInitialAdminEmail(googleUser.Email);
        var user = await dbContext.Users
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.GoogleId == googleUser.GoogleId || x.Email == googleUser.Email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Name = googleUser.Name,
                Email = googleUser.Email,
                GoogleId = googleUser.GoogleId,
                PictureUrl = googleUser.PictureUrl,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now,
                IsActive = true,
                IsAdmin = isInitialAdmin,
                Subscription = new UserSubscription
                {
                    IsPaid = isInitialAdmin,
                    PaidUntil = isInitialAdmin ? now.AddYears(10) : null,
                    LastPaymentAt = isInitialAdmin ? now : null,
                    PaymentProvider = isInitialAdmin ? "system" : "",
                    ExternalReference = isInitialAdmin ? "seed-admin" : "",
                    Notes = isInitialAdmin ? "Administrador inicial do sistema." : "",
                    ResumeAnalysisCount = 0,
                    ResumeAnalysisLimit = 20,
                    ChatConversationCount = 0,
                    ChatConversationLimit = 50,
                    InterviewAnalysisCount = 0,
                    InterviewAnalysisLimit = 5,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };

            dbContext.Users.Add(user);
        }
        else
        {
            user.Name = googleUser.Name;
            user.Email = googleUser.Email;
            user.GoogleId ??= googleUser.GoogleId;
            user.PictureUrl = googleUser.PictureUrl;
            user.UpdatedAt = now;
            user.LastLoginAt = now;
            user.IsActive = true;
            user.IsAdmin = user.IsAdmin || isInitialAdmin;

            if (user.Subscription is null)
            {
                user.Subscription = new UserSubscription
                {
                    UserId = user.Id,
                    IsPaid = user.IsAdmin,
                    PaidUntil = user.IsAdmin ? now.AddYears(10) : null,
                    LastPaymentAt = user.IsAdmin ? now : null,
                    PaymentProvider = user.IsAdmin ? "system" : "",
                    ExternalReference = user.IsAdmin ? "seed-admin" : "",
                    Notes = user.IsAdmin ? "Administrador do sistema." : "",
                    ResumeAnalysisCount = 0,
                    ResumeAnalysisLimit = 20,
                    ChatConversationCount = 0,
                    ChatConversationLimit = 50,
                    InterviewAnalysisCount = 0,
                    InterviewAnalysisLimit = 5,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.UserSubscriptions.Add(user.Subscription);
            }
            else if (user.IsAdmin)
            {
                user.Subscription.IsPaid = true;
                user.Subscription.PaidUntil = user.Subscription.PaidUntil is null || user.Subscription.PaidUntil < now.AddYears(1)
                    ? now.AddYears(10)
                    : user.Subscription.PaidUntil;
                user.Subscription.PaymentProvider = string.IsNullOrWhiteSpace(user.Subscription.PaymentProvider) ? "system" : user.Subscription.PaymentProvider;
                user.Subscription.UpdatedAt = now;
            }
        }

        var tokens = CreateTokens(user, client);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tokens;
    }

    public async Task<AuthTokens> SignInDevelopmentAdminAsync(ClientContext client, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await dbContext.Users
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.Email == DevelopmentAdminEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Name = "Theo Miliani",
                Email = DevelopmentAdminEmail,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now,
                IsActive = true,
                IsAdmin = true
            };

            dbContext.Users.Add(user);
        }
        else
        {
            user.Name = string.IsNullOrWhiteSpace(user.Name) ? "Theo Miliani" : user.Name;
            user.UpdatedAt = now;
            user.LastLoginAt = now;
            user.IsActive = true;
            user.IsAdmin = true;
        }

        EnsureAdminSubscription(user, now);

        var tokens = CreateTokens(user, client);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tokens;
    }

    public async Task<AuthTokens?> RefreshAsync(string refreshToken, ClientContext client, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenFactory.Hash(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken?.User is null || !storedToken.User.IsActive || !storedToken.IsActive)
        {
            return null;
        }

        var tokens = CreateTokens(storedToken.User, client);
        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.ReplacedByTokenHash = RefreshTokenFactory.Hash(tokens.RefreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return tokens;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenFactory.Hash(refreshToken);
        var storedToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken is not null && storedToken.RevokedAt is null)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserProfile?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.Users
            .Include(x => x.Subscription)
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new UserProfile(
                x.Id,
                x.Name,
                x.Email,
                x.PictureUrl,
                x.IsAdmin,
                x.IsAdmin || (x.Subscription != null && x.Subscription.PaidUntil != null && x.Subscription.PaidUntil >= now),
                x.Subscription != null && x.Subscription.IsPaid,
                x.Subscription != null ? x.Subscription.PaidUntil : null,
                x.Subscription != null ? x.Subscription.ResumeAnalysisCount : 0,
                x.Subscription != null ? x.Subscription.ResumeAnalysisLimit : 20,
                x.Subscription != null ? x.Subscription.ChatConversationCount : 0,
                x.Subscription != null ? x.Subscription.ChatConversationLimit : 50,
                x.Subscription != null ? x.Subscription.InterviewAnalysisCount : 0,
                x.Subscription != null ? x.Subscription.InterviewAnalysisLimit : 5,
                x.Subscription != null ? x.Subscription.CreditBalance : 0,
                x.Subscription != null ? x.Subscription.TotalCreditsPurchased : 0))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private AuthTokens CreateTokens(User user, ClientContext client)
    {
        var refreshToken = RefreshTokenFactory.CreateToken();
        var refreshTokenDays = int.TryParse(configuration["REFRESH_TOKEN_DAYS"] ?? configuration["Jwt:RefreshTokenDays"], out var days)
            ? days
            : 30;

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = RefreshTokenFactory.Hash(refreshToken),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(refreshTokenDays),
            IpAddress = client.IpAddress,
            UserAgent = client.UserAgent
        });

        return new AuthTokens(jwtTokenService.CreateAccessToken(user), refreshToken, jwtTokenService.ExpiresInSeconds);
    }

    private void EnsureAdminSubscription(User user, DateTimeOffset now)
    {
        if (user.Subscription is null)
        {
            user.Subscription = new UserSubscription
            {
                UserId = user.Id,
                CreatedAt = now
            };
            dbContext.UserSubscriptions.Add(user.Subscription);
        }

        user.Subscription.IsPaid = true;
        user.Subscription.PaidUntil = user.Subscription.PaidUntil is null || user.Subscription.PaidUntil < now.AddYears(1)
            ? now.AddYears(10)
            : user.Subscription.PaidUntil;
        user.Subscription.LastPaymentAt ??= now;
        user.Subscription.PaymentProvider = string.IsNullOrWhiteSpace(user.Subscription.PaymentProvider) ? "system" : user.Subscription.PaymentProvider;
        user.Subscription.ExternalReference = string.IsNullOrWhiteSpace(user.Subscription.ExternalReference) ? "dev-admin" : user.Subscription.ExternalReference;
        user.Subscription.Notes = string.IsNullOrWhiteSpace(user.Subscription.Notes) ? "Administrador de desenvolvimento." : user.Subscription.Notes;
        user.Subscription.ResumeAnalysisLimit = user.Subscription.ResumeAnalysisLimit == 0 ? 20 : user.Subscription.ResumeAnalysisLimit;
        user.Subscription.ChatConversationLimit = user.Subscription.ChatConversationLimit == 0 ? 50 : user.Subscription.ChatConversationLimit;
        user.Subscription.InterviewAnalysisLimit = user.Subscription.InterviewAnalysisLimit == 0 ? 5 : user.Subscription.InterviewAnalysisLimit;
        user.Subscription.UpdatedAt = now;
    }

    private static bool IsInitialAdminEmail(string email)
        => InitialAdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);

    private const string DevelopmentAdminEmail = "theo.miliani@gmail.com";

    private static readonly string[] InitialAdminEmails =
    [
        DevelopmentAdminEmail
    ];
}
