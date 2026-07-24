namespace Lessie.Domain.Entities;

public sealed class ResumeImprovementSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ResumeFileName { get; set; } = string.Empty;
    public string JobContextSummary { get; set; } = string.Empty;
    public string ChatSummary { get; set; } = string.Empty;
    public string CurrentOptimizedResume { get; set; } = string.Empty;
    public string AtsAnalysisJson { get; set; } = "{}";
    public string CanonicalResumeJson { get; set; } = "{}";
    public string LinkedInProfileUrl { get; set; } = string.Empty;
    public string GitHubProfileUrl { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;
    public bool ReadyToExport { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastMessageAt { get; set; }

    public User? User { get; set; }
    public ICollection<ResumeImprovementMessage> Messages { get; set; } = [];
    public ICollection<ResumeImprovementDocumentChunk> DocumentChunks { get; set; } = [];
}
