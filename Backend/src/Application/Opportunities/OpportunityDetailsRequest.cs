namespace Lessie.Application.Opportunities;

public sealed class OpportunityDetailsRequest
{
    public string JobId { get; init; } = string.Empty;
    public bool RevealContact { get; init; }
}
