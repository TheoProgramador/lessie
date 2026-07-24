namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoveryJobSearchRequest
{
    public string Keywords { get; init; } = string.Empty;
    public string? Location { get; init; }
    public int MaxPages { get; init; } = 5;
    public string? DatePosted { get; init; }
    public string? JobType { get; init; }
    public string? ExperienceLevel { get; init; }
    public string? WorkType { get; init; }
    public bool EasyApply { get; init; }
    public string? SortBy { get; init; }
}
