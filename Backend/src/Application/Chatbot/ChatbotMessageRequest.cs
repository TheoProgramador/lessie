namespace Lessie.Application.Chatbot;

public sealed class ChatbotMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto> History { get; set; } = new();
}
