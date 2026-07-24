namespace Lessie.Application.Chatbot;

public interface IChatbotService
{
    Task<ChatbotMessageResponse> SendMessageAsync(Guid userId, ChatbotMessageRequest request, CancellationToken cancellationToken);
}
