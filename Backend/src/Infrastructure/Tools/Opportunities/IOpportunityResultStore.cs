using Lessie.Application.Opportunities;

namespace Lessie.Infrastructure.Tools.Opportunities;

public interface IOpportunityResultStore
{
    Task<IReadOnlyCollection<JobOpportunityDto>> SaveSearchResultsAsync(
        Guid userId,
        string query,
        IReadOnlyCollection<JobOpportunityDto> results,
        CancellationToken cancellationToken);

    Task<JobOpportunityDto?> SaveDetailsAsync(
        Guid userId,
        string query,
        JobOpportunityDto result,
        CancellationToken cancellationToken);
}
