namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoveryJobDto
{
    public string ResultKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string JobId { get; init; } = string.Empty;
    public string JobUrl { get; init; } = string.Empty;
    public string Insight { get; init; } = string.Empty;
    public string Metadata { get; init; } = string.Empty;
    public string Source { get; init; } = "LinkedIn Jobs";
    public bool ResumeSent { get; init; }
}
