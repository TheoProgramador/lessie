namespace Lessie.Application.PeopleDiscovery;

public interface IPeopleDiscoveryProgressReporter
{
    Task ReportAsync(PeopleDiscoveryProgressEvent progressEvent, CancellationToken cancellationToken);
}
