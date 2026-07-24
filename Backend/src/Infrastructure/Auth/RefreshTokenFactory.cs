using System.Security.Cryptography;
using System.Text;

namespace Lessie.Infrastructure.Auth;

internal static class RefreshTokenFactory
{
    public static string CreateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
