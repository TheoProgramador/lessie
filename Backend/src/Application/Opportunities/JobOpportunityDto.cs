namespace Lessie.Application.Opportunities;

public sealed class JobOpportunityDto
{
    public string ResultKey { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string RemoteType { get; init; } = string.Empty;
    public string EmploymentType { get; init; } = string.Empty;
    public string Salary { get; init; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; init; }
    public string Date { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Requirements { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string ApplyUrl { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string ContactSubject { get; init; } = string.Empty;
    public string Source { get; init; } = "APInfo";
    public string Provider { get; init; } = "APInfo";
}
