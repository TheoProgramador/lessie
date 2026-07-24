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

internal sealed class JdIntelOpportunityProvider(
    IConfiguration configuration,
    ILogger<JdIntelOpportunityProvider> logger,
    ILoggerFactory loggerFactory) : IOpportunityProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderName => "jd-intel";

    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchAsync(
        OpportunitySearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = JdIntelSettings.FromConfiguration(configuration);
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

        await using var client = await CreateClientAsync(settings, timeoutCts.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeoutCts.Token);
        var fetchTool = tools.FirstOrDefault(tool => string.Equals(tool.Name, settings.FetchJobsToolName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("jd-intel MCP did not expose fetch_jobs.");

        var companies = settings.Companies.Length == 0 ? [string.Empty] : settings.Companies;
        var perCompanyLimit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 80);
        var results = new List<JobOpportunityDto>();
        foreach (var company in companies.Take(settings.MaxCompanies))
        {
            var response = await client.CallToolAsync(
                fetchTool.Name,
                BuildFetchArguments(request, settings, company, perCompanyLimit),
                cancellationToken: timeoutCts.Token);

            results.AddRange(Parse(response).Select(Normalize));
        }

        var normalizedResults = results
            .Where(result => !string.IsNullOrWhiteSpace(result.Title) || !string.IsNullOrWhiteSpace(result.Url))
            .Take(Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 200))
            .ToArray();

        cache[cacheKey] = new CacheEntry(normalizedResults, DateTimeOffset.UtcNow.Add(settings.CacheDuration));
        return normalizedResults;
    }

    public Task<JobOpportunityDto?> GetDetailsAsync(
        OpportunityDetailsRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<JobOpportunityDto?>(null);
    }

    private async Task<McpClient> CreateClientAsync(JdIntelSettings settings, CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "jd-intel",
                Command = settings.Command,
                Arguments = settings.Arguments.ToArray(),
                WorkingDirectory = settings.ResolveWorkingDirectory(),
                EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
                InheritEnvironmentVariables = true,
                ShutdownTimeout = TimeSpan.FromSeconds(5),
                StandardErrorLines = line => logger.LogInformation("jd-intel MCP stderr: {Line}", line)
            },
            loggerFactory);

        return await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);
    }

    private static Dictionary<string, object?> BuildFetchArguments(
        OpportunitySearchRequest request,
        JdIntelSettings settings,
        string company,
        int limit)
    {
        var postedWithinDays = Math.Max(1, (int)Math.Ceiling(request.HoursOld.GetValueOrDefault(settings.PostedWithinHours) / 24d));
        var arguments = new Dictionary<string, object?>
        {
            ["company"] = company,
            ["titleFilter"] = request.Query.Trim(),
            ["filter"] = request.Query.Trim(),
            ["postedWithinDays"] = postedWithinDays,
            ["limit"] = limit
        };

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            arguments["locationInclude"] = request.Location.Trim();
        }

        if (request.IsRemote)
        {
            arguments["locationInclude"] = "remote";
        }

        return arguments;
    }

    private static IReadOnlyCollection<JobOpportunityDto> Parse(CallToolResult response)
    {
        if (response.IsError == true)
        {
            throw new InvalidOperationException(ExtractText(response) ?? "jd-intel MCP tool returned an error.");
        }

        if (response.StructuredContent is { } structuredContent)
        {
            var node = JsonSerializer.SerializeToNode(structuredContent, JsonOptions);
            var parsed = ParseJsonNode(node);
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
            return ParseJsonNode(JsonNode.Parse(text));
        }

        return [];
    }

    private static IReadOnlyCollection<JobOpportunityDto> ParseJsonNode(JsonNode? node)
    {
        var jobs = node switch
        {
            JsonArray array => array,
            JsonObject jsonObject when jsonObject["jobs"] is JsonArray jobsArray => jobsArray,
            JsonObject jsonObject when jsonObject["data"] is JsonArray dataArray => dataArray,
            JsonObject jsonObject when jsonObject["results"] is JsonArray resultsArray => resultsArray,
            _ => []
        };

        return jobs.OfType<JsonObject>().Select(ParseJobObject).ToArray();
    }

    private static JobOpportunityDto ParseJobObject(JsonObject job)
    {
        var id = GetString(job, "id", "jobId");
        var title = GetString(job, "title");
        var company = GetString(job, "company", "companyName");
        var location = GetString(job, "location");
        var locationType = GetString(job, "locationType", "location_type");
        var postedAt = GetString(job, "postedAt", "posted_at", "publishedAt");
        var salary = FormatSalary(job["salary"]);
        var url = GetString(job, "url", "applyUrl", "apply_url");

        return new JobOpportunityDto
        {
            ResultKey = $"jd-intel:{GuessSource(url)}:{company}:{id}:{url}",
            Id = id,
            Title = title,
            Company = company,
            Location = location,
            RemoteType = NormalizeLocationType(locationType, location),
            EmploymentType = GetString(job, "employmentType", "employment_type", "commitment"),
            Salary = salary,
            PublishedAt = ParseDate(postedAt),
            Date = postedAt,
            Description = GetString(job, "description", "markdown", "content"),
            Url = url,
            ApplyUrl = url,
            Source = GuessSource(url),
            Provider = "jd-intel"
        };
    }

    private static JobOpportunityDto Normalize(JobOpportunityDto result)
    {
        var publishedAt = result.PublishedAt ?? ParseDate(result.Date);
        return new JobOpportunityDto
        {
            ResultKey = result.ResultKey,
            Id = string.IsNullOrWhiteSpace(result.Id) ? result.Url : result.Id,
            Title = result.Title,
            Company = result.Company,
            Location = result.Location,
            Country = result.Country,
            RemoteType = result.RemoteType,
            EmploymentType = result.EmploymentType,
            Salary = result.Salary,
            PublishedAt = publishedAt,
            Date = !string.IsNullOrWhiteSpace(result.Date)
                ? result.Date
                : publishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            Description = Trim(result.Description, 3000),
            Requirements = result.Requirements,
            Url = result.Url,
            ApplyUrl = result.ApplyUrl,
            ContactEmail = result.ContactEmail,
            ContactSubject = result.ContactSubject,
            Source = result.Source,
            Provider = "jd-intel"
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

    private static string FormatSalary(JsonNode? salaryNode)
    {
        if (salaryNode is not JsonObject salary)
        {
            return salaryNode?.ToString() ?? string.Empty;
        }

        var min = GetString(salary, "min");
        var max = GetString(salary, "max");
        var currency = GetString(salary, "currency");
        if (string.IsNullOrWhiteSpace(min) && string.IsNullOrWhiteSpace(max))
        {
            return string.Empty;
        }

        return $"{currency} {min}-{max}".Trim();
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeLocationType(string locationType, string location)
    {
        var value = $"{locationType} {location}";
        if (value.Contains("remote", StringComparison.OrdinalIgnoreCase)
            || value.Contains("remoto", StringComparison.OrdinalIgnoreCase))
        {
            return "Remote";
        }

        return value.Contains("hybrid", StringComparison.OrdinalIgnoreCase) ? "Hybrid" : locationType;
    }

    private static string GuessSource(string url)
    {
        if (url.Contains("greenhouse", StringComparison.OrdinalIgnoreCase))
        {
            return "Greenhouse";
        }

        if (url.Contains("lever", StringComparison.OrdinalIgnoreCase))
        {
            return "Lever";
        }

        if (url.Contains("ashby", StringComparison.OrdinalIgnoreCase))
        {
            return "Ashby";
        }

        if (url.Contains("smartrecruiters", StringComparison.OrdinalIgnoreCase))
        {
            return "SmartRecruiters";
        }

        if (url.Contains("teamtailor", StringComparison.OrdinalIgnoreCase))
        {
            return "Teamtailor";
        }

        if (url.Contains("recruitee", StringComparison.OrdinalIgnoreCase))
        {
            return "Recruitee";
        }

        if (url.Contains("workdayjobs", StringComparison.OrdinalIgnoreCase))
        {
            return "Workday";
        }

        return "jd-intel";
    }

    private static string Trim(string value, int maxLength)
    {
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record CacheEntry(IReadOnlyCollection<JobOpportunityDto> Results, DateTimeOffset ExpiresAt);

    private sealed class JdIntelSettings
    {
        public bool Enabled { get; private init; }
        public string Command { get; private init; } = "npx";
        public IReadOnlyCollection<string> Arguments { get; private init; } = ["-y", "jd-intel-mcp"];
        public string? WorkingDirectory { get; private init; }
        public string FetchJobsToolName { get; private init; } = "fetch_jobs";
        public int TimeoutSeconds { get; private init; } = 90;
        public int CacheMinutes { get; private init; } = 10;
        public int PostedWithinHours { get; private init; } = 720;
        public int MaxCompanies { get; private init; } = 12;
        public string[] Companies { get; private init; } = [];

        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));
        public TimeSpan CacheDuration => TimeSpan.FromMinutes(Math.Max(1, CacheMinutes));

        public static JdIntelSettings FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("OpportunityProviders:JdIntel");
            return new JdIntelSettings
            {
                Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                Command = string.IsNullOrWhiteSpace(section["Command"]) ? "npx" : section["Command"]!,
                Arguments = section.GetSection("Arguments").Get<string[]>() ?? ["-y", "jd-intel-mcp"],
                WorkingDirectory = section["WorkingDirectory"],
                FetchJobsToolName = section["FetchJobsToolName"] ?? "fetch_jobs",
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 90,
                CacheMinutes = int.TryParse(section["CacheMinutes"], out var cacheMinutes) ? cacheMinutes : 10,
                PostedWithinHours = int.TryParse(section["PostedWithinHours"], out var postedWithinHours) ? postedWithinHours : 720,
                MaxCompanies = int.TryParse(section["MaxCompanies"], out var maxCompanies) ? Math.Clamp(maxCompanies, 1, 50) : 12,
                Companies = section.GetSection("Companies").Get<string[]>() ?? []
            };
        }

        public string? ResolveWorkingDirectory()
        {
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                return null;
            }

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
            return string.Join(
                "|",
                request.Query.Trim().ToLowerInvariant(),
                request.Location?.Trim().ToLowerInvariant(),
                request.Limit,
                request.HoursOld.GetValueOrDefault(PostedWithinHours),
                request.IsRemote,
                string.Join(",", Companies).ToLowerInvariant());
        }
    }
}
