namespace Lessie.Domain.Entities;

public sealed class OpportunitySearchText
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public string QueryKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public ICollection<OpportunitySavedSearch> SavedSearches { get; set; } = [];
}
