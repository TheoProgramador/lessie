using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Lessie.Infrastructure.ProviderKeys;

internal sealed class ProviderKeyEncryption(IConfiguration configuration)
{
    public string Encrypt(string value)
    {
        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return Convert.ToBase64String(nonce.Concat(tag).Concat(ciphertext).ToArray());
    }

    public string Decrypt(string encryptedValue)
    {
        var payload = Convert.FromBase64String(encryptedValue);
        var nonce = payload[..12];
        var tag = payload[12..28];
        var ciphertext = payload[28..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(GetKey(), 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] GetKey()
    {
        var secret = configuration["PROVIDER_KEY_ENCRYPTION_KEY"] ?? configuration["ProviderKeys:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("ProviderKeys:EncryptionKey must be configured with at least 32 characters.");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }
}
