namespace Lessie.Application.ProviderKeys;

public sealed record ProviderKeyStatus(string Provider, bool Configured, DateTimeOffset? LastUsedAt);
