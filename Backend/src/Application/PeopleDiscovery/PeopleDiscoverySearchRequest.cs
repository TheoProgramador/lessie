namespace Lessie.Application.PeopleDiscovery;

public sealed class PeopleDiscoverySearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? Location { get; set; }
}
