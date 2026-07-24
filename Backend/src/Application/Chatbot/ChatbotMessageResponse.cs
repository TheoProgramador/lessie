namespace Lessie.Application.Chatbot;

public sealed class ChatbotMessageResponse
{
    public string Message { get; set; } = string.Empty;
    public object? ToolResult { get; set; }
}
