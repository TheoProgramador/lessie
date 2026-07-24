using Lessie.Application.Opportunities;
using Lessie.Application.PeopleDiscovery;

namespace Lessie.Infrastructure.Tools.PeopleDiscovery;

internal sealed class JobSpyPeopleDiscoveryJobSearchService(
    IOpportunitySearchService opportunitySearchService,
    IPeopleDiscoveryProgressReporter progressReporter,
    IPeopleDiscoveryResultStore resultStore) : IPeopleDiscoveryJobSearchService
{
    public async Task<IReadOnlyCollection<PeopleDiscoveryJobDto>> SearchAsync(
        PeopleDiscoveryJobSearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = "Aggregated jobs search started across JobSpy, Jobscope and optional jobsearch-buddy.",
                Progress = 10,
                Total = 100
            },
            cancellationToken);

        var searchRequest = new OpportunitySearchRequest
        {
            Query = request.Keywords.Trim(),
            Location = request.Location?.Trim(),
            Limit = Math.Clamp(request.MaxPages, 1, 10) * 10,
            HoursOld = MapDatePostedToHours(request.DatePosted),
            JobType = MapJobType(request.JobType),
            EasyApply = request.EasyApply,
            IsRemote = string.Equals(request.WorkType, "remote", StringComparison.OrdinalIgnoreCase)
        };

        var opportunities = await opportunitySearchService.SearchAsync(searchRequest, userId, cancellationToken);
        var jobs = opportunities
            .Select(MapJob)
            .ToArray();

        var saved = await resultStore.SaveAndFilterJobsAsync(
            userId,
            BuildQueryText(request),
            jobs,
            cancellationToken);

        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = $"Aggregated jobs search finished. Deduplicated results: {saved.Count}.",
                Progress = 100,
                Total = 100,
                PeopleCount = saved.Count
            },
            cancellationToken);

        return saved;
    }

    private static PeopleDiscoveryJobDto MapJob(JobOpportunityDto item)
    {
        var metadataParts = new[]
        {
            item.Date,
            item.RemoteType,
            item.EmploymentType,
            item.Salary,
            string.IsNullOrWhiteSpace(item.ApplyUrl) ? null : $"Apply: {item.ApplyUrl}"
        };

        return new PeopleDiscoveryJobDto
        {
            Title = item.Title,
            Company = item.Company,
            Location = item.Location,
            JobId = item.Id,
            JobUrl = string.IsNullOrWhiteSpace(item.Url) ? item.ApplyUrl : item.Url,
            Insight = BuildInsight(item),
            Metadata = string.Join(" | ", metadataParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            Source = string.Join(" / ", new[] { item.Provider, item.Source }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct())
        };
    }

    private static string BuildInsight(JobOpportunityDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            return Trim(item.Description, 1200);
        }

        if (!string.IsNullOrWhiteSpace(item.Requirements))
        {
            return Trim(item.Requirements, 1200);
        }

        return string.Join(" | ", new[] { item.Provider, item.Source }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildQueryText(PeopleDiscoveryJobSearchRequest request)
    {
        var filters = new[]
        {
            request.Keywords,
            request.Location,
            request.DatePosted,
            request.JobType,
            request.ExperienceLevel,
            request.WorkType,
            request.EasyApply ? "easy_apply" : null,
            request.SortBy,
            $"pages:{Math.Clamp(request.MaxPages, 1, 10)}",
            "providers:all"
        };

        return string.Join(" | ", filters.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int? MapDatePostedToHours(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "past_hour" => 1,
            "past_24_hours" => 24,
            "past_week" => 168,
            "past_month" => 720,
            _ => null
        };
    }

    private static string? MapJobType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "full_time" => "fulltime",
            "part_time" => "parttime",
            "contract" => "contract",
            "temporary" => "temporary",
            "internship" => "internship",
            _ => null
        };
    }

    private static string Trim(string value, int maxLength)
    {
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
