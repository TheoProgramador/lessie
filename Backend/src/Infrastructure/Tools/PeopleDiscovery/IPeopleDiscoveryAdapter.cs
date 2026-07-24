using Lessie.Application.PeopleDiscovery;

namespace Lessie.Infrastructure.Tools.PeopleDiscovery;

internal interface IPeopleDiscoveryAdapter
{
    Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> SearchAsync(string query, string userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> SearchPostsAsync(
        string query,
        string userId,
        string? location,
        CancellationToken cancellationToken);
}
