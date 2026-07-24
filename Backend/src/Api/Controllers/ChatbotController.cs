using Lessie.Api.Http;
using Lessie.Application.Chatbot;
using Lessie.Application.ProviderKeys;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/chatbot")]
public sealed class ChatbotController(
    IChatbotService chatbotService,
    IPollinationsChatbotService pollinationsChatbotService,
    LessieDbContext dbContext) : ControllerBase
{
    [HttpPost("message")]
    public async Task<IActionResult> SendMessageAsync(ChatbotMessageRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        try
        {
            var response = await chatbotService.SendMessageAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (ProviderKeyMissingException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (GroqAuthenticationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (GroqProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [HttpPost("pollinations/message")]
    public async Task<IActionResult> SendPollinationsMessageAsync(ChatbotMessageRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        try
        {
            var quotaResult = await EnsureChatQuotaAsync(userId, cancellationToken);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var response = await pollinationsChatbotService.SendMessageAsync(userId, request, cancellationToken);
            await IncrementChatConversationCountAsync(userId, cancellationToken);
            return Ok(response);
        }
        catch (ProviderKeyMissingException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (PollinationsAuthenticationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (PollinationsProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    private static string? ValidateRequest(ChatbotMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return "Mensagem obrigatoria.";
        }

        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "user",
            "assistant",
            "system"
        };

        if (request.History.Any(message =>
                string.IsNullOrWhiteSpace(message.Role)
                || string.IsNullOrWhiteSpace(message.Content)
                || !allowedRoles.Contains(message.Role)))
        {
            return "Historico possui mensagens invalidas.";
        }

        foreach (var message in request.History)
        {
            message.Role = message.Role.Trim().ToLowerInvariant();
            message.Content = message.Content.Trim();
        }

        request.Message = request.Message.Trim();

        return null;
    }

    private async Task<IActionResult?> EnsureChatQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        if (user.Subscription is null)
        {
            var now = DateTimeOffset.UtcNow;
            user.Subscription = new UserSubscription
            {
                UserId = user.Id,
                ResumeAnalysisLimit = 20,
                ChatConversationLimit = 50,
                InterviewAnalysisLimit = 5,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.UserSubscriptions.Add(user.Subscription);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (user.IsAdmin || user.Subscription.ChatConversationCount < user.Subscription.ChatConversationLimit)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = $"Limite de conversas de chat atingido ({user.Subscription.ChatConversationCount}/{user.Subscription.ChatConversationLimit}). As buscas continuam liberadas.",
            chatConversationCount = user.Subscription.ChatConversationCount,
            chatConversationLimit = user.Subscription.ChatConversationLimit
        });
    }

    private async Task IncrementChatConversationCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        if (user is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (user.Subscription is null)
        {
            user.Subscription = new UserSubscription
            {
                UserId = user.Id,
                ResumeAnalysisLimit = 20,
                ChatConversationLimit = 50,
                InterviewAnalysisLimit = 5,
                CreatedAt = now
            };
            dbContext.UserSubscriptions.Add(user.Subscription);
        }

        user.Subscription.ChatConversationCount += 1;
        user.Subscription.UpdatedAt = now;
        user.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
