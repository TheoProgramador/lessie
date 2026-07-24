namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoveryJobSearchResponse
{
    public bool Success { get; init; }
    public string Source { get; init; } = "mcp";
    public string ToolName { get; init; } = "jobs.search";
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyCollection<PeopleDiscoveryJobDto> Results { get; init; } = [];
    public string? Error { get; init; }
}
