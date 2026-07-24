using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Lessie.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Lessie.Infrastructure.Auth;

internal sealed class JwtTokenService(IConfiguration configuration)
{
    public int ExpiresInSeconds => AccessTokenMinutes * 60;

    private int AccessTokenMinutes
        => int.TryParse(configuration["JWT_ACCESS_TOKEN_MINUTES"] ?? configuration["Jwt:AccessTokenMinutes"], out var minutes)
            ? minutes
            : 15;

    public string CreateAccessToken(User user)
    {
        var secret = configuration["JWT_SECRET"] ?? configuration["Jwt:Secret"];
        var issuer = configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"];
        var audience = configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("JWT_SECRET must be configured with at least 32 characters.");
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new("is_admin", user.IsAdmin ? "true" : "false")
        };

        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        if (!string.IsNullOrWhiteSpace(user.PictureUrl))
        {
            claims.Add(new Claim("picture", user.PictureUrl));
        }

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
