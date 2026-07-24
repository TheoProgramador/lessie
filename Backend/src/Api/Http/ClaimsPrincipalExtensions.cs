using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Lessie.Api.Http;

internal static class ClaimsPrincipalExtensions
{
    public static bool TryGetCurrentUserId(this ClaimsPrincipal user, out Guid userId)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("nameid");

        return Guid.TryParse(subject, out userId);
    }
}
