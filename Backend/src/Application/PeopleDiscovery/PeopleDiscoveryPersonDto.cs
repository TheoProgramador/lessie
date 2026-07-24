namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoveryPersonDto
{
    public string ResultKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string ContactInfo { get; init; } = string.Empty;
    public string ProfileUrl { get; init; } = string.Empty;
    public string Source { get; init; } = "LinkedIn";
    public bool ResumeSent { get; init; }
}
