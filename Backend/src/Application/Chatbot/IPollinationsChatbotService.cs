namespace Lessie.Application.Chatbot;

public interface IPollinationsChatbotService
{
    Task<ChatbotMessageResponse> SendMessageAsync(Guid userId, ChatbotMessageRequest request, CancellationToken cancellationToken);
}
