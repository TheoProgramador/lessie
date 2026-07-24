using Lessie.Api.Http;
using Lessie.Application.Chatbot;
using Lessie.Application.InterviewAnalysis;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/interview-analysis")]
public sealed class InterviewAnalysisController(
    IInterviewAnalysisService interviewAnalysisService,
    LessieDbContext dbContext) : ControllerBase
{
    [HttpPost("analyze")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> AnalyzeAsync(
        [FromForm] IFormFile audio,
        [FromForm] string? candidateName,
        [FromForm] string? roleTitle,
        [FromForm] string? companyName,
        [FromForm] string? interviewContext,
        [FromForm] string? jobDescription,
        [FromForm] string? customInstructions,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (audio is null || audio.Length == 0)
        {
            return BadRequest(new { message = "Envie um arquivo de audio ou video para analisar." });
        }

        try
        {
            var quotaResult = await EnsureInterviewAnalysisQuotaAsync(userId, cancellationToken);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var response = await interviewAnalysisService.AnalyzeAsync(
                userId,
                await ReadFileAsync(audio, cancellationToken),
                new InterviewAnalysisRequest
                {
                    CandidateName = candidateName ?? string.Empty,
                    RoleTitle = roleTitle ?? string.Empty,
                    CompanyName = companyName ?? string.Empty,
                    InterviewContext = interviewContext ?? string.Empty,
                    JobDescription = jobDescription ?? string.Empty,
                    CustomInstructions = customInstructions ?? string.Empty
                },
                cancellationToken);

            await IncrementInterviewAnalysisCountAsync(userId, cancellationToken);
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
        catch (PollinationsAuthenticationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (GroqProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (PollinationsProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    private static async Task<InterviewAudioInput> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return new InterviewAudioInput(file.FileName, file.ContentType, stream.ToArray());
    }

    private async Task<IActionResult?> EnsureInterviewAnalysisQuotaAsync(Guid userId, CancellationToken cancellationToken)
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

        if (user.IsAdmin || user.Subscription.InterviewAnalysisCount < user.Subscription.InterviewAnalysisLimit)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = $"Limite de analises de entrevista atingido ({user.Subscription.InterviewAnalysisCount}/{user.Subscription.InterviewAnalysisLimit}).",
            interviewAnalysisCount = user.Subscription.InterviewAnalysisCount,
            interviewAnalysisLimit = user.Subscription.InterviewAnalysisLimit
        });
    }

    private async Task IncrementInterviewAnalysisCountAsync(Guid userId, CancellationToken cancellationToken)
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

        user.Subscription.InterviewAnalysisCount += 1;
        user.Subscription.UpdatedAt = now;
        user.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
