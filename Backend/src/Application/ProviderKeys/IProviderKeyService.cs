namespace Lessie.Application.ProviderKeys;

public interface IProviderKeyService
{
    Task<IReadOnlyCollection<ProviderKeyStatus>> GetStatusesAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProviderKeyStatus> SaveGroqKeyAsync(Guid userId, string apiKey, CancellationToken cancellationToken);
    Task<ProviderKeyStatus> DeleteGroqKeyAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> GetActiveGroqKeyAsync(Guid userId, CancellationToken cancellationToken);
    Task MarkGroqKeyUsedAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProviderKeyStatus> SavePollinationsKeyAsync(Guid userId, string apiKey, CancellationToken cancellationToken);
    Task<ProviderKeyStatus> DeletePollinationsKeyAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> GetActivePollinationsKeyAsync(Guid userId, CancellationToken cancellationToken);
    Task MarkPollinationsKeyUsedAsync(Guid userId, CancellationToken cancellationToken);
}
