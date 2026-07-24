using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lessie.Application.Opportunities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Lessie.Infrastructure.Tools.Opportunities;

internal sealed partial class JobscopeOpportunityProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<JobscopeOpportunityProvider> logger) : IOpportunityProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderName => "Jobscope";

    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchAsync(
        OpportunitySearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = JobscopeSettings.FromConfiguration(configuration);
        if (!settings.Enabled)
        {
            return [];
        }

        var cacheKey = settings.BuildCacheKey(request);
        if (cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Results;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(settings.Timeout);

        var companies = LoadCompanies(settings)
            .Where(company => settings.Ats.Length == 0 || settings.Ats.Contains(company.Ats, StringComparer.OrdinalIgnoreCase))
            .Take(settings.MaxCompanies)
            .ToArray();

        var semaphore = new SemaphoreSlim(settings.MaxConcurrency);
        var tasks = companies.Select(company => SearchCompanyAsync(company, request, semaphore, timeoutCts.Token)).ToArray();
        var results = (await Task.WhenAll(tasks))
            .SelectMany(item => item)
            .Where(item => Matches(item, request))
            .Take(Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 200))
            .ToArray();

        cache[cacheKey] = new CacheEntry(results, DateTimeOffset.UtcNow.Add(settings.CacheDuration));
        return results;
    }

    public Task<JobOpportunityDto?> GetDetailsAsync(
        OpportunityDetailsRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<JobOpportunityDto?>(null);
    }

    private async Task<IReadOnlyCollection<JobOpportunityDto>> SearchCompanyAsync(
        JobscopeCompany company,
        OpportunitySearchRequest request,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return company.Ats.ToLowerInvariant() switch
            {
                "greenhouse" => await SearchGreenhouseAsync(company, cancellationToken),
                "lever" => await SearchLeverAsync(company, cancellationToken),
                "ashby" => await SearchAshbyAsync(company, cancellationToken),
                "workday" => await SearchWorkdayAsync(company, cancellationToken),
                _ => []
            };
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Jobscope provider failed for {Ats}/{Company}.", company.Ats, company.CompanySlug);
            return [];
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<IReadOnlyCollection<JobOpportunityDto>> SearchGreenhouseAsync(
        JobscopeCompany company,
        CancellationToken cancellationToken)
    {
        var url = $"https://boards-api.greenhouse.io/v1/boards/{Uri.EscapeDataString(company.AtsSlug)}/jobs?content=true";
        var response = await httpClient.GetFromJsonAsync<JsonElement>(url, JsonOptions, cancellationToken);
        if (!response.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return jobs.EnumerateArray().Select(job =>
        {
            var id = ReadString(job, "id");
            var location = job.TryGetProperty("location", out var locationNode)
                ? ReadString(locationNode, "name")
                : string.Empty;
            var description = StripHtml(ReadString(job, "content"));
            var published = ReadString(job, "first_published");
            if (string.IsNullOrWhiteSpace(published))
            {
                published = ReadString(job, "updated_at");
            }

            return new JobOpportunityDto
            {
                ResultKey = $"jobscope:greenhouse:{company.CompanySlug}:{id}",
                Id = id,
                Title = ReadString(job, "title"),
                Company = company.Name,
                Location = location,
                RemoteType = InferRemote(location),
                PublishedAt = ParseDate(published),
                Date = published,
                Description = Trim(description, 2000),
                Url = ReadString(job, "absolute_url"),
                ApplyUrl = ReadString(job, "absolute_url"),
                Source = "Greenhouse",
                Provider = "Jobscope"
            };
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<JobOpportunityDto>> SearchLeverAsync(
        JobscopeCompany company,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.lever.co/v0/postings/{Uri.EscapeDataString(company.AtsSlug)}?mode=json";
        var jobs = await httpClient.GetFromJsonAsync<JsonElement>(url, JsonOptions, cancellationToken);
        if (jobs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return jobs.EnumerateArray().Select(job =>
        {
            var id = ReadString(job, "id");
            var categories = job.TryGetProperty("categories", out var categoriesNode) ? categoriesNode : default;
            var location = categories.ValueKind == JsonValueKind.Object ? ReadString(categories, "location") : string.Empty;
            var createdAt = ReadLong(job, "createdAt");
            DateTimeOffset? publishedAt = createdAt > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(createdAt) : null;

            return new JobOpportunityDto
            {
                ResultKey = $"jobscope:lever:{company.CompanySlug}:{id}",
                Id = id,
                Title = ReadString(job, "text"),
                Company = company.Name,
                Location = location,
                RemoteType = InferRemote($"{ReadString(job, "workplaceType")} {location}"),
                EmploymentType = categories.ValueKind == JsonValueKind.Object ? ReadString(categories, "commitment") : string.Empty,
                PublishedAt = publishedAt,
                Date = publishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                Description = Trim(ReadString(job, "descriptionPlain"), 2000),
                Url = FirstNonEmpty(ReadString(job, "hostedUrl"), ReadString(job, "applyUrl")),
                ApplyUrl = FirstNonEmpty(ReadString(job, "applyUrl"), ReadString(job, "hostedUrl")),
                Source = "Lever",
                Provider = "Jobscope"
            };
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<JobOpportunityDto>> SearchAshbyAsync(
        JobscopeCompany company,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.ashbyhq.com/posting-api/job-board/{Uri.EscapeDataString(company.AtsSlug)}?includeCompensation=true";
        var response = await httpClient.GetFromJsonAsync<JsonElement>(url, JsonOptions, cancellationToken);
        if (!response.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return jobs.EnumerateArray().Select(job =>
        {
            var id = ReadString(job, "id");
            var location = ReadString(job, "locationName");
            var published = ReadString(job, "publishedAt");

            return new JobOpportunityDto
            {
                ResultKey = $"jobscope:ashby:{company.CompanySlug}:{id}",
                Id = id,
                Title = ReadString(job, "title"),
                Company = company.Name,
                Location = location,
                RemoteType = ReadBool(job, "isRemote") ? "Remote" : InferRemote(location),
                EmploymentType = ReadString(job, "employmentType"),
                Salary = ReadString(job, "compensationTierSummary"),
                PublishedAt = ParseDate(published),
                Date = published,
                Description = Trim(ReadString(job, "descriptionPlain"), 2000),
                Url = FirstNonEmpty(ReadString(job, "jobUrl"), ReadString(job, "applyUrl")),
                ApplyUrl = FirstNonEmpty(ReadString(job, "applyUrl"), ReadString(job, "jobUrl")),
                Source = "Ashby",
                Provider = "Jobscope"
            };
        }).ToArray();
    }

    private async Task<IReadOnlyCollection<JobOpportunityDto>> SearchWorkdayAsync(
        JobscopeCompany company,
        CancellationToken cancellationToken)
    {
        var parts = company.AtsSlug.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return [];
        }

        var tenant = parts[0];
        var shard = parts[1];
        var site = parts[2];
        var url = $"https://{tenant}.{shard}.myworkdayjobs.com/wday/cxs/{tenant}/{site}/jobs";
        using var response = await httpClient.PostAsJsonAsync(
            url,
            new { appliedFacets = new { }, limit = 20, offset = 0, searchText = "" },
            JsonOptions,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        if (!body.TryGetProperty("jobPostings", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return jobs.EnumerateArray().Select(job =>
        {
            var externalPath = ReadString(job, "externalPath");
            var location = ReadString(job, "locationsText");
            var applyUrl = $"https://{tenant}.{shard}.myworkdayjobs.com/{site}{externalPath}";

            return new JobOpportunityDto
            {
                ResultKey = $"jobscope:workday:{company.CompanySlug}:{externalPath}",
                Id = externalPath,
                Title = ReadString(job, "title"),
                Company = company.Name,
                Location = location,
                RemoteType = InferRemote(location),
                Url = applyUrl,
                ApplyUrl = applyUrl,
                Source = "Workday",
                Provider = "Jobscope"
            };
        }).ToArray();
    }

    private static IReadOnlyCollection<JobscopeCompany> LoadCompanies(JobscopeSettings settings)
    {
        var path = settings.ResolveDirectoryPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("companies", out var companies)
            ? companies.EnumerateArray()
                .Select(company => new JobscopeCompany(
                    ReadString(company, "name"),
                    ReadString(company, "company_slug"),
                    ReadString(company, "ats"),
                    ReadString(company, "ats_slug")))
                .Where(company => !string.IsNullOrWhiteSpace(company.Name)
                    && !string.IsNullOrWhiteSpace(company.Ats)
                    && !string.IsNullOrWhiteSpace(company.AtsSlug))
                .ToArray()
            : [];
    }

    private static bool Matches(JobOpportunityDto job, OpportunitySearchRequest request)
    {
        var queryTerms = Tokenize(request.Query).ToArray();
        var searchable = $"{job.Title} {job.Company} {job.Description}".ToLowerInvariant();
        if (queryTerms.Length > 0 && !queryTerms.Any(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Location)
            && !job.Location.Contains(request.Location.Trim(), StringComparison.OrdinalIgnoreCase)
            && !job.RemoteType.Contains(request.Location.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.IsRemote && !job.RemoteType.Contains("remote", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.HoursOld is > 0 && job.PublishedAt is { } publishedAt)
        {
            return DateTimeOffset.UtcNow - publishedAt.ToUniversalTime() <= TimeSpan.FromHours(request.HoursOld.Value);
        }

        return true;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return WordRegex()
            .Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? value.ToString()
            : string.Empty;
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var result)
            ? result
            : 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string StripHtml(string value)
    {
        return Regex.Replace(WebEntityRegex().Replace(value, " "), "<[^>]+>", " ").Replace("&amp;", "&").Trim();
    }

    private static string InferRemote(string value)
    {
        if (value.Contains("remote", StringComparison.OrdinalIgnoreCase)
            || value.Contains("remoto", StringComparison.OrdinalIgnoreCase))
        {
            return "Remote";
        }

        return value.Contains("hybrid", StringComparison.OrdinalIgnoreCase) ? "Hybrid" : string.Empty;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string Trim(string value, int maxLength)
    {
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record JobscopeCompany(string Name, string CompanySlug, string Ats, string AtsSlug);
    private sealed record CacheEntry(IReadOnlyCollection<JobOpportunityDto> Results, DateTimeOffset ExpiresAt);

    private sealed class JobscopeSettings
    {
        public bool Enabled { get; private init; }
        public string DirectoryPath { get; private init; } = "external/jobscope-mcp/src/directory/data.json";
        public string[] Ats { get; private init; } = ["greenhouse", "lever", "ashby", "workday"];
        public int TimeoutSeconds { get; private init; } = 45;
        public int CacheMinutes { get; private init; } = 10;
        public int MaxCompanies { get; private init; } = 80;
        public int MaxConcurrency { get; private init; } = 8;

        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));
        public TimeSpan CacheDuration => TimeSpan.FromMinutes(Math.Max(1, CacheMinutes));

        public static JobscopeSettings FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("OpportunityProviders:Jobscope");
            return new JobscopeSettings
            {
                Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                DirectoryPath = section["DirectoryPath"] ?? "external/jobscope-mcp/src/directory/data.json",
                Ats = section.GetSection("Ats").Get<string[]>() ?? ["greenhouse", "lever", "ashby", "workday"],
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 45,
                CacheMinutes = int.TryParse(section["CacheMinutes"], out var cacheMinutes) ? cacheMinutes : 10,
                MaxCompanies = int.TryParse(section["MaxCompanies"], out var maxCompanies) ? Math.Clamp(maxCompanies, 1, 500) : 80,
                MaxConcurrency = int.TryParse(section["MaxConcurrency"], out var maxConcurrency) ? Math.Clamp(maxConcurrency, 1, 24) : 8
            };
        }

        public string BuildCacheKey(OpportunitySearchRequest request)
        {
            return string.Join(
                "|",
                request.Query.Trim().ToLowerInvariant(),
                request.Location?.Trim().ToLowerInvariant(),
                request.Limit,
                request.HoursOld,
                request.IsRemote,
                string.Join(",", Ats));
        }

        public string? ResolveDirectoryPath()
        {
            if (Path.IsPathRooted(DirectoryPath))
            {
                return DirectoryPath;
            }

            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var current = new DirectoryInfo(start);
                while (current is not null)
                {
                    var candidate = Path.GetFullPath(Path.Combine(current.FullName, DirectoryPath));
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    current = current.Parent;
                }
            }

            return null;
        }
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"&[a-zA-Z0-9#]+;")]
    private static partial Regex WebEntityRegex();
}
