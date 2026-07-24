using Lessie.Application.ProviderKeys;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Infrastructure.ProviderKeys;

internal sealed class ProviderKeyService(
    LessieDbContext dbContext,
    ProviderKeyEncryption encryption) : IProviderKeyService
{
    private const string GroqProvider = "Groq";
    private const string PollinationsProvider = "Pollinations";

    public async Task<IReadOnlyCollection<ProviderKeyStatus>> GetStatusesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var keys = await dbContext.UserProviderApiKeys
            .Where(key => key.IsActive)
            .OrderByDescending(key => key.UpdatedAt)
            .ToListAsync(cancellationToken);

        var groqKey = keys.FirstOrDefault(key => IsProvider(key.Provider, GroqProvider) && key.IsActive);
        var pollinationsKey = keys.FirstOrDefault(key => IsProvider(key.Provider, PollinationsProvider) && key.IsActive);
        return
        [
            new ProviderKeyStatus(GroqProvider, groqKey is not null, groqKey?.LastUsedAt),
            new ProviderKeyStatus(PollinationsProvider, pollinationsKey is not null, pollinationsKey?.LastUsedAt)
        ];
    }

    public async Task<ProviderKeyStatus> SaveGroqKeyAsync(Guid userId, string apiKey, CancellationToken cancellationToken)
    {
        return await SaveKeyAsync(userId, GroqProvider, apiKey, cancellationToken);
    }

    public async Task<ProviderKeyStatus> SavePollinationsKeyAsync(Guid userId, string apiKey, CancellationToken cancellationToken)
    {
        return await SaveKeyAsync(userId, PollinationsProvider, apiKey, cancellationToken);
    }

    public async Task<ProviderKeyStatus> DeleteGroqKeyAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await DeleteKeyAsync(userId, GroqProvider, cancellationToken);
    }

    public async Task<ProviderKeyStatus> DeletePollinationsKeyAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await DeleteKeyAsync(userId, PollinationsProvider, cancellationToken);
    }

    public async Task<string?> GetActiveGroqKeyAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await GetActiveKeyAsync(userId, GroqProvider, cancellationToken);
    }

    public async Task<string?> GetActivePollinationsKeyAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await GetActiveKeyAsync(userId, PollinationsProvider, cancellationToken);
    }

    public async Task MarkGroqKeyUsedAsync(Guid userId, CancellationToken cancellationToken)
    {
        await MarkKeyUsedAsync(userId, GroqProvider, cancellationToken);
    }

    public async Task MarkPollinationsKeyUsedAsync(Guid userId, CancellationToken cancellationToken)
    {
        await MarkKeyUsedAsync(userId, PollinationsProvider, cancellationToken);
    }

    private async Task<ProviderKeyStatus> SaveKeyAsync(Guid userId, string provider, string apiKey, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var providerKeys = await dbContext.UserProviderApiKeys
            .Where(key => key.Provider.ToLower() == provider.ToLower())
            .OrderByDescending(key => key.IsActive)
            .ThenByDescending(key => key.UpdatedAt)
            .ToListAsync(cancellationToken);
        var existing = providerKeys.FirstOrDefault();

        if (existing is null)
        {
            existing = new UserProviderApiKey
            {
                UserId = userId,
                Provider = provider,
                EncryptedApiKey = encryption.Encrypt(apiKey),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true
            };
            dbContext.UserProviderApiKeys.Add(existing);
        }
        else
        {
            existing.Provider = provider;
            existing.EncryptedApiKey = encryption.Encrypt(apiKey);
            existing.UpdatedAt = now;
            existing.IsActive = true;
        }

        foreach (var duplicate in providerKeys.Where(key => key.Id != existing.Id && key.IsActive))
        {
            duplicate.IsActive = false;
            duplicate.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProviderKeyStatus(provider, true, existing.LastUsedAt);
    }

    private async Task<ProviderKeyStatus> DeleteKeyAsync(Guid userId, string provider, CancellationToken cancellationToken)
    {
        var existingKeys = await dbContext.UserProviderApiKeys
            .Where(key => key.Provider.ToLower() == provider.ToLower() && key.IsActive)
            .ToListAsync(cancellationToken);

        if (existingKeys.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var existing in existingKeys)
            {
                existing.IsActive = false;
                existing.UpdatedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ProviderKeyStatus(provider, false, null);
    }

    private async Task<string?> GetActiveKeyAsync(Guid userId, string provider, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UserProviderApiKeys
            .AsNoTracking()
            .Where(key => key.Provider.ToLower() == provider.ToLower() && key.IsActive)
            .OrderByDescending(key => key.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return existing is null ? null : encryption.Decrypt(existing.EncryptedApiKey);
    }

    private async Task MarkKeyUsedAsync(Guid userId, string provider, CancellationToken cancellationToken)
    {
        var existing = await dbContext.UserProviderApiKeys
            .Where(key => key.Provider.ToLower() == provider.ToLower() && key.IsActive)
            .OrderByDescending(key => key.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        existing.LastUsedAt = now;
        existing.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsProvider(string provider, string expected)
    {
        return string.Equals(provider.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }
}
