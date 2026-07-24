using System.ComponentModel;
using ApInfoMcpServer.Models;
using ApInfoMcpServer.Scrapers;
using ModelContextProtocol.Server;

namespace ApInfoMcpServer.Tools;

[McpServerToolType]
public sealed class ApInfoTools(ApInfoScraper scraper)
{
    [McpServerTool(Name = "apinfo.search_jobs", ReadOnly = true, OpenWorld = true)]
    [Description("Searches real APInfo job opportunities. This always navigates APInfo because new jobs may appear for the same keywords.")]
    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchJobsAsync(
        [Description("Keywords, stack, role, or APInfo job code. Example: .NET remoto")] string query,
        [Description("Optional location filter. Example: Home Office, Sao Paulo, SP")] string? location = null,
        [Description("Maximum results to return. Default 20, maximum 80.")] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return await scraper.SearchJobsAsync(query.Trim(), location, limit, cancellationToken);
    }

    [McpServerTool(Name = "apinfo.get_job_details", ReadOnly = true, OpenWorld = true)]
    [Description("Reads APInfo job details. When revealContact is true, opens the APInfo contact page visibly so the user can complete captcha manually.")]
    public async Task<JobOpportunityDto?> GetJobDetailsAsync(
        [Description("APInfo job id, also shown as Codigo on APInfo.")] string jobId,
        [Description("Set true only when the browser can be shown and the user will manually complete the captcha to reveal email/contact data.")] bool revealContact = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        return await scraper.GetJobDetailsAsync(jobId.Trim(), revealContact, cancellationToken);
    }

    [McpServerTool(Name = "apinfo.search_jobs_by_stack", ReadOnly = true, OpenWorld = true)]
    [Description("Searches APInfo opportunities by technology stack.")]
    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchJobsByStackAsync(
        [Description("Technology or stack name. Example: .NET, Angular, SAP")] string technology,
        CancellationToken cancellationToken = default)
    {
        return await SearchJobsAsync(technology, null, 20, cancellationToken);
    }
}
