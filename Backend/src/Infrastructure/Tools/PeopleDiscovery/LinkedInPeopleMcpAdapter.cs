using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lessie.Application.PeopleDiscovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Lessie.Infrastructure.Tools.PeopleDiscovery;

internal sealed class LinkedInPeopleMcpAdapter(
    IConfiguration configuration,
    ILogger<LinkedInPeopleMcpAdapter> logger,
    IPeopleDiscoveryProgressReporter progressReporter,
    IPeopleDiscoveryResultStore resultStore) : IPeopleDiscoveryAdapter, IPeopleDiscoveryJobSearchService
{
    private const string DefaultToolName = "search_people";
    private const string DefaultPostsToolName = "search_posts";
    private const string DefaultJobsToolName = "search_jobs";
    private const string LinkedInLoginMessage =
        "LinkedIn session is not authenticated. Run 'uv run linkedin-mcp-server --login' in external/linkedin-mcp-server, complete the browser login, then retry.";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> SearchAsync(string query, string userId, CancellationToken cancellationToken)
    {
        var settings = PeopleDiscoveryMcpSettings.FromConfiguration(configuration);
        settings.Validate();
        var hasUserContext = Guid.TryParse(userId, out var parsedUserId);
        IReadOnlyCollection<PeopleDiscoveryPersonDto> previousResults = hasUserContext
            ? await resultStore.FindPreviousResultsAsync(parsedUserId, query, "LinkedIn People", cancellationToken)
            : [];
        var shouldSearchPeople = previousResults.Count < settings.CachedPeopleResultThreshold;

        if (previousResults.Count > 0)
        {
            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = "info",
                    Message = $"Loaded {previousResults.Count} previous People Discovery results for this user.",
                    Progress = 5,
                    Total = 100,
                    PeopleCount = previousResults.Count
                },
                cancellationToken);
        }

        IReadOnlyCollection<PeopleDiscoveryPersonDto> peopleResults = [];
        if (!shouldSearchPeople)
        {
            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = "info",
                    Message = "Previous people results are enough for this query. Skipping LinkedIn people navigation.",
                    Progress = 100,
                    Total = 100,
                    PeopleCount = previousResults.Count
                },
                cancellationToken);

            return previousResults;
        }

        await using var client = await LinkedInMcpStdioClient.StartAsync(settings, logger, progressReporter, cancellationToken);
        await client.InitializeAsync(cancellationToken);
        await client.EnsureToolExistsAsync(settings.ToolName, cancellationToken);

        logger.LogInformation(
            "LinkedIn MCP people search started. Query: {Query}. Timeout: {TimeoutSeconds}s.",
            query,
            settings.TimeoutSeconds);
        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = "LinkedIn MCP people search started.",
                Progress = previousResults.Count > 0 ? 10 : 0,
                Total = 100
            },
            cancellationToken);

        var response = await client.CallToolAsync(
            settings.ToolName,
            new JsonObject
            {
                ["keywords"] = query
            },
            cancellationToken);

        peopleResults = LinkedInPeopleResultParser.ParsePeople(response);
        if (peopleResults.Count == 0)
        {
            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = "warning",
                    Message = "Raw MCP people response diagnostic (temporary).",
                    Details = LinkedInPeopleResultParser.BuildDebugSummary(response),
                    PeopleCount = 0
                },
                cancellationToken);
        }

        IReadOnlyCollection<PeopleDiscoveryPersonDto> freshResults = peopleResults;
        if (hasUserContext)
        {
            freshResults = await resultStore.SaveAndFilterAsync(parsedUserId, query, peopleResults, cancellationToken);
        }

        var results = LinkedInPeopleResultParser.Deduplicate(previousResults.Concat(freshResults)).ToArray();

        logger.LogInformation(
            "LinkedIn MCP people search finished. Previous: {PreviousCount}. Fresh: {FreshCount}. Results: {ResultCount}.",
            previousResults.Count,
            peopleResults.Count,
            results.Length);
        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = $"LinkedIn MCP people search finished. Previous: {previousResults.Count}. Fresh: {peopleResults.Count}. Results: {results.Length}.",
                Progress = 100,
                Total = 100,
                PeopleCount = results.Length
            },
            cancellationToken);

        return results;
    }

    public async Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> SearchPostsAsync(
        string query,
        string userId,
        string? location,
        CancellationToken cancellationToken)
    {
        var settings = PeopleDiscoveryMcpSettings.FromConfiguration(configuration);
        settings.Validate();
        var effectiveLocation = ResolveLocation(location, settings.DefaultLocation);
        var hasUserContext = Guid.TryParse(userId, out var parsedUserId);
        var previousResults = hasUserContext
            ? await resultStore.FindPreviousResultsAsync(parsedUserId, BuildLocalizedQueryText(query, effectiveLocation), "LinkedIn Post", cancellationToken)
            : [];
        var shouldSearchPosts = previousResults.Count < settings.CachedPostResultThreshold;

        if (previousResults.Count > 0)
        {
            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = "info",
                    Message = $"Loaded {previousResults.Count} previous Post Search results for this user.",
                    Progress = 5,
                    Total = 100,
                    PeopleCount = previousResults.Count
                },
                cancellationToken);
        }

        if (!shouldSearchPosts)
        {
            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = "info",
                    Message = "Previous post results are enough for this query. Skipping LinkedIn post navigation.",
                    Progress = 100,
                    Total = 100,
                    PeopleCount = previousResults.Count
                },
                cancellationToken);

            return previousResults;
        }

        await using var client = await LinkedInMcpStdioClient.StartAsync(settings, logger, progressReporter, cancellationToken);
        await client.InitializeAsync(cancellationToken);
        await client.EnsureToolExistsAsync(settings.PostsToolName, cancellationToken);

        logger.LogInformation(
            "LinkedIn MCP posts search started. Query: {Query}. Location: {Location}. Timeout: {TimeoutSeconds}s.",
            query,
            effectiveLocation,
            settings.TimeoutSeconds);
        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = $"LinkedIn MCP posts search started. Location: {effectiveLocation}.",
                Progress = 10,
                Total = 100,
                PeopleCount = previousResults.Count
            },
            cancellationToken);

        var response = await client.CallToolAsync(
            settings.PostsToolName,
            new JsonObject
            {
                ["keywords"] = query,
                ["location"] = effectiveLocation,
                ["page_count"] = settings.PostSearchPageCount,
                ["max_results"] = settings.PostSearchMaxResults,
                ["lightweight"] = settings.PostSearchLightweight
            },
            cancellationToken);

        var postResults = LinkedInPeopleResultParser.ParsePosts(response);
        if (postResults.Count == 0)
        {
            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = "warning",
                    Message = "Raw MCP posts response diagnostic (temporary).",
                    Details = LinkedInPeopleResultParser.BuildDebugSummary(response),
                    PeopleCount = previousResults.Count
                },
                cancellationToken);
        }

        IReadOnlyCollection<PeopleDiscoveryPersonDto> freshResults = postResults;
        if (hasUserContext)
        {
            freshResults = await resultStore.SaveAndFilterAsync(
                parsedUserId,
                BuildLocalizedQueryText(query, effectiveLocation),
                postResults,
                cancellationToken);
        }

        var results = LinkedInPeopleResultParser.Deduplicate(previousResults.Concat(freshResults)).ToArray();
        logger.LogInformation(
            "LinkedIn MCP posts search finished. Previous: {PreviousCount}. Fresh: {FreshCount}. Results: {ResultCount}.",
            previousResults.Count,
            postResults.Count,
            results.Length);
        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = $"LinkedIn MCP posts search finished. Previous: {previousResults.Count}. Fresh: {postResults.Count}. Results: {results.Length}.",
                Progress = 100,
                Total = 100,
                PeopleCount = results.Length
            },
            cancellationToken);

        return results;
    }

    public async Task<IReadOnlyCollection<PeopleDiscoveryJobDto>> SearchAsync(
        PeopleDiscoveryJobSearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = PeopleDiscoveryMcpSettings.FromConfiguration(configuration);
        settings.Validate();
        var effectiveLocation = ResolveLocation(request.Location, settings.DefaultLocation);

        await using var client = await LinkedInMcpStdioClient.StartAsync(settings, logger, progressReporter, cancellationToken);
        await client.InitializeAsync(cancellationToken);
        await client.EnsureToolExistsAsync(settings.JobsToolName, cancellationToken);

        logger.LogInformation(
            "LinkedIn MCP jobs search started. Keywords: {Keywords}. Location: {Location}. MaxPages: {MaxPages}.",
            request.Keywords,
            effectiveLocation,
            request.MaxPages);
        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = $"LinkedIn MCP jobs search started. Location: {effectiveLocation}.",
                Progress = 10,
                Total = 100
            },
            cancellationToken);

        var arguments = new JsonObject
        {
            ["keywords"] = request.Keywords.Trim(),
            ["max_pages"] = Math.Clamp(request.MaxPages, 1, 10),
            ["easy_apply"] = request.EasyApply
        };

        AddOptional(arguments, "location", effectiveLocation);
        AddOptional(arguments, "date_posted", request.DatePosted);
        AddOptional(arguments, "job_type", request.JobType);
        AddOptional(arguments, "experience_level", request.ExperienceLevel);
        AddOptional(arguments, "work_type", request.WorkType);
        AddOptional(arguments, "sort_by", request.SortBy);

        var response = await client.CallToolAsync(settings.JobsToolName, arguments, cancellationToken);
        var results = LinkedInPeopleResultParser.ParseJobs(response);
        if (results.Count == 0)
        {
            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = "warning",
                    Message = "Raw MCP jobs response diagnostic (temporary).",
                    Details = LinkedInPeopleResultParser.BuildDebugSummary(response)
                },
                cancellationToken);
        }

        results = await resultStore.SaveAndFilterJobsAsync(
            userId,
            BuildJobQueryText(request, effectiveLocation),
            results,
            cancellationToken);

        logger.LogInformation("LinkedIn MCP jobs search finished. Results: {ResultCount}.", results.Count);
        await progressReporter.ReportAsync(
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = $"LinkedIn MCP jobs search finished. Results: {results.Count}.",
                Progress = 100,
                Total = 100
            },
            cancellationToken);
        return results;
    }

    private static string ResolveLocation(string? requestedLocation, string defaultLocation)
    {
        return string.IsNullOrWhiteSpace(requestedLocation)
            ? defaultLocation
            : requestedLocation.Trim();
    }

    private static string BuildLocalizedQueryText(string query, string location)
    {
        return string.IsNullOrWhiteSpace(location) ? query : $"{query} | location:{location}";
    }

    private static string BuildJobQueryText(PeopleDiscoveryJobSearchRequest request, string location)
    {
        var filters = new[]
        {
            request.Keywords,
            location,
            request.DatePosted,
            request.JobType,
            request.ExperienceLevel,
            request.WorkType,
            request.EasyApply ? "easy_apply" : null,
            request.SortBy,
            $"pages:{Math.Clamp(request.MaxPages, 1, 10)}"
        };

        return string.Join(" | ", filters.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void AddOptional(JsonObject arguments, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[name] = value.Trim();
        }
    }

    private sealed class PeopleDiscoveryMcpSettings
    {
        public bool Enabled { get; private init; }
        public string Provider { get; private init; } = "LinkedInMcp";
        public string Command { get; private init; } = string.Empty;
        public IReadOnlyCollection<string> Arguments { get; private init; } = [];
        public string? WorkingDirectory { get; private init; }
        public int TimeoutSeconds { get; private init; } = 60;
        public string ToolName { get; private init; } = DefaultToolName;
        public string PostsToolName { get; private init; } = DefaultPostsToolName;
        public string JobsToolName { get; private init; } = DefaultJobsToolName;
        public string DefaultLocation { get; private init; } = "Brazil";
        public int CachedPeopleResultThreshold { get; private init; } = 8;
        public int CachedPostResultThreshold { get; private init; } = 8;
        public int PostSearchPageCount { get; private init; } = 1;
        public int PostSearchMaxResults { get; private init; } = 12;
        public bool PostSearchLightweight { get; private init; } = true;

        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));

        public static PeopleDiscoveryMcpSettings FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("Mcp:PeopleDiscovery");
            return new PeopleDiscoveryMcpSettings
            {
                Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                Provider = section["Provider"] ?? "LinkedInMcp",
                Command = section["Command"] ?? string.Empty,
                Arguments = section.GetSection("Arguments").Get<string[]>() ?? [],
                WorkingDirectory = section["WorkingDirectory"],
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 60,
                ToolName = section["ToolName"] ?? DefaultToolName,
                PostsToolName = section["PostsToolName"] ?? DefaultPostsToolName,
                JobsToolName = section["JobsToolName"] ?? DefaultJobsToolName,
                DefaultLocation = string.IsNullOrWhiteSpace(section["DefaultLocation"]) ? "Brazil" : section["DefaultLocation"]!,
                CachedPeopleResultThreshold = int.TryParse(section["CachedPeopleResultThreshold"], out var cachedThreshold)
                    ? Math.Max(1, cachedThreshold)
                    : 8,
                CachedPostResultThreshold = int.TryParse(section["CachedPostResultThreshold"], out var cachedPostThreshold)
                    ? Math.Max(1, cachedPostThreshold)
                    : 8,
                PostSearchPageCount = int.TryParse(section["PostSearchPageCount"], out var postPageCount)
                    ? Math.Clamp(postPageCount, 1, 5)
                    : 1,
                PostSearchMaxResults = int.TryParse(section["PostSearchMaxResults"], out var postMaxResults)
                    ? Math.Clamp(postMaxResults, 1, 50)
                    : 12,
                PostSearchLightweight = !bool.TryParse(section["PostSearchLightweight"], out var lightweight) || lightweight
            };
        }

        public void Validate()
        {
            if (!Enabled || !string.Equals(Provider, "LinkedInMcp", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("LinkedIn MCP is not configured.");
            }

            if (string.IsNullOrWhiteSpace(Command))
            {
                throw new InvalidOperationException("LinkedIn MCP is not configured.");
            }
        }

        public string? ResolveWorkingDirectory()
        {
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                return null;
            }

            if (Path.IsPathRooted(WorkingDirectory))
            {
                if (!Directory.Exists(WorkingDirectory))
                {
                    throw new InvalidOperationException("LinkedIn MCP is not configured.");
                }

                EnsureMcpProjectExists(WorkingDirectory);
                return WorkingDirectory;
            }

            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current is not null)
            {
                var candidate = Path.GetFullPath(Path.Combine(current.FullName, WorkingDirectory));
                if (Directory.Exists(candidate))
                {
                    EnsureMcpProjectExists(candidate);
                    return candidate;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("LinkedIn MCP is not configured.");
        }

        private static void EnsureMcpProjectExists(string workingDirectory)
        {
            if (!File.Exists(Path.Combine(workingDirectory, "pyproject.toml")))
            {
                throw new InvalidOperationException(
                    "LinkedIn MCP is not installed. Clone eliasbiondo/linkedin-mcp-server into external/linkedin-mcp-server.");
            }
        }
    }

    private sealed class LinkedInMcpStdioClient : IAsyncDisposable
    {
        private readonly Process process;
        private readonly StreamWriter stdin;
        private readonly StreamReader stdout;
        private readonly StringBuilder stderr = new();
        private readonly object stderrLock = new();
        private readonly TimeSpan timeout;
        private readonly ILogger logger;
        private readonly IPeopleDiscoveryProgressReporter progressReporter;
        private int reportedKnownStderrIssue;
        private int nextRequestId;

        private LinkedInMcpStdioClient(
            Process process,
            TimeSpan timeout,
            ILogger logger,
            IPeopleDiscoveryProgressReporter progressReporter)
        {
            this.process = process;
            this.timeout = timeout;
            this.logger = logger;
            this.progressReporter = progressReporter;
            stdin = process.StandardInput;
            stdout = process.StandardOutput;
            _ = CaptureStderrAsync(process.StandardError);
        }

        public static Task<LinkedInMcpStdioClient> StartAsync(
            PeopleDiscoveryMcpSettings settings,
            ILogger logger,
            IPeopleDiscoveryProgressReporter progressReporter,
            CancellationToken cancellationToken)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = settings.Command,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = Utf8NoBom,
                    StandardOutputEncoding = Utf8NoBom,
                    StandardErrorEncoding = Utf8NoBom,
                    CreateNoWindow = true
                };

                foreach (var argument in settings.Arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                var workingDirectory = settings.ResolveWorkingDirectory();
                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    startInfo.WorkingDirectory = workingDirectory;
                }

                logger.LogInformation(
                    "Starting LinkedIn MCP process. Command: {Command}. WorkingDirectory: {WorkingDirectory}. Timeout: {TimeoutSeconds}s.",
                    settings.Command,
                    startInfo.WorkingDirectory,
                    settings.TimeoutSeconds);

                var process = Process.Start(startInfo);
                if (process is null || process.HasExited)
                {
                    throw new InvalidOperationException("LinkedIn MCP process could not be started.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                logger.LogInformation("LinkedIn MCP process started. ProcessId: {ProcessId}.", process.Id);
                return Task.FromResult(new LinkedInMcpStdioClient(process, settings.Timeout, logger, progressReporter));
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 2)
            {
                throw new InvalidOperationException(
                    $"LinkedIn MCP command '{settings.Command}' was not found. Install uv and make sure it is available in PATH.",
                    exception);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("LinkedIn MCP process could not be started.", exception);
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await SendRequestAsync(
                "initialize",
                new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject
                    {
                        ["name"] = "Lessie",
                        ["version"] = "1.0.0"
                    }
                },
                cancellationToken);

            await SendNotificationAsync("notifications/initialized", cancellationToken);
        }

        public async Task EnsureToolExistsAsync(string toolName, CancellationToken cancellationToken)
        {
            var toolsResponse = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            var tools = toolsResponse["result"]?["tools"] as JsonArray;

            var exists = tools?.Any(tool =>
                string.Equals(tool?["name"]?.GetValue<string>(), toolName, StringComparison.OrdinalIgnoreCase)) == true;

            if (!exists)
            {
                throw new InvalidOperationException("LinkedIn MCP returned an invalid response.");
            }
        }

        public async Task<JsonNode> CallToolAsync(string toolName, JsonObject arguments, CancellationToken cancellationToken)
        {
            var requestId = Interlocked.Increment(ref nextRequestId);
            return await SendRequestAsync(
                requestId,
                "tools/call",
                new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments,
                    ["_meta"] = new JsonObject
                    {
                        ["progressToken"] = $"people-discovery-{requestId}"
                    }
                },
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort cleanup for an external MCP process.
            }

            process.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task<JsonNode> SendRequestAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            var requestId = Interlocked.Increment(ref nextRequestId);
            return await SendRequestAsync(requestId, method, parameters, cancellationToken);
        }

        private async Task<JsonNode> SendRequestAsync(
            int requestId,
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            var request = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = requestId,
                ["method"] = method,
                ["params"] = parameters
            };

            await WriteMessageAsync(request, cancellationToken);
            logger.LogDebug("LinkedIn MCP request sent. RequestId: {RequestId}. Method: {Method}.", requestId, method);
            return await ReadResponseAsync(requestId, method, cancellationToken);
        }

        private async Task SendNotificationAsync(string method, CancellationToken cancellationToken)
        {
            await WriteMessageAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["method"] = method
                },
                cancellationToken);
        }

        private async Task WriteMessageAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var json = message.ToJsonString(JsonOptions);
            await stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
            await stdin.FlushAsync(cancellationToken);
        }

        private async Task<JsonNode> ReadResponseAsync(int requestId, string method, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            var startedAt = DateTimeOffset.UtcNow;
            var heartbeatTask = LogHeartbeatAsync(requestId, method, startedAt, heartbeatCts.Token);

            try
            {
                while (!timeoutCts.IsCancellationRequested)
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException(BuildProcessExitedMessage());
                    }

                    string? line;
                    try
                    {
                        line = await stdout.ReadLineAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new InvalidOperationException(BuildTimeoutMessage(method));
                    }

                    if (line is null)
                    {
                        throw new InvalidOperationException(BuildProcessExitedMessage());
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    JsonObject? message;
                    try
                    {
                        message = JsonNode.Parse(line) as JsonObject;
                    }
                    catch (JsonException)
                    {
                        continue;
                    }

                    if (message is null)
                    {
                        continue;
                    }

                    if (TryGetValue<int>(message, "id") != requestId)
                    {
                        try
                        {
                            await LogMcpNotificationAsync(message, timeoutCts.Token);
                        }
                        catch (Exception notificationException)
                        {
                            logger.LogWarning(
                                notificationException,
                                "LinkedIn MCP notification could not be parsed. RawMessage: {RawMessage}",
                                message.ToJsonString(JsonOptions));
                        }

                        continue;
                    }

                    if (message["error"] is { } errorNode)
                    {
                        throw new InvalidOperationException(GetErrorMessage(errorNode, GetStderrSnapshot()));
                    }

                    logger.LogDebug(
                        "LinkedIn MCP response received. RequestId: {RequestId}. Method: {Method}. ElapsedSeconds: {ElapsedSeconds}.",
                        requestId,
                        method,
                        (DateTimeOffset.UtcNow - startedAt).TotalSeconds);

                    return message;
                }

                throw new InvalidOperationException(BuildTimeoutMessage(method));
            }
            finally
            {
                await heartbeatCts.CancelAsync();
                try
                {
                    await heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private async Task LogHeartbeatAsync(int requestId, string method, DateTimeOffset startedAt, CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var elapsedSeconds = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
                logger.LogInformation(
                    "LinkedIn MCP is still working. RequestId: {RequestId}. Method: {Method}. ElapsedSeconds: {ElapsedSeconds}. ProcessId: {ProcessId}. ProcessRunning: {ProcessRunning}.",
                    requestId,
                    method,
                    Math.Round(elapsedSeconds),
                    process.Id,
                    !process.HasExited);

                await progressReporter.ReportAsync(
                    new PeopleDiscoveryProgressEvent
                    {
                        Level = "info",
                        Message = "LinkedIn MCP is still working.",
                        ElapsedSeconds = Math.Round(elapsedSeconds),
                        ProcessId = process.Id,
                        ProcessRunning = !process.HasExited
                    },
                    cancellationToken);
            }
        }

        private async Task LogMcpNotificationAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var method = TryGetValue<string>(message, "method");
            if (string.IsNullOrWhiteSpace(method))
            {
                return;
            }

            var parameters = TryGetObject(message, "params");
            if (string.Equals(method, "notifications/progress", StringComparison.OrdinalIgnoreCase))
            {
                var progressMessage = TryGetValue<string>(parameters, "message") ?? "LinkedIn MCP progress.";
                logger.LogInformation(
                    "LinkedIn MCP progress. Progress: {Progress}. Total: {Total}. Message: {Message}. Token: {ProgressToken}.",
                    TryGetValue<double>(parameters, "progress"),
                    TryGetValue<double>(parameters, "total"),
                    progressMessage,
                    TryGetValue<string>(parameters, "progressToken"));
                await progressReporter.ReportAsync(
                    new PeopleDiscoveryProgressEvent
                    {
                        Level = "info",
                        Message = progressMessage,
                        Progress = TryGetValue<double>(parameters, "progress"),
                        Total = TryGetValue<double>(parameters, "total")
                    },
                    cancellationToken);
                return;
            }

            var level = TryGetValue<string>(parameters, "level");
            var dataNode = TryGetProperty(parameters, "data");
            var data = dataNode?.ToJsonString(JsonOptions);
            logger.LogInformation(
                "LinkedIn MCP notification received. Method: {Method}. Level: {Level}. Data: {Data}.",
                method,
                level,
                data);

            var isError = string.Equals(level, "error", StringComparison.OrdinalIgnoreCase);
            var notificationDetails = ExtractNotificationDetails(dataNode, message, isError);
            var notificationMessage = string.Equals(notificationDetails, LinkedInLoginMessage, StringComparison.Ordinal)
                ? LinkedInLoginMessage
                : ExtractNotificationMessage(dataNode);

            await progressReporter.ReportAsync(
                new PeopleDiscoveryProgressEvent
                {
                    Level = level ?? "info",
                    Message = notificationMessage,
                    Details = notificationDetails,
                    PeopleCount = isError ? null : ExtractPeopleCount(dataNode)
                },
                cancellationToken);
        }

        private static string ExtractNotificationMessage(JsonNode? dataNode)
        {
            if (dataNode is JsonValue dataValue && dataValue.TryGetValue<string>(out var text))
            {
                return text;
            }

            var dataObject = dataNode as JsonObject;
            var message = TryGetValue<string>(dataObject, "msg")
                ?? TryGetValue<string>(dataObject, "message")
                ?? "LinkedIn MCP notification received.";
            var error = TryGetValue<string>(TryGetObject(dataObject, "extra"), "error");

            if (!string.IsNullOrWhiteSpace(error))
            {
                return string.Equals(message, "Internal Server Error", StringComparison.OrdinalIgnoreCase)
                    ? $"LinkedIn MCP error: {error}"
                    : $"{message}: {error}";
            }

            return message;
        }

        private string? ExtractNotificationDetails(JsonNode? dataNode, JsonObject rawMessage, bool isError)
        {
            if (!isError)
            {
                return null;
            }

            var knownIssue = TryGetKnownIssue(GetStderrSnapshot());
            if (!string.IsNullOrWhiteSpace(knownIssue))
            {
                return knownIssue;
            }

            var dataObject = dataNode as JsonObject;
            var error = TryGetValue<string>(TryGetObject(dataObject, "extra"), "error");
            if (!string.IsNullOrWhiteSpace(error))
            {
                return error;
            }

            var data = dataNode?.ToJsonString(JsonOptions);
            var rawJson = rawMessage.ToJsonString(JsonOptions);
            var stderrSnapshot = GetStderrSnapshot();

            if (!string.IsNullOrWhiteSpace(stderrSnapshot))
            {
                return $"stderr: {stderrSnapshot} | mcp: {rawJson}";
            }

            if (dataNode is JsonValue dataValue
                && dataValue.TryGetValue<string>(out var text)
                && string.Equals(text, "Internal Server Error", StringComparison.OrdinalIgnoreCase))
            {
                return rawJson;
            }

            return string.IsNullOrWhiteSpace(data) ? rawJson : $"{data} | raw: {rawJson}";
        }

        private static int? ExtractPeopleCount(JsonNode? dataNode)
        {
            var dataObject = dataNode as JsonObject;
            var extra = TryGetObject(dataObject, "extra");
            return TryGetValue<int>(extra, "peopleCount");
        }

        private static JsonNode? TryGetProperty(JsonObject? jsonObject, string propertyName)
        {
            return jsonObject is not null && jsonObject.TryGetPropertyValue(propertyName, out var value) ? value : null;
        }

        private static JsonObject? TryGetObject(JsonObject? jsonObject, string propertyName)
        {
            return TryGetProperty(jsonObject, propertyName) as JsonObject;
        }

        private static T? TryGetValue<T>(JsonObject? jsonObject, string propertyName)
        {
            var node = TryGetProperty(jsonObject, propertyName);
            if (node is JsonValue value && value.TryGetValue<T>(out var result))
            {
                return result;
            }

            return default;
        }

        private async Task CaptureStderrAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                AppendStderr(line);

                var knownIssue = TryGetKnownIssue(line);
                if (string.IsNullOrWhiteSpace(knownIssue)
                    || Interlocked.Exchange(ref reportedKnownStderrIssue, 1) == 1)
                {
                    continue;
                }

                await progressReporter.ReportAsync(
                    new PeopleDiscoveryProgressEvent
                    {
                        Level = "error",
                        Message = knownIssue,
                        Details = "Authentication failed before LinkedIn scraping started."
                    },
                    CancellationToken.None);
            }
        }

        private void AppendStderr(string line)
        {
            lock (stderrLock)
            {
                if (stderr.Length > 4000)
                {
                    stderr.Remove(0, Math.Min(stderr.Length, 1000));
                }

                stderr.AppendLine(line);
            }
        }

        private string GetStderrSnapshot()
        {
            lock (stderrLock)
            {
                return stderr.ToString().Trim();
            }
        }

        private string BuildProcessExitedMessage()
        {
            var details = GetStderrSnapshot();
            var knownIssue = TryGetKnownIssue(details);
            if (!string.IsNullOrWhiteSpace(knownIssue))
            {
                return knownIssue;
            }

            return string.IsNullOrWhiteSpace(details)
                ? "LinkedIn MCP process could not be started."
                : $"LinkedIn MCP process could not be started. {details}";
        }

        private string BuildTimeoutMessage(string method)
        {
            return $"LinkedIn MCP request '{method}' did not finish within {timeout.TotalSeconds:0}s. "
                + $"ProcessRunning={!process.HasExited}. Increase Mcp:PeopleDiscovery:TimeoutSeconds if LinkedIn scraping is still progressing.";
        }

        private static string GetErrorMessage(JsonNode errorNode, string stderrSnapshot)
        {
            var errorObject = errorNode as JsonObject;
            var message = TryGetValue<string>(errorObject, "message");
            var details = TryGetProperty(errorObject, "data")?.ToJsonString(JsonOptions);
            var knownIssue = TryGetKnownIssue($"{message} {details} {stderrSnapshot}");
            if (!string.IsNullOrWhiteSpace(knownIssue))
            {
                return knownIssue;
            }

            if (string.IsNullOrWhiteSpace(details) && !string.IsNullOrWhiteSpace(stderrSnapshot))
            {
                details = stderrSnapshot;
            }

            if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(details))
            {
                return $"{message}. Details: {details}";
            }

            return string.IsNullOrWhiteSpace(message)
                ? "LinkedIn MCP returned an invalid response."
                : message;
        }

        private static string? TryGetKnownIssue(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return text.Contains("LinkedIn session is not authenticated", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Authentication required", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Please run the server with --login", StringComparison.OrdinalIgnoreCase)
                ? LinkedInLoginMessage
                : null;
        }
    }

    private static class LinkedInPeopleResultParser
    {
        private const int DebugPreviewMaxLength = 6000;

        public static IReadOnlyCollection<PeopleDiscoveryPersonDto> ParsePeople(JsonNode response)
        {
            var result = response["result"] ?? throw new InvalidOperationException("LinkedIn MCP returned an invalid response.");
            var payload = ExtractPayload(result);
            var candidates = FindProfileArrays(payload)
                .Select(array => array.Select(MapPerson).Where(IsLikelyPersonProfile).ToArray())
                .Where(people => people.Length > 0)
                .OrderByDescending(people => people.Length)
                .FirstOrDefault();

            return candidates ?? [];
        }

        public static IReadOnlyCollection<PeopleDiscoveryPersonDto> ParsePosts(JsonNode response)
        {
            var result = response["result"] ?? throw new InvalidOperationException("LinkedIn MCP returned an invalid response.");
            var payload = ExtractPayload(result);
            var candidates = FindArraysByName(payload, "posts")
                .Select(array => array.Select(MapPost).Where(person => !IsEmpty(person)).ToArray())
                .Where(posts => posts.Length > 0)
                .OrderByDescending(posts => posts.Length)
                .FirstOrDefault();

            return candidates ?? [];
        }

        public static IReadOnlyCollection<PeopleDiscoveryJobDto> ParseJobs(JsonNode response)
        {
            var result = response["result"] ?? throw new InvalidOperationException("LinkedIn MCP returned an invalid response.");
            var payload = ExtractPayload(result);
            var jobs = FindArraysByName(payload, "jobs")
                .SelectMany(array => array.Select(MapJob).Where(job => !IsEmpty(job)))
                .GroupBy(job => job.JobUrl + "|" + job.JobId + "|" + job.Title, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            return jobs.Length > 0 ? jobs : MapJobIds(payload);
        }

        public static IEnumerable<PeopleDiscoveryPersonDto> Deduplicate(IEnumerable<PeopleDiscoveryPersonDto> results)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in results)
            {
                var key = string.Join(
                    "|",
                    result.Source,
                    result.ProfileUrl,
                    result.Name,
                    result.Title);

                if (seen.Add(key))
                {
                    yield return result;
                }
            }
        }

        public static string BuildDebugSummary(JsonNode response)
        {
            JsonNode payload;
            try
            {
                payload = ExtractPayload(response["result"] ?? response);
            }
            catch (Exception exception)
            {
                return $"Could not extract MCP payload: {exception.Message}\n\nRaw response:\n{Truncate(response.ToJsonString(JsonOptions))}";
            }

            var payloadObject = payload as JsonObject;
            var url = TryGetValue<string>(payloadObject, "url") ?? "(no url)";
            var searchResults = TryGetProperty(TryGetObject(payloadObject, "sections"), "search_results") as JsonObject;
            var raw = TryGetValue<string>(searchResults, "raw");
            var people = TryGetProperty(searchResults, "people") as JsonArray;
            var posts = TryGetProperty(searchResults, "posts") as JsonArray;
            var rawOrPayload = string.IsNullOrWhiteSpace(raw)
                ? payload.ToJsonString(JsonOptions)
                : raw;

            var diagnostics = new[]
            {
                $"url: {url}",
                $"serializedPeopleCount: {people?.Count ?? 0}",
                $"serializedPostCount: {posts?.Count ?? 0}",
                $"rawHtmlAvailable: {!string.IsNullOrWhiteSpace(raw)}",
                $"rawLength: {rawOrPayload.Length}",
                $"profileLinkOccurrences: {Regex.Matches(rawOrPayload, "/in/", RegexOptions.IgnoreCase).Count}",
                $"postLinkOccurrences: {Regex.Matches(rawOrPayload, "/feed/update|/posts/|highlightedUpdateUrn", RegexOptions.IgnoreCase).Count}",
                $"peopleSearchResultOccurrences: {Regex.Matches(rawOrPayload, "people-search-result", RegexOptions.IgnoreCase).Count}",
                $"searchResultTitleOccurrences: {Regex.Matches(rawOrPayload, "search-result-lockup-title", RegexOptions.IgnoreCase).Count}",
                $"authWallOccurrences: {Regex.Matches(rawOrPayload, "authwall|login|checkpoint", RegexOptions.IgnoreCase).Count}"
            };

            return string.Join('\n', diagnostics) + "\n\npreview:\n" + Truncate(Normalize(rawOrPayload));
        }

        private static JsonNode ExtractPayload(JsonNode result)
        {
            var resultObject = result as JsonObject;
            if (TryGetProperty(resultObject, "structuredContent") is JsonNode structuredContent)
            {
                return structuredContent;
            }

            if (TryGetProperty(resultObject, "content") is JsonArray content)
            {
                foreach (var item in content)
                {
                    var text = TryGetValue<string>(item as JsonObject, "text");
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    try
                    {
                        if (JsonNode.Parse(text) is JsonNode parsed)
                        {
                            return parsed;
                        }
                    }
                    catch (JsonException)
                    {
                        continue;
                    }
                }
            }

            return result;
        }

        private static IEnumerable<IReadOnlyCollection<JsonObject>> FindProfileArrays(JsonNode node)
        {
            foreach (var propertyName in new[] { "people", "profiles", "profileResults", "profile_results" })
            {
                foreach (var found in FindArraysByName(node, propertyName))
                {
                    yield return found;
                }
            }
        }

        private static IEnumerable<IReadOnlyCollection<JsonObject>> FindArraysByName(JsonNode node, string propertyName)
        {
            foreach (var array in FindJsonArraysByName(node, propertyName))
            {
                var objects = array.OfType<JsonObject>().ToArray();
                if (objects.Length > 0)
                {
                    yield return objects;
                }
            }
        }

        private static IEnumerable<JsonArray> FindJsonArraysByName(JsonNode node, string propertyName)
        {
            if (node is not JsonObject jsonObject)
            {
                yield break;
            }

            foreach (var property in jsonObject)
            {
                if (property.Value is JsonArray array
                    && string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return array;
                }

                if (property.Value is null)
                {
                    continue;
                }

                foreach (var found in FindJsonArraysByName(property.Value, propertyName))
                {
                    yield return found;
                }
            }
        }

        private static PeopleDiscoveryPersonDto MapPerson(JsonObject person)
        {
            return new PeopleDiscoveryPersonDto
            {
                Name = FirstString(person, "name", "fullName", "full_name", "title"),
                Title = FirstString(person, "headline", "subtitle", "occupation", "position", "jobTitle", "job_title"),
                Company = FirstString(person, "company", "current", "currentCompany", "current_company", "organization"),
                Location = FirstString(person, "location", "geo", "region"),
                ContactInfo = string.Empty,
                ProfileUrl = FirstString(person, "profileUrl", "profile_url", "linkedinUrl", "linkedin_url", "url", "link"),
                Source = "LinkedIn People"
            };
        }

        private static PeopleDiscoveryPersonDto MapPost(JsonObject post)
        {
            var postText = FirstString(post, "postText", "post_text", "text", "content", "title");
            return new PeopleDiscoveryPersonDto
            {
                Name = FirstString(post, "authorName", "author_name", "author", "name"),
                Title = TrimTo(postText, 280),
                Company = FirstString(post, "authorHeadline", "author_headline", "headline", "subtitle"),
                Location = FirstString(post, "postedAt", "posted_at", "date", "time", "socialText", "social_text"),
                ContactInfo = ExtractContactInfo(postText),
                ProfileUrl = FirstString(post, "postUrl", "post_url", "url", "link", "authorProfileUrl", "author_profile_url"),
                Source = "LinkedIn Post"
            };
        }

        private static PeopleDiscoveryJobDto MapJob(JsonObject job)
        {
            return new PeopleDiscoveryJobDto
            {
                Title = FirstString(job, "title", "jobTitle", "job_title"),
                Company = FirstString(job, "company", "companyName", "company_name"),
                Location = FirstString(job, "location", "geo", "region"),
                JobId = FirstString(job, "jobId", "job_id", "id"),
                JobUrl = FirstString(job, "jobUrl", "job_url", "url", "link"),
                Insight = FirstString(job, "insight", "description"),
                Metadata = FirstString(job, "metadata"),
                Source = "LinkedIn Jobs"
            };
        }

        private static IReadOnlyCollection<PeopleDiscoveryJobDto> MapJobIds(JsonNode payload)
        {
            return FindJsonArraysByName(payload, "job_ids")
                .SelectMany(array => array
                    .Select(node => node is JsonValue value && value.TryGetValue<string>(out var jobId) ? jobId : string.Empty)
                    .Where(jobId => !string.IsNullOrWhiteSpace(jobId))
                    .Select(jobId => new PeopleDiscoveryJobDto
                    {
                        JobId = jobId,
                        JobUrl = $"https://www.linkedin.com/jobs/view/{jobId}/",
                        Source = "LinkedIn Jobs"
                    }))
                .GroupBy(job => job.JobId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private static string ExtractContactInfo(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var contacts = new List<string>();
            AddMatches(contacts, text, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", value => value);
            AddMatches(
                contacts,
                text,
                @"(?:https?://)?(?:wa\.me|api\.whatsapp\.com|chat\.whatsapp\.com|t\.me|telegram\.me|mailto:)[^\s<>""]+",
                value => value.TrimEnd('.', ',', ';', ')'));
            AddMatches(
                contacts,
                text,
                @"(?:(?:whats(?:app)?|wpp|zap|telefone|phone|celular|contato|contact)\D{0,20})?(\+?\d[\d\s().-]{7,}\d)",
                value =>
                {
                    var digits = Regex.Replace(value, @"\D", "");
                    return digits.Length >= 10 && digits.Length <= 15 ? value.Trim() : string.Empty;
                });

            return string.Join(" | ", contacts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static void AddMatches(List<string> contacts, string text, string pattern, Func<string, string> clean)
        {
            foreach (Match match in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
            {
                var value = clean(match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : match.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    contacts.Add(value);
                }
            }
        }

        private static bool IsEmpty(PeopleDiscoveryPersonDto person)
        {
            return string.IsNullOrWhiteSpace(person.Name)
                && string.IsNullOrWhiteSpace(person.Title)
                && string.IsNullOrWhiteSpace(person.ProfileUrl);
        }

        private static bool IsLikelyPersonProfile(PeopleDiscoveryPersonDto person)
        {
            if (IsEmpty(person))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(person.ProfileUrl))
            {
                return !string.IsNullOrWhiteSpace(person.Name) && !string.IsNullOrWhiteSpace(person.Title);
            }

            return Regex.IsMatch(person.ProfileUrl, @"(^|linkedin\.com)/in/|/in/", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(person.ProfileUrl, @"/feed/update|/posts/|highlightedUpdateUrn|/jobs/", RegexOptions.IgnoreCase);
        }

        private static bool IsEmpty(PeopleDiscoveryJobDto job)
        {
            return string.IsNullOrWhiteSpace(job.Title)
                && string.IsNullOrWhiteSpace(job.Company)
                && string.IsNullOrWhiteSpace(job.JobUrl)
                && string.IsNullOrWhiteSpace(job.JobId);
        }

        private static string FirstString(JsonObject jsonObject, params string[] names)
        {
            foreach (var name in names)
            {
                if (!jsonObject.TryGetPropertyValue(name, out var node) || node is null)
                {
                    continue;
                }

                if (node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string TrimTo(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            var trimmed = value[..maxLength].Trim();
            var lastSpace = trimmed.LastIndexOf(' ');
            return lastSpace > 80 ? trimmed[..lastSpace] : trimmed;
        }

        private static string Normalize(string text)
        {
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static string Truncate(string text)
        {
            return text.Length <= DebugPreviewMaxLength
                ? text
                : text[..DebugPreviewMaxLength] + "\n...[truncated]";
        }

        private static JsonNode? TryGetProperty(JsonObject? jsonObject, string propertyName)
        {
            return jsonObject is not null && jsonObject.TryGetPropertyValue(propertyName, out var value) ? value : null;
        }

        private static JsonObject? TryGetObject(JsonObject? jsonObject, string propertyName)
        {
            return TryGetProperty(jsonObject, propertyName) as JsonObject;
        }

        private static T? TryGetValue<T>(JsonObject? jsonObject, string propertyName)
        {
            var node = TryGetProperty(jsonObject, propertyName);
            if (node is JsonValue value && value.TryGetValue<T>(out var result))
            {
                return result;
            }

            return default;
        }
    }
}
