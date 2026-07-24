using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lessie.Application.Chatbot;
using Lessie.Application.ProviderKeys;
using Lessie.Application.Tools;
using Microsoft.Extensions.Configuration;

namespace Lessie.Infrastructure.Chatbot;

internal sealed class GroqChatbotService(
    HttpClient httpClient,
    IConfiguration configuration,
    IProviderKeyService providerKeyService,
    IToolRegistry toolRegistry) : IChatbotService
{
    private const string SystemPrompt = """
        You are the first AI orchestrator of Lessie Clone.

        For now, you only answer as a normal chatbot.

        In the future, you will orchestrate MCP tools for people discovery, company discovery, opportunity discovery and lead enrichment.

        Be concise, useful and practical.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ChatbotMessageResponse> SendMessageAsync(Guid userId, ChatbotMessageRequest request, CancellationToken cancellationToken)
    {
        var apiKey = await providerKeyService.GetActiveGroqKeyAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderKeyMissingException("Configure a API Key da Groq antes de enviar mensagens.");
        }

        var model = configuration["GROQ_MODEL"] ?? configuration["Groq:Model"] ?? "openai/gpt-oss-120b";
        var toolResult = await TryExecutePeopleSearchAsync(userId, request.Message, cancellationToken);
        var messages = BuildMessages(request, toolResult);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "openai/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(new GroqChatCompletionRequest(
            model,
            messages,
            0.3m), options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new GroqAuthenticationException("Groq recusou a API Key informada.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new GroqProviderException("Groq falhou ao processar a mensagem.");
        }

        var completion = await response.Content.ReadFromJsonAsync<GroqChatCompletionResponse>(JsonOptions, cancellationToken);
        var message = completion?.Choices.FirstOrDefault()?.Message.Content;

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new GroqProviderException("Groq retornou uma resposta vazia.");
        }

        await providerKeyService.MarkGroqKeyUsedAsync(userId, cancellationToken);

        return new ChatbotMessageResponse
        {
            Message = message,
            ToolResult = toolResult
        };
    }

    private static List<GroqMessage> BuildMessages(ChatbotMessageRequest request, ToolResult? toolResult)
    {
        var messages = new List<GroqMessage>
        {
            new("system", SystemPrompt)
        };

        messages.AddRange(request.History.Select(message => new GroqMessage(message.Role, message.Content)));

        if (toolResult is not null)
        {
            messages.Add(new GroqMessage("system", $"""
                Internal tool result from {toolResult.ToolName}:
                {JsonSerializer.Serialize(toolResult, JsonOptions)}

                Use this tool result to answer the user's people discovery request in a friendly and practical way.
                If the tool failed, explain the failure without inventing real profiles.
                """));
        }

        messages.Add(new GroqMessage("user", request.Message));

        return messages;
    }

    private async Task<ToolResult?> TryExecutePeopleSearchAsync(Guid userId, string message, CancellationToken cancellationToken)
    {
        if (!IsPeopleSearchIntent(message))
        {
            return null;
        }

        return await toolRegistry.ExecuteAsync(
            "people.search",
            new ToolRequest
            {
                Query = ExtractPeopleSearchQuery(message),
                UserId = userId.ToString()
            },
            cancellationToken);
    }

    private static bool IsPeopleSearchIntent(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("encontre recrutadores")
            || normalized.Contains("buscar recrutadores")
            || normalized.Contains("busque recrutadores")
            || normalized.Contains("procurar pessoas")
            || normalized.Contains("procure pessoas")
            || normalized.Contains("encontrar pessoas")
            || normalized.Contains("encontre pessoas")
            || normalized.Contains("buscar pessoas")
            || normalized.Contains("buscar profissionais")
            || normalized.Contains("procure profissionais")
            || normalized.Contains("pessoas relacionadas");
    }

    private static string ExtractPeopleSearchQuery(string message)
    {
        return message
            .Replace("Encontre", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Procure", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Buscar", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Busque", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private sealed record GroqChatCompletionRequest(
        string Model,
        IReadOnlyCollection<GroqMessage> Messages,
        decimal Temperature);

    private sealed record GroqMessage(string Role, string Content);

    private sealed class GroqChatCompletionResponse
    {
        public List<GroqChoice> Choices { get; set; } = new();
    }

    private sealed class GroqChoice
    {
        public GroqChoiceMessage Message { get; set; } = new();
    }

    private sealed class GroqChoiceMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
