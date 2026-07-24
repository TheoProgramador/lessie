namespace Lessie.Application.PeopleDiscovery;

public interface IPeopleDiscoveryJobSearchService
{
    Task<IReadOnlyCollection<PeopleDiscoveryJobDto>> SearchAsync(
        PeopleDiscoveryJobSearchRequest request,
        Guid userId,
        CancellationToken cancellationToken);
}
