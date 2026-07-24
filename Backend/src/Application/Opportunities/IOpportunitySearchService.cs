namespace Lessie.Application.Opportunities;

public interface IOpportunitySearchService
{
    Task<IReadOnlyCollection<JobOpportunityDto>> SearchAsync(
        OpportunitySearchRequest request,
        Guid userId,
        CancellationToken cancellationToken);

    Task<JobOpportunityDto?> GetDetailsAsync(
        OpportunityDetailsRequest request,
        Guid userId,
        CancellationToken cancellationToken);
}
