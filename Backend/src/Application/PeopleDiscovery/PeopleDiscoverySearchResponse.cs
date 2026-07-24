namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoverySearchResponse
{
    public bool Success { get; set; }
    public string Source { get; set; } = "mcp";
    public string ToolName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyCollection<PeopleDiscoveryPersonDto> Results { get; set; } = [];
    public string? Error { get; set; }
}
