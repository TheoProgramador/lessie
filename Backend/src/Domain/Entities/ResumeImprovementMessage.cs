namespace Lessie.Domain.Entities;

public sealed class ResumeImprovementMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResumeImprovementSessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string CompactContent { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ResumeImprovementSession? Session { get; set; }
}
