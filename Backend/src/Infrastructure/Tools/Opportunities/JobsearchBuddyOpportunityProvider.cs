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

internal sealed class JobsearchBuddyOpportunityProvider(
    IConfiguration configuration,
    ILogger<JobsearchBuddyOpportunityProvider> logger,
    ILoggerFactory loggerFactory) : IOpportunityProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "jobsearch-buddy";

    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchAsync(
        OpportunitySearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = JobsearchBuddySettings.FromConfiguration(configuration);
        if (!settings.Enabled)
        {
            return [];
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(settings.Timeout);

        await using var client = await CreateClientAsync(settings, timeoutCts.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeoutCts.Token);
        var searchTool = tools.FirstOrDefault(tool => string.Equals(tool.Name, settings.SearchToolName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("jobsearch-buddy MCP did not expose search_jobs.");

        var response = await client.CallToolAsync(
            searchTool.Name,
            new Dictionary<string, object?>
            {
                ["query"] = request.Query.Trim(),
                ["location_filter"] = request.Location?.Trim() ?? string.Empty,
                ["posted_since"] = MapPostedSince(request.HoursOld),
                ["ats"] = settings.Ats,
                ["limit"] = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 100)
            },
            cancellationToken: timeoutCts.Token);

        return Parse(response).ToArray();
    }

    public Task<JobOpportunityDto?> GetDetailsAsync(
        OpportunityDetailsRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<JobOpportunityDto?>(null);
    }

    private async Task<McpClient> CreateClientAsync(JobsearchBuddySettings settings, CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "jobsearch-buddy",
                Command = settings.Command,
                Arguments = settings.Arguments.ToArray(),
                WorkingDirectory = settings.ResolveWorkingDirectory(),
                EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
                InheritEnvironmentVariables = true,
                ShutdownTimeout = TimeSpan.FromSeconds(5),
                StandardErrorLines = line => logger.LogInformation("jobsearch-buddy MCP stderr: {Line}", line)
            },
            loggerFactory);

        return await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);
    }

    private static IReadOnlyCollection<JobOpportunityDto> Parse(CallToolResult response)
    {
        if (response.IsError == true)
        {
            throw new InvalidOperationException(ExtractText(response) ?? "jobsearch-buddy MCP tool returned an error.");
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

        return text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('[')
            ? ParseJsonNode(JsonNode.Parse(text))
            : [];
    }

    private static IReadOnlyCollection<JobOpportunityDto> ParseJsonNode(JsonNode? node)
    {
        var jobs = node switch
        {
            JsonArray array => array,
            JsonObject jsonObject when jsonObject["jobs"] is JsonArray jobsArray => jobsArray,
            JsonObject jsonObject when jsonObject["data"] is JsonArray dataArray => dataArray,
            _ => []
        };

        return jobs.OfType<JsonObject>().Select(ParseJobObject).ToArray();
    }

    private static JobOpportunityDto ParseJobObject(JsonObject job)
    {
        var company = GetString(job, "company", "company_name", "companyName");
        var id = GetString(job, "job_id", "jobId", "external_id", "id");
        var title = GetString(job, "title");
        var url = GetString(job, "url", "job_url", "apply_url");
        var posted = GetString(job, "posted_at", "published_at", "date");

        return new JobOpportunityDto
        {
            ResultKey = $"jobsearch-buddy:{company}:{id}:{url}",
            Id = id,
            Title = title,
            Company = company,
            Location = GetString(job, "location", "locations"),
            RemoteType = GetString(job, "remote", "remote_type"),
            EmploymentType = GetString(job, "employment_type", "commitment"),
            Salary = GetString(job, "salary"),
            PublishedAt = ParseDate(posted),
            Date = posted,
            Description = GetString(job, "short_jd", "description", "snippet"),
            Url = url,
            ApplyUrl = url,
            Source = GetString(job, "ats", "source"),
            Provider = "jobsearch-buddy"
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
                if (node is JsonArray array)
                {
                    return string.Join(", ", array.Select(item => item?.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)));
                }

                return node.GetValueKind() == JsonValueKind.String
                    ? node.GetValue<string>() ?? string.Empty
                    : node.ToString();
            }
        }

        return string.Empty;
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string MapPostedSince(int? hoursOld)
    {
        return hoursOld switch
        {
            <= 24 => "24h",
            <= 168 => "1w",
            <= 336 => "2w",
            > 0 => $"{Math.Max(1, (int)Math.Ceiling(hoursOld.Value / 24d))}d",
            _ => string.Empty
        };
    }

    private sealed class JobsearchBuddySettings
    {
        public bool Enabled { get; private init; }
        public string Command { get; private init; } = "uv";
        public IReadOnlyCollection<string> Arguments { get; private init; } = ["run", "--directory", "external/jobsearch-buddy", "jsb-mcp"];
        public string? WorkingDirectory { get; private init; }
        public string SearchToolName { get; private init; } = "search_jobs";
        public int TimeoutSeconds { get; private init; } = 30;
        public string[] Ats { get; private init; } = [];

        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));

        public static JobsearchBuddySettings FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("OpportunityProviders:JobsearchBuddy");
            return new JobsearchBuddySettings
            {
                Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                Command = section["Command"] ?? "uv",
                Arguments = section.GetSection("Arguments").Get<string[]>()
                    ?? ["run", "--directory", "external/jobsearch-buddy", "jsb-mcp"],
                WorkingDirectory = section["WorkingDirectory"],
                SearchToolName = section["SearchToolName"] ?? "search_jobs",
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 30,
                Ats = section.GetSection("Ats").Get<string[]>() ?? []
            };
        }

        public string? ResolveWorkingDirectory()
        {
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                return FindRepositoryRoot();
            }

            if (Path.IsPathRooted(WorkingDirectory))
            {
                return WorkingDirectory;
            }

            var root = FindRepositoryRoot();
            return root is null ? Path.GetFullPath(WorkingDirectory) : Path.GetFullPath(Path.Combine(root, WorkingDirectory));
        }

        private static string? FindRepositoryRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var current = new DirectoryInfo(start);
                while (current is not null)
                {
                    if (Directory.Exists(Path.Combine(current.FullName, "external", "jobsearch-buddy")))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }

            return null;
        }
    }
}
