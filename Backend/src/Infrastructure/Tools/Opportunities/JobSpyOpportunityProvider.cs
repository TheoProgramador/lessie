using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lessie.Application.Opportunities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Lessie.Infrastructure.Tools.Opportunities;

internal sealed class JobSpyOpportunityProvider(
    IConfiguration configuration,
    ILogger<JobSpyOpportunityProvider> logger,
    ILoggerFactory loggerFactory) : IOpportunityProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderName => "JobSpy";

    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchAsync(
        OpportunitySearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = JobSpySettings.FromConfiguration(configuration);
        settings.Validate();

        var cacheKey = settings.BuildCacheKey(request);
        if (cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Results;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(settings.Timeout);

        await using var client = await CreateClientAsync(settings, timeoutCts.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeoutCts.Token);
        var searchTool = SelectSearchTool(tools);

        logger.LogInformation("Selected JobSpy MCP tool {ToolName}.", searchTool.Name);

        var response = await client.CallToolAsync(
            searchTool.Name,
            BuildSearchArguments(request, settings),
            cancellationToken: timeoutCts.Token);

        var results = JobSpyResultParser.Parse(response)
            .Select(result => Normalize(result, request))
            .Where(result => !string.IsNullOrWhiteSpace(result.Title) || !string.IsNullOrWhiteSpace(result.Url))
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

    private async Task<McpClient> CreateClientAsync(JobSpySettings settings, CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "jobspy-mcp",
                Command = settings.Command,
                Arguments = settings.Arguments.ToArray(),
                WorkingDirectory = settings.ResolveWorkingDirectory(),
                EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
                InheritEnvironmentVariables = true,
                ShutdownTimeout = TimeSpan.FromSeconds(5),
                StandardErrorLines = line => logger.LogInformation("JobSpy MCP stderr: {Line}", line)
            },
            loggerFactory);

        return await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);
    }

    private static McpClientTool SelectSearchTool(IEnumerable<McpClientTool> tools)
    {
        return tools
            .Select(tool => new { Tool = tool, Score = ScoreTool(tool) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()?.Tool
            ?? throw new InvalidOperationException("JobSpy MCP did not expose a compatible job search tool.");
    }

    private static int ScoreTool(McpClientTool tool)
    {
        var text = $"{tool.Name} {tool.Description} {tool.JsonSchema}".ToLowerInvariant();
        var score = 0;

        if (text.Contains("job") || text.Contains("vaga"))
        {
            score += 3;
        }

        if (text.Contains("scrape") || text.Contains("search"))
        {
            score += 3;
        }

        if (text.Contains("search_term") || text.Contains("searchterm"))
        {
            score += 4;
        }

        if (text.Contains("location"))
        {
            score += 2;
        }

        if (text.Contains("site_name") || text.Contains("site_names"))
        {
            score += 2;
        }

        if (text.Contains("fetch") && text.Contains("single"))
        {
            score -= 5;
        }

        return score;
    }

    private static Dictionary<string, object?> BuildSearchArguments(OpportunitySearchRequest request, JobSpySettings settings)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 80);
        var arguments = new Dictionary<string, object?>
        {
            ["site_name"] = request.SiteNames is { Count: > 0 } ? request.SiteNames : settings.SiteNames,
            ["search_term"] = request.Query.Trim(),
            ["location"] = string.IsNullOrWhiteSpace(request.Location) ? settings.DefaultLocation : request.Location.Trim(),
            ["results_wanted"] = limit,
            ["country_indeed"] = settings.CountryIndeed,
            ["description_format"] = "plain"
        };

        var hoursOld = request.HoursOld.GetValueOrDefault(settings.HoursOld);
        if (hoursOld > 0)
        {
            arguments["hours_old"] = hoursOld;
        }

        if (!string.IsNullOrWhiteSpace(request.JobType))
        {
            arguments["job_type"] = request.JobType.Trim();
        }

        if (request.EasyApply)
        {
            arguments["easy_apply"] = true;
        }

        if (request.IsRemote)
        {
            arguments["is_remote"] = true;
        }

        if (settings.FetchLinkedInDescription)
        {
            arguments["linkedin_fetch_description"] = true;
        }

        if (settings.FetchIndeedDescription)
        {
            arguments["indeed_fetch_description"] = true;
        }

        return arguments;
    }

    private static JobOpportunityDto Normalize(JobOpportunityDto result, OpportunitySearchRequest request)
    {
        var publishedAt = result.PublishedAt ?? ParseDate(result.Date);
        var source = string.IsNullOrWhiteSpace(result.Source) ? GuessSource(result.Url) : NormalizeSource(result.Source);
        var id = string.IsNullOrWhiteSpace(result.Id) ? BuildId(result.Url, result.Title, result.Company) : result.Id;
        var date = !string.IsNullOrWhiteSpace(result.Date)
            ? result.Date
            : publishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

        return new JobOpportunityDto
        {
            ResultKey = string.IsNullOrWhiteSpace(result.ResultKey) ? $"{source}:{id}" : result.ResultKey,
            Id = id,
            Title = result.Title,
            Company = result.Company,
            Location = result.Location,
            Country = result.Country,
            RemoteType = string.IsNullOrWhiteSpace(result.RemoteType) && IsRemote(result.Location) ? "Remote" : result.RemoteType,
            EmploymentType = result.EmploymentType,
            Salary = result.Salary,
            PublishedAt = publishedAt,
            Date = date,
            Description = result.Description,
            Requirements = result.Requirements,
            Url = result.Url,
            ApplyUrl = result.ApplyUrl,
            ContactEmail = result.ContactEmail,
            ContactSubject = result.ContactSubject,
            Source = source,
            Provider = "JobSpy"
        };
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string BuildId(string url, string title, string company)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            var indeed = Regex.Match(url, @"[?&]jk=([^&]+)", RegexOptions.IgnoreCase);
            if (indeed.Success)
            {
                return indeed.Groups[1].Value;
            }

            var linkedIn = Regex.Match(url, @"/jobs/view/(\d+)", RegexOptions.IgnoreCase);
            if (linkedIn.Success)
            {
                return linkedIn.Groups[1].Value;
            }

            return url.Trim();
        }

        return $"{company}:{title}";
    }

    private static bool IsRemote(string location)
    {
        return location.Contains("remote", StringComparison.OrdinalIgnoreCase)
            || location.Contains("remoto", StringComparison.OrdinalIgnoreCase)
            || location.Contains("home office", StringComparison.OrdinalIgnoreCase);
    }

    private static string GuessSource(string url)
    {
        if (url.Contains("linkedin", StringComparison.OrdinalIgnoreCase))
        {
            return "LinkedIn";
        }

        if (url.Contains("indeed", StringComparison.OrdinalIgnoreCase))
        {
            return "Indeed";
        }

        if (url.Contains("glassdoor", StringComparison.OrdinalIgnoreCase))
        {
            return "Glassdoor";
        }

        if (url.Contains("ziprecruiter", StringComparison.OrdinalIgnoreCase))
        {
            return "ZipRecruiter";
        }

        if (url.Contains("bayt", StringComparison.OrdinalIgnoreCase))
        {
            return "Bayt";
        }

        if (url.Contains("naukri", StringComparison.OrdinalIgnoreCase))
        {
            return "Naukri";
        }

        if (url.Contains("bdjobs", StringComparison.OrdinalIgnoreCase))
        {
            return "BDJobs";
        }

        return "JobSpy";
    }

    private static string NormalizeSource(string source)
    {
        return source.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant() switch
        {
            "linkedin" => "LinkedIn",
            "indeed" => "Indeed",
            "glassdoor" => "Glassdoor",
            "ziprecruiter" => "ZipRecruiter",
            "google" => "Google Jobs",
            "bayt" => "Bayt",
            "naukri" => "Naukri",
            "bdjobs" => "BDJobs",
            _ => source
        };
    }

    private sealed record CacheEntry(IReadOnlyCollection<JobOpportunityDto> Results, DateTimeOffset ExpiresAt);

    private sealed class JobSpySettings
    {
        public bool Enabled { get; private init; }
        public string Command { get; private init; } = "npx";
        public IReadOnlyCollection<string> Arguments { get; private init; } = ["vite-node", "src/mcp/index.ts"];
        public string WorkingDirectory { get; private init; } = "external/jobspy-js";
        public int TimeoutSeconds { get; private init; } = 180;
        public int CacheMinutes { get; private init; } = 5;
        public string[] SiteNames { get; private init; } = ["indeed", "linkedin", "glassdoor", "zip_recruiter"];
        public string DefaultLocation { get; private init; } = "Brazil";
        public string CountryIndeed { get; private init; } = "brazil";
        public int HoursOld { get; private init; } = 720;
        public bool FetchLinkedInDescription { get; private init; }
        public bool FetchIndeedDescription { get; private init; }
        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));
        public TimeSpan CacheDuration => TimeSpan.FromMinutes(Math.Max(1, CacheMinutes));

        public static JobSpySettings FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("OpportunityProviders:JobSpy");
            return new JobSpySettings
            {
                Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                Command = section["Command"] ?? "npx",
                Arguments = section.GetSection("Arguments").Get<string[]>() ?? ["vite-node", "src/mcp/index.ts"],
                WorkingDirectory = section["WorkingDirectory"] ?? "external/jobspy-js",
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 180,
                CacheMinutes = int.TryParse(section["CacheMinutes"], out var cacheMinutes) ? cacheMinutes : 5,
                SiteNames = section.GetSection("SiteNames").Get<string[]>()
                    ?? ["indeed", "linkedin", "glassdoor", "zip_recruiter"],
                DefaultLocation = section["DefaultLocation"] ?? "Brazil",
                CountryIndeed = section["CountryIndeed"] ?? "brazil",
                HoursOld = int.TryParse(section["HoursOld"], out var hoursOld) ? hoursOld : 720,
                FetchLinkedInDescription = bool.TryParse(section["FetchLinkedInDescription"], out var fetchLinkedInDescription)
                    && fetchLinkedInDescription,
                FetchIndeedDescription = bool.TryParse(section["FetchIndeedDescription"], out var fetchIndeedDescription)
                    && fetchIndeedDescription
            };
        }

        public void Validate()
        {
            if (!Enabled)
            {
                throw new InvalidOperationException("JobSpy provider is not enabled.");
            }

            if (string.IsNullOrWhiteSpace(Command))
            {
                throw new InvalidOperationException("JobSpy MCP command is not configured.");
            }
        }

        public string ResolveWorkingDirectory()
        {
            if (Path.IsPathRooted(WorkingDirectory))
            {
                return WorkingDirectory;
            }

            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var current = new DirectoryInfo(start);
                while (current is not null)
                {
                    var candidate = Path.GetFullPath(Path.Combine(current.FullName, WorkingDirectory));
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    current = current.Parent;
                }
            }

            return Path.GetFullPath(WorkingDirectory);
        }

        public string BuildCacheKey(OpportunitySearchRequest request)
        {
            var siteNames = request.SiteNames is { Count: > 0 }
                ? request.SiteNames
                : SiteNames;

            return string.Join(
                "|",
                request.Query.Trim().ToLowerInvariant(),
                (request.Location ?? DefaultLocation).Trim().ToLowerInvariant(),
                request.Limit,
                string.Join(",", siteNames).ToLowerInvariant(),
                CountryIndeed.ToLowerInvariant(),
                request.HoursOld.GetValueOrDefault(HoursOld),
                request.JobType?.Trim().ToLowerInvariant(),
                request.EasyApply,
                request.IsRemote);
        }
    }

    private static class JobSpyResultParser
    {
        public static IReadOnlyCollection<JobOpportunityDto> Parse(CallToolResult response)
        {
            if (response.IsError == true)
            {
                throw new InvalidOperationException(ExtractText(response) ?? "JobSpy MCP tool returned an error.");
            }

            if (response.StructuredContent is { } structuredContent)
            {
                var structuredNode = JsonSerializer.SerializeToNode(structuredContent, JsonOptions);
                var parsed = ParseJsonNode(structuredNode);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }

            var text = ExtractText(response);
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            if (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('['))
            {
                var parsed = ParseJsonNode(JsonNode.Parse(text));
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }

            return ParseSummary(text);
        }

        private static IReadOnlyCollection<JobOpportunityDto> ParseJsonNode(JsonNode? node)
        {
            if (node is null)
            {
                return [];
            }

            var jobs = node switch
            {
                JsonArray array => array,
                JsonObject jsonObject when jsonObject["jobs"] is JsonArray jobsArray => jobsArray,
                JsonObject jsonObject when jsonObject["data"] is JsonArray dataArray => dataArray,
                _ => []
            };

            return jobs
                .OfType<JsonObject>()
                .Select(ParseJobObject)
                .ToArray();
        }

        private static JobOpportunityDto ParseJobObject(JsonObject job)
        {
            var title = GetString(job, "title", "jobTitle");
            var company = GetString(job, "company", "companyName");
            var location = GetString(job, "location");
            var url = GetString(job, "job_url", "jobUrl", "url");
            var directUrl = GetString(job, "job_url_direct", "jobUrlDirect", "applyUrl");
            var source = GetString(job, "site", "source");
            var publishedAt = GetString(job, "date_posted", "datePosted", "publishedAt");
            var minAmount = GetString(job, "min_amount", "minAmount");
            var maxAmount = GetString(job, "max_amount", "maxAmount");
            var currency = GetString(job, "currency", "salaryCurrency");
            var interval = GetString(job, "interval", "salaryPeriod");
            var salary = GetString(job, "salary");
            if (string.IsNullOrWhiteSpace(salary) && (!string.IsNullOrWhiteSpace(minAmount) || !string.IsNullOrWhiteSpace(maxAmount)))
            {
                salary = $"{currency} {minAmount}-{maxAmount} {interval}".Trim();
            }

            return new JobOpportunityDto
            {
                Id = GetString(job, "id"),
                Title = title,
                Company = company,
                Location = location,
                Country = GetString(job, "country"),
                RemoteType = GetBool(job, "is_remote", "isRemote") ? "Remote" : GetString(job, "work_from_home_type", "workFromHomeType"),
                EmploymentType = GetString(job, "job_type", "jobType"),
                Salary = salary,
                Date = publishedAt,
                PublishedAt = ParseDate(publishedAt),
                Description = GetString(job, "description", "jobSummary"),
                Url = url,
                ApplyUrl = directUrl,
                Source = source
            };
        }

        private static IReadOnlyCollection<JobOpportunityDto> ParseSummary(string text)
        {
            var blocks = Regex.Split(text, @"\n\s*\n")
                .Where(block => Regex.IsMatch(block, @"^\s*\d+\.\s+\*\*", RegexOptions.Multiline))
                .ToArray();

            return blocks.Select(ParseSummaryBlock).ToArray();
        }

        private static JobOpportunityDto ParseSummaryBlock(string block)
        {
            var title = Match(block, @"^\s*\d+\.\s+\*\*(?<value>.+?)\*\*");
            var company = Match(block, @"Company:\s*(?<value>.+)");
            var location = Match(block, @"Location:\s*(?<value>.+)");
            var url = Match(block, @"URL:\s*(?<value>\S+)");
            var date = Match(block, @"Posted:\s*(?<value>.+)");
            var jobType = Match(block, @"Type:\s*(?<value>.+)");
            var salary = Match(block, @"Salary:\s*(?<value>.+)");

            return new JobOpportunityDto
            {
                Title = title,
                Company = company,
                Location = location.Replace(" (Remote)", string.Empty, StringComparison.OrdinalIgnoreCase),
                RemoteType = location.Contains("Remote", StringComparison.OrdinalIgnoreCase) ? "Remote" : string.Empty,
                EmploymentType = jobType,
                Salary = salary,
                PublishedAt = ParseDate(date),
                Date = date,
                Url = url,
                ApplyUrl = url,
                Source = GuessSource(url)
            };
        }

        private static string ExtractText(CallToolResult response)
        {
            foreach (var content in response.Content)
            {
                if (content is TextContentBlock textContent && !string.IsNullOrWhiteSpace(textContent.Text))
                {
                    return textContent.Text;
                }
            }

            return string.Empty;
        }

        private static string GetString(JsonObject jsonObject, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (jsonObject.TryGetPropertyValue(propertyName, out var node) && node is not null)
                {
                    return node.GetValueKind() == JsonValueKind.String
                        ? node.GetValue<string>() ?? string.Empty
                        : node.ToJsonString(JsonOptions).Trim('"');
                }
            }

            return string.Empty;
        }

        private static bool GetBool(JsonObject jsonObject, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (jsonObject.TryGetPropertyValue(propertyName, out var node)
                    && node is JsonValue value
                    && value.TryGetValue<bool>(out var result))
                {
                    return result;
                }
            }

            return false;
        }

        private static string Match(string block, string pattern)
        {
            var match = Regex.Match(block, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }
    }
}
