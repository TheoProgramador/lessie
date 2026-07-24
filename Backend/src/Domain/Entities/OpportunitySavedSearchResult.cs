namespace Lessie.Domain.Entities;

public sealed class OpportunitySavedSearchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OpportunitySavedSearchId { get; set; }
    public string ResultKey { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ApplyUrl { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactSubject { get; set; } = string.Empty;
    public string Source { get; set; } = "APInfo";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public OpportunitySavedSearch? OpportunitySavedSearch { get; set; }
}
