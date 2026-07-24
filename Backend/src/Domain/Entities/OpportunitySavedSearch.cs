namespace Lessie.Domain.Entities;

public sealed class OpportunitySavedSearch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OpportunitySearchTextId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastRunAt { get; set; } = DateTimeOffset.UtcNow;
    public int RunCount { get; set; } = 1;

    public OpportunitySearchText? SearchText { get; set; }
    public ICollection<OpportunitySavedSearchResult> Results { get; set; } = [];
}
