namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoveryProgressEvent
{
    public string Level { get; init; } = "info";
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
    public double? Progress { get; init; }
    public double? Total { get; init; }
    public double? ElapsedSeconds { get; init; }
    public int? PeopleCount { get; init; }
    public int? ProcessId { get; init; }
    public bool? ProcessRunning { get; init; }
}
