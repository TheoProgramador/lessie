namespace Lessie.Domain.Entities;

public sealed class PeopleDiscoverySavedSearch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeopleDiscoverySearchTextId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastRunAt { get; set; } = DateTimeOffset.UtcNow;
    public int RunCount { get; set; } = 1;

    public PeopleDiscoverySearchText? SearchText { get; set; }
    public ICollection<PeopleDiscoverySavedSearchResult> Results { get; set; } = [];
}
