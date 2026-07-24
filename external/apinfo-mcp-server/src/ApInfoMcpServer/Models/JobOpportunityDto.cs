namespace ApInfoMcpServer.Models;

public sealed record JobOpportunityDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Requirements { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string ApplyUrl { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string ContactSubject { get; init; } = string.Empty;
    public string Source { get; init; } = "APInfo";
}
