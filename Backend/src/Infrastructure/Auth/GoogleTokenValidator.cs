using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace Lessie.Infrastructure.Auth;

internal sealed class GoogleTokenValidator(IConfiguration configuration)
{
    public async Task<GoogleUserInfo?> ValidateAsync(string credential, CancellationToken cancellationToken)
    {
        var clientId = configuration["GOOGLE_CLIENT_ID"] ?? configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("GOOGLE_CLIENT_ID is not configured.");
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(credential, settings);
            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            var name = string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name;
            return new GoogleUserInfo(payload.Subject, payload.Email, name, payload.Picture);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
