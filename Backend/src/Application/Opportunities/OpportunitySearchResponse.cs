namespace Lessie.Application.Opportunities;

public sealed class OpportunitySearchResponse
{
    public bool Success { get; init; }
    public string Source { get; init; } = "APInfo";
    public string ToolName { get; init; } = "opportunity.search";
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyCollection<JobOpportunityDto> Results { get; init; } = [];
    public string? Error { get; init; }
}
