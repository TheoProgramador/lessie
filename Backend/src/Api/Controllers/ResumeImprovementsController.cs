using Lessie.Api.Http;
using Lessie.Application.Chatbot;
using Lessie.Application.ResumeImprovements;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/resume-improvements")]
public sealed class ResumeImprovementsController(
    IResumeImprovementService resumeImprovementService,
    LessieDbContext dbContext) : ControllerBase
{
    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await resumeImprovementService.GetHistoryAsync(userId, cancellationToken));
    }

    [HttpGet("history/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var session = await resumeImprovementService.GetSessionAsync(userId, sessionId, cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpDelete("history/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var deleted = await resumeImprovementService.DeleteSessionAsync(userId, sessionId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("history/{sessionId:guid}/optimized-resume")]
    public async Task<IActionResult> SaveOptimizedResumeAsync(
        Guid sessionId,
        ResumeImprovementSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await resumeImprovementService.SaveOptimizedResumeAsync(userId, sessionId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("history/{sessionId:guid}/title")]
    public async Task<IActionResult> RenameSessionAsync(
        Guid sessionId,
        ResumeImprovementRenameRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await resumeImprovementService.RenameSessionAsync(userId, sessionId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("history/{sessionId:guid}/profile-links")]
    public async Task<IActionResult> UpdateProfileLinksAsync(
        Guid sessionId,
        [FromBody] ResumeImprovementProfileLinksRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await resumeImprovementService.UpdateProfileLinksAsync(userId, sessionId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("analyze")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> AnalyzeAsync(
        [FromForm] IFormFile resume,
        [FromForm] List<IFormFile>? jobScreenshots,
        [FromForm] IFormFile? linkedinProfile,
        [FromForm] string? linkedinProfileUrl,
        [FromForm] string? githubProfileUrl,
        [FromForm] string? portfolioUrl,
        [FromForm] string? personalInfo,
        [FromForm] string? customInstructions,
        [FromForm] string? jobDescription,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (resume is null || resume.Length == 0)
        {
            return BadRequest(new { message = "Envie um curriculo em PDF ou DOCX." });
        }

        try
        {
            var quotaResult = await EnsureResumeAnalysisQuotaAsync(userId, cancellationToken);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var response = await resumeImprovementService.AnalyzeAsync(
                userId,
                await ReadFileAsync(resume, cancellationToken),
                await ReadFilesAsync(jobScreenshots ?? [], cancellationToken),
                new ResumeImprovementAdditionalContext
                {
                    LinkedInProfile = linkedinProfile is { Length: > 0 }
                        ? await ReadFileAsync(linkedinProfile, cancellationToken)
                        : null,
                    LinkedInProfileUrl = linkedinProfileUrl ?? string.Empty,
                    GitHubProfileUrl = githubProfileUrl ?? string.Empty,
                    PortfolioUrl = portfolioUrl ?? string.Empty,
                    PersonalInfo = personalInfo ?? string.Empty,
                    CustomInstructions = customInstructions ?? string.Empty,
                    JobDescription = jobDescription ?? string.Empty
                },
                cancellationToken);

            await IncrementResumeAnalysisCountAsync(userId, cancellationToken);
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
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (PollinationsProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [HttpPost("chat")]
    public async Task<IActionResult> ChatAsync(ResumeImprovementChatRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await resumeImprovementService.ChatAsync(userId, request, cancellationToken);
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
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (PollinationsProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [HttpPost("history/{sessionId:guid}/job-screenshots")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> OptimizeForJobAsync(
        Guid sessionId,
        [FromForm] List<IFormFile>? jobScreenshots,
        [FromForm] bool forkFromSession,
        [FromForm] string? linkedinProfileUrl,
        [FromForm] string? githubProfileUrl,
        [FromForm] string? portfolioUrl,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await resumeImprovementService.OptimizeForJobAsync(
                userId,
                sessionId,
                await ReadFilesAsync(jobScreenshots ?? [], cancellationToken),
                new ResumeImprovementProfileLinksRequest
                {
                    LinkedInProfileUrl = linkedinProfileUrl ?? string.Empty,
                    GitHubProfileUrl = githubProfileUrl ?? string.Empty,
                    PortfolioUrl = portfolioUrl ?? string.Empty
                },
                forkFromSession,
                cancellationToken);

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
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (PollinationsProviderException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [HttpPost("export")]
    public IActionResult Export(ResumeExportRequest request)
    {
        var result = resumeImprovementService.Export(request);
        return File(result.Content, result.ContentType, result.FileName);
    }

    private static async Task<ResumeFileInput> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return new ResumeFileInput(file.FileName, file.ContentType, stream.ToArray());
    }

    private static async Task<IReadOnlyCollection<ResumeFileInput>> ReadFilesAsync(
        IReadOnlyCollection<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var result = new List<ResumeFileInput>();
        foreach (var file in files.Where(file => file.Length > 0).Take(4))
        {
            result.Add(await ReadFileAsync(file, cancellationToken));
        }

        return result;
    }

    private async Task<IActionResult?> EnsureResumeAnalysisQuotaAsync(Guid userId, CancellationToken cancellationToken)
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
                ResumeAnalysisCount = 0,
                ResumeAnalysisLimit = 20,
                ChatConversationLimit = 50,
                InterviewAnalysisLimit = 5,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.UserSubscriptions.Add(user.Subscription);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (user.IsAdmin)
        {
            return null;
        }

        if (user.Subscription.ResumeAnalysisCount < user.Subscription.ResumeAnalysisLimit)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = $"Limite de analises de curriculo atingido ({user.Subscription.ResumeAnalysisCount}/{user.Subscription.ResumeAnalysisLimit}). As buscas continuam liberadas.",
            resumeAnalysisCount = user.Subscription.ResumeAnalysisCount,
            resumeAnalysisLimit = user.Subscription.ResumeAnalysisLimit
        });
    }

    private async Task IncrementResumeAnalysisCountAsync(Guid userId, CancellationToken cancellationToken)
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

        user.Subscription.ResumeAnalysisCount += 1;
        user.Subscription.UpdatedAt = now;
        user.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

}
