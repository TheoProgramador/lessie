namespace Lessie.Domain.Entities;

public sealed class PeopleDiscoverySavedSearchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeopleDiscoverySavedSearchId { get; set; }
    public string ResultKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool ResumeSent { get; set; }
    public DateTimeOffset? ResumeSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public PeopleDiscoverySavedSearch? PeopleDiscoverySavedSearch { get; set; }
}
