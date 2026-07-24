namespace Lessie.Application.Opportunities;

public sealed class OpportunitySearchRequest
{
    public string Query { get; init; } = string.Empty;
    public string? Location { get; init; }
    public int Limit { get; init; } = 20;
    public IReadOnlyCollection<string>? SiteNames { get; init; }
    public int? HoursOld { get; init; }
    public string? JobType { get; init; }
    public bool EasyApply { get; init; }
    public bool IsRemote { get; init; }
}
