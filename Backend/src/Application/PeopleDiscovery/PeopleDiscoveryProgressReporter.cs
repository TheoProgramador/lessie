namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoveryProgressReporter : IPeopleDiscoveryProgressReporter
{
    private Func<PeopleDiscoveryProgressEvent, CancellationToken, Task>? handler;

    public void Subscribe(Func<PeopleDiscoveryProgressEvent, CancellationToken, Task> progressHandler)
    {
        handler = progressHandler;
    }

    public Task ReportAsync(PeopleDiscoveryProgressEvent progressEvent, CancellationToken cancellationToken)
    {
        return handler?.Invoke(progressEvent, cancellationToken) ?? Task.CompletedTask;
    }
}
