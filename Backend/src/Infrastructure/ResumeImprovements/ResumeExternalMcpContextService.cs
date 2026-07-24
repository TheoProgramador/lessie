using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Lessie.Infrastructure.ResumeImprovements;

internal sealed class ResumeExternalMcpContextService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<ResumeExternalMcpContextService> logger,
    ILoggerFactory loggerFactory) : IResumeExternalMcpContextService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> BuildContextAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken)
    {
        var settings = ExternalResumeMcpSettings.FromConfiguration(configuration);
        var contexts = new List<string>();

        await AddContextAsync(contexts, "RChilli MCP Hub", () => BuildRChilliContextAsync(settings.RChilli, resumeText, jobDescription, cancellationToken));
        await AddContextAsync(contexts, "FormaCV MCP", () => BuildFormaCvContextAsync(settings.FormaCv, resumeText, jobDescription, cancellationToken));
        await AddContextAsync(contexts, "CV Forge MCP", () => BuildCvForgeContextAsync(settings.CvForge, jobDescription, cancellationToken));

        return contexts.Count == 0
            ? "Nenhum MCP externo opcional de curriculo retornou contexto nesta rodada."
            : string.Join("\n\n", contexts);
    }

    private async Task AddContextAsync(
        ICollection<string> contexts,
        string label,
        Func<Task<string>> buildContextAsync)
    {
        try
        {
            var context = await buildContextAsync();
            if (!string.IsNullOrWhiteSpace(context))
            {
                contexts.Add($"[{label}]\n{TrimTo(context, 4000)}");
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "{Label} did not return resume context.", label);
        }
    }

    private async Task<string> BuildCvForgeContextAsync(
        StdioMcpSettings settings,
        string jobDescription,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(jobDescription))
        {
            return string.Empty;
        }

        return await CallStdioToolAsync(
            settings,
            settings.PrimaryToolName,
            new Dictionary<string, object?>
            {
                ["jobTitle"] = "Vaga alvo",
                ["company"] = "Empresa alvo",
                ["jobDescription"] = TrimTo(jobDescription, settings.MaxInputCharacters)
            },
            cancellationToken);
    }

    private async Task<string> BuildFormaCvContextAsync(
        StdioMcpSettings settings,
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(resumeText) || string.IsNullOrWhiteSpace(jobDescription))
        {
            return string.Empty;
        }

        return await CallStdioToolAsync(
            settings,
            settings.PrimaryToolName,
            new Dictionary<string, object?>
            {
                ["cv"] = TrimTo(resumeText, settings.MaxInputCharacters),
                ["vacancy_description"] = TrimTo(jobDescription, settings.MaxInputCharacters),
                ["instructions"] = "Use somente informacoes factuais do curriculo. Priorize ATS, aderencia a vaga e clareza sem inventar experiencias.",
                ["options"] = new Dictionary<string, object?>
                {
                    ["bold_matches"] = true,
                    ["demote_irrelevant"] = true
                }
            },
            cancellationToken);
    }

    private async Task<string> CallStdioToolAsync(
        StdioMcpSettings settings,
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(settings.Timeout);

        await using var client = await CreateClientAsync(settings, timeoutCts.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeoutCts.Token);
        var tool = tools.FirstOrDefault(item => string.Equals(item.Name, toolName, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
        {
            return $"Ferramenta {toolName} nao encontrada. Ferramentas disponiveis: {string.Join(", ", tools.Select(item => item.Name))}";
        }

        var response = await client.CallToolAsync(tool.Name, arguments, cancellationToken: timeoutCts.Token);
        if (response.IsError == true)
        {
            return $"Ferramenta {tool.Name} retornou erro: {ExtractText(response)}";
        }

        return ExtractPayload(response);
    }

    private async Task<McpClient> CreateClientAsync(StdioMcpSettings settings, CancellationToken cancellationToken)
    {
        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        foreach (var pair in settings.Environment)
        {
            environment[pair.Key] = pair.Value;
        }

        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = settings.Name,
                Command = settings.Command,
                Arguments = settings.Arguments.ToArray(),
                WorkingDirectory = settings.ResolveWorkingDirectory(),
                EnvironmentVariables = environment,
                InheritEnvironmentVariables = true,
                ShutdownTimeout = TimeSpan.FromSeconds(5),
                StandardErrorLines = line => logger.LogInformation("{McpName} stderr: {Line}", settings.Name, line)
            },
            loggerFactory);

        return await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);
    }

    private async Task<string> BuildRChilliContextAsync(
        RChilliSettings settings,
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ServerUrl) || string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            return string.Empty;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(settings.Timeout);

        var sessionId = await InitializeRChilliSessionAsync(settings, timeoutCts.Token);
        var contexts = new List<string>();

        if (!string.IsNullOrWhiteSpace(resumeText))
        {
            var resumeResult = await CallRChilliToolAsync(
                settings,
                sessionId,
                settings.ParseResumeToolName,
                new JsonObject
                {
                    ["resume_text"] = TrimTo(resumeText, settings.MaxInputCharacters)
                },
                timeoutCts.Token);
            if (!string.IsNullOrWhiteSpace(resumeResult))
            {
                contexts.Add($"Resume parse: {resumeResult}");
            }
        }

        if (!string.IsNullOrWhiteSpace(jobDescription))
        {
            var jdResult = await CallRChilliToolAsync(
                settings,
                sessionId,
                settings.ParseJobDescriptionToolName,
                new JsonObject
                {
                    ["job_description"] = TrimTo(jobDescription, settings.MaxInputCharacters)
                },
                timeoutCts.Token);
            if (!string.IsNullOrWhiteSpace(jdResult))
            {
                contexts.Add($"JD parse: {jdResult}");
            }
        }

        if (!string.IsNullOrWhiteSpace(resumeText) && !string.IsNullOrWhiteSpace(jobDescription))
        {
            var scoreResult = await CallRChilliToolAsync(
                settings,
                sessionId,
                settings.ScoreResumeToolName,
                new JsonObject
                {
                    ["resume_text"] = TrimTo(resumeText, settings.MaxInputCharacters),
                    ["job_description"] = TrimTo(jobDescription, settings.MaxInputCharacters)
                },
                timeoutCts.Token);
            if (!string.IsNullOrWhiteSpace(scoreResult))
            {
                contexts.Add($"Resume/JD score: {scoreResult}");
            }
        }

        return string.Join("\n", contexts);
    }

    private async Task<string?> InitializeRChilliSessionAsync(RChilliSettings settings, CancellationToken cancellationToken)
    {
        var response = await SendRChilliJsonRpcAsync(
            settings,
            null,
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = "lessie-init",
                ["method"] = "initialize",
                ["params"] = new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject
                    {
                        ["name"] = "Lessie",
                        ["version"] = "1.0"
                    }
                }
            },
            cancellationToken);

        return response.SessionId;
    }

    private async Task<string> CallRChilliToolAsync(
        RChilliSettings settings,
        string? sessionId,
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        var response = await SendRChilliJsonRpcAsync(
            settings,
            sessionId,
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = $"lessie-{Guid.NewGuid():N}",
                ["method"] = "tools/call",
                ["params"] = new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments
                }
            },
            cancellationToken);

        return ExtractJsonRpcText(response.Payload);
    }

    private async Task<RChilliJsonRpcResponse> SendRChilliJsonRpcAsync(
        RChilliSettings settings,
        string? sessionId,
        JsonObject body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.ServerUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-06-18");
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);
        }

        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogDebug("RChilli MCP returned {StatusCode}: {Body}", response.StatusCode, TrimTo(error, 800));
            return new RChilliJsonRpcResponse(null, string.Empty);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var returnedSessionId = response.Headers.TryGetValues("Mcp-Session-Id", out var values)
            ? values.FirstOrDefault()
            : sessionId;

        return new RChilliJsonRpcResponse(returnedSessionId, ExtractSseJson(content));
    }

    private static string ExtractPayload(CallToolResult response)
    {
        if (response.StructuredContent is { } structuredContent)
        {
            var json = JsonSerializer.Serialize(structuredContent, JsonOptions);
            if (!string.IsNullOrWhiteSpace(json) && json != "{}")
            {
                return json;
            }
        }

        return ExtractText(response);
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

    private static string ExtractSseJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var dataLines = content
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["data:".Length..].Trim())
            .Where(line => line.StartsWith('{'))
            .ToArray();

        return dataLines.LastOrDefault() ?? content;
    }

    private static string ExtractJsonRpcText(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(payload);
            var result = node?["result"];
            if (result is null)
            {
                return TrimTo(payload, 3000);
            }

            if (result["content"] is JsonArray content)
            {
                var text = content
                    .OfType<JsonObject>()
                    .Select(item => item["text"]?.GetValue<string>())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text!;
                }
            }

            return result.ToJsonString(JsonOptions);
        }
        catch
        {
            return TrimTo(payload, 3000);
        }
    }

    private static string TrimTo(string value, int maxLength)
    {
        value = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        return value.Length <= maxLength ? value : value[..maxLength].Trim();
    }

    private sealed record RChilliJsonRpcResponse(string? SessionId, string Payload);

    private sealed class ExternalResumeMcpSettings
    {
        public RChilliSettings RChilli { get; private init; } = new();
        public StdioMcpSettings FormaCv { get; private init; } = new("formacv", "npx", ["-y", "@formacv/mcp"], "tailor_cv");
        public StdioMcpSettings CvForge { get; private init; } = new("cv-forge", "npx", ["-y", "cv-forge"], "parse_job_requirements");

        public static ExternalResumeMcpSettings FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("ResumeImprovements:ExternalMcp");
            return new ExternalResumeMcpSettings
            {
                RChilli = RChilliSettings.FromConfiguration(section.GetSection("RChilli")),
                FormaCv = StdioMcpSettings.FromConfiguration(
                    section.GetSection("FormaCV"),
                    new StdioMcpSettings("formacv", "npx", ["-y", "@formacv/mcp"], "tailor_cv")),
                CvForge = StdioMcpSettings.FromConfiguration(
                    section.GetSection("CvForge"),
                    new StdioMcpSettings("cv-forge", "npx", ["-y", "cv-forge"], "parse_job_requirements"))
            };
        }
    }

    private sealed class RChilliSettings
    {
        public bool Enabled { get; private init; }
        public string ServerUrl { get; private init; } = "https://mcp.rchilli.ai/mcp";
        public string AccessToken { get; private init; } = string.Empty;
        public int TimeoutSeconds { get; private init; } = 45;
        public int MaxInputCharacters { get; private init; } = 16000;
        public string ParseResumeToolName { get; private init; } = "parse_resume";
        public string ParseJobDescriptionToolName { get; private init; } = "parse_job_description";
        public string ScoreResumeToolName { get; private init; } = "score_resume_against_jd";

        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));

        public static RChilliSettings FromConfiguration(IConfigurationSection section)
        {
            return new RChilliSettings
            {
                Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                ServerUrl = section["ServerUrl"] ?? "https://mcp.rchilli.ai/mcp",
                AccessToken = section["AccessToken"] ?? string.Empty,
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 45,
                MaxInputCharacters = int.TryParse(section["MaxInputCharacters"], out var maxInputCharacters)
                    ? Math.Clamp(maxInputCharacters, 2000, 40000)
                    : 16000,
                ParseResumeToolName = section["ParseResumeToolName"] ?? "parse_resume",
                ParseJobDescriptionToolName = section["ParseJobDescriptionToolName"] ?? "parse_job_description",
                ScoreResumeToolName = section["ScoreResumeToolName"] ?? "score_resume_against_jd"
            };
        }
    }

    private sealed class StdioMcpSettings(
        string name,
        string command,
        IReadOnlyCollection<string> arguments,
        string primaryToolName)
    {
        public bool Enabled { get; private init; }
        public string Name { get; private init; } = name;
        public string Command { get; private init; } = command;
        public IReadOnlyCollection<string> Arguments { get; private init; } = arguments;
        public string? WorkingDirectory { get; private init; }
        public string PrimaryToolName { get; private init; } = primaryToolName;
        public int TimeoutSeconds { get; private init; } = 60;
        public int MaxInputCharacters { get; private init; } = 16000;
        public IReadOnlyDictionary<string, string> Environment { get; private init; } = new Dictionary<string, string>();

        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));

        public static StdioMcpSettings FromConfiguration(IConfigurationSection section, StdioMcpSettings defaults)
        {
            return new StdioMcpSettings(
                section["Name"] ?? defaults.Name,
                string.IsNullOrWhiteSpace(section["Command"]) ? defaults.Command : section["Command"]!,
                section.GetSection("Arguments").Get<string[]>() ?? defaults.Arguments,
                section["PrimaryToolName"] ?? defaults.PrimaryToolName)
            {
                Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
                WorkingDirectory = section["WorkingDirectory"] ?? defaults.WorkingDirectory,
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : defaults.TimeoutSeconds,
                MaxInputCharacters = int.TryParse(section["MaxInputCharacters"], out var maxInputCharacters)
                    ? Math.Clamp(maxInputCharacters, 2000, 40000)
                    : defaults.MaxInputCharacters,
                Environment = section.GetSection("Environment")
                    .GetChildren()
                    .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                    .ToDictionary(item => item.Key, item => item.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)
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
    }
}
