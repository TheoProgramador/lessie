using Lessie.Application.Chatbot;

namespace Lessie.Application.ResumeImprovements;

public interface IResumeImprovementService
{
    Task<ResumeImprovementAnalyzeResponse> AnalyzeAsync(
        Guid userId,
        ResumeFileInput resume,
        IReadOnlyCollection<ResumeFileInput> jobScreenshots,
        ResumeImprovementAdditionalContext additionalContext,
        CancellationToken cancellationToken);

    Task<ResumeImprovementChatResponse> ChatAsync(
        Guid userId,
        ResumeImprovementChatRequest request,
        CancellationToken cancellationToken);

    Task<ResumeImprovementChatResponse> OptimizeForJobAsync(
        Guid userId,
        Guid sessionId,
        IReadOnlyCollection<ResumeFileInput> jobScreenshots,
        ResumeImprovementProfileLinksRequest profileLinks,
        bool forkFromSession,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ResumeImprovementHistoryItem>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<ResumeImprovementSessionDetail?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ResumeImprovementSaveResponse> SaveOptimizedResumeAsync(
        Guid userId,
        Guid sessionId,
        ResumeImprovementSaveRequest request,
        CancellationToken cancellationToken);

    Task<ResumeImprovementRenameResponse> RenameSessionAsync(
        Guid userId,
        Guid sessionId,
        ResumeImprovementRenameRequest request,
        CancellationToken cancellationToken);

    Task<ResumeImprovementProfileLinksResponse> UpdateProfileLinksAsync(
        Guid userId,
        Guid sessionId,
        ResumeImprovementProfileLinksRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    ResumeExportResult Export(ResumeExportRequest request);
}

public sealed record ResumeFileInput(string FileName, string ContentType, byte[] Content);

public sealed class ResumeImprovementAdditionalContext
{
    public ResumeFileInput? LinkedInProfile { get; set; }
    public string LinkedInProfileUrl { get; set; } = string.Empty;
    public string GitHubProfileUrl { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;
    public string PersonalInfo { get; set; } = string.Empty;
    public string CustomInstructions { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
}

public sealed class ResumeImprovementAnalyzeResponse
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ResumeText { get; set; } = string.Empty;
    public string JobContext { get; set; } = string.Empty;
    public string OptimizedResume { get; set; } = string.Empty;
    public bool ReadyToExport { get; set; }
    public ResumeAtsAnalysis? AtsAnalysis { get; set; }
}

public sealed class ResumeImprovementChatRequest
{
    public Guid? SessionId { get; set; }
    public bool ForkFromSession { get; set; }
    public string ResumeText { get; set; } = string.Empty;
    public string JobContext { get; set; } = string.Empty;
    public string OptimizedResume { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string LinkedInProfileUrl { get; set; } = string.Empty;
    public string GitHubProfileUrl { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;
    public List<ChatMessageDto> History { get; set; } = new();
}

public sealed class ResumeImprovementChatResponse
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SentPayloadPreview { get; set; } = string.Empty;
    public string OptimizedResume { get; set; } = string.Empty;
    public bool ReadyToExport { get; set; }
    public ResumeAtsAnalysis? AtsAnalysis { get; set; }
}

public sealed class ResumeImprovementHistoryItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ResumeFileName { get; set; } = string.Empty;
    public bool ReadyToExport { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ResumeImprovementSessionDetail
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ResumeFileName { get; set; } = string.Empty;
    public string JobContext { get; set; } = string.Empty;
    public string OptimizedResume { get; set; } = string.Empty;
    public bool ReadyToExport { get; set; }
    public bool HasResumeContext { get; set; }
    public string LinkedInProfileUrl { get; set; } = string.Empty;
    public string GitHubProfileUrl { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;
    public ResumeAtsAnalysis? AtsAnalysis { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public sealed class ResumeImprovementSaveRequest
{
    public string OptimizedResume { get; set; } = string.Empty;
    public bool ForkFromSession { get; set; }
}

public sealed class ResumeImprovementSaveResponse
{
    public Guid SessionId { get; set; }
    public string OptimizedResume { get; set; } = string.Empty;
    public bool ReadyToExport { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ResumeAtsAnalysis? AtsAnalysis { get; set; }
}

public sealed class ResumeImprovementRenameRequest
{
    public string Title { get; set; } = string.Empty;
}

public sealed class ResumeImprovementRenameResponse
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ResumeImprovementProfileLinksRequest
{
    public string LinkedInProfileUrl { get; set; } = string.Empty;
    public string GitHubProfileUrl { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;
}

public sealed class ResumeImprovementProfileLinksResponse
{
    public Guid SessionId { get; set; }
    public string LinkedInProfileUrl { get; set; } = string.Empty;
    public string GitHubProfileUrl { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ResumeExportRequest
{
    public string Content { get; set; } = string.Empty;
    public string Format { get; set; } = "docx";
}

public sealed record ResumeExportResult(byte[] Content, string ContentType, string FileName);
