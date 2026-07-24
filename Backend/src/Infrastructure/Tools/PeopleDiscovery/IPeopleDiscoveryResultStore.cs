using Lessie.Application.PeopleDiscovery;

namespace Lessie.Infrastructure.Tools.PeopleDiscovery;

public interface IPeopleDiscoveryResultStore
{
    Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> FindPreviousResultsAsync(
        Guid userId,
        string query,
        string source,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> SaveAndFilterAsync(
        Guid userId,
        string query,
        IReadOnlyCollection<PeopleDiscoveryPersonDto> results,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PeopleDiscoveryJobDto>> SaveAndFilterJobsAsync(
        Guid userId,
        string query,
        IReadOnlyCollection<PeopleDiscoveryJobDto> results,
        CancellationToken cancellationToken);

    Task<bool> MarkResumeSentAsync(
        Guid userId,
        string resultKey,
        CancellationToken cancellationToken);
}
