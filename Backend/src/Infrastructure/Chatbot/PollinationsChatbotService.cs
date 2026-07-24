using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lessie.Application.Chatbot;
using Lessie.Application.ProviderKeys;
using Microsoft.Extensions.Configuration;

namespace Lessie.Infrastructure.Chatbot;

internal sealed class PollinationsChatbotService(
    HttpClient httpClient,
    IConfiguration configuration,
    IProviderKeyService providerKeyService) : IPollinationsChatbotService
{
    private const string SystemPrompt = """
        Voce e o chat Pollinations do Lessie.

        Responda sempre em portugues do Brasil, de forma clara, pratica e concisa.
        Se o usuario pedir outro idioma explicitamente, ainda assim priorize portugues do Brasil,
        a menos que a traducao ou comparacao entre idiomas seja o objetivo da pergunta.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ChatbotMessageResponse> SendMessageAsync(Guid userId, ChatbotMessageRequest request, CancellationToken cancellationToken)
    {
        var apiKey = await providerKeyService.GetActivePollinationsKeyAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderKeyMissingException("Configure o token Pollinations antes de enviar mensagens.");
        }

        var model = configuration["POLLINATIONS_MODEL"] ?? configuration["Pollinations:Model"] ?? "gpt-5.4";
        var messages = BuildMessages(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(new PollinationsChatCompletionRequest(
            model,
            messages,
            0.3m), options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new PollinationsAuthenticationException("Pollinations recusou o token informado.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new PollinationsProviderException("Pollinations falhou ao processar a mensagem.");
        }

        var completion = await response.Content.ReadFromJsonAsync<PollinationsChatCompletionResponse>(JsonOptions, cancellationToken);
        var message = completion?.Choices.FirstOrDefault()?.Message.Content;

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new PollinationsProviderException("Pollinations retornou uma resposta vazia.");
        }

        await providerKeyService.MarkPollinationsKeyUsedAsync(userId, cancellationToken);

        return new ChatbotMessageResponse
        {
            Message = message
        };
    }

    private static List<PollinationsMessage> BuildMessages(ChatbotMessageRequest request)
    {
        var messages = new List<PollinationsMessage>
        {
            new("system", SystemPrompt)
        };

        messages.AddRange(request.History.Select(message => new PollinationsMessage(message.Role, message.Content)));
        messages.Add(new PollinationsMessage("user", request.Message));

        return messages;
    }

    private sealed record PollinationsChatCompletionRequest(
        string Model,
        IReadOnlyCollection<PollinationsMessage> Messages,
        decimal Temperature);

    private sealed record PollinationsMessage(string Role, string Content);

    private sealed class PollinationsChatCompletionResponse
    {
        public List<PollinationsChoice> Choices { get; set; } = new();
    }

    private sealed class PollinationsChoice
    {
        public PollinationsChoiceMessage Message { get; set; } = new();
    }

    private sealed class PollinationsChoiceMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
