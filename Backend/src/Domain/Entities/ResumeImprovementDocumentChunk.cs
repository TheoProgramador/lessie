namespace Lessie.Domain.Entities;

public sealed class ResumeImprovementDocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResumeImprovementSessionId { get; set; }
    public string Source { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ResumeImprovementSession? Session { get; set; }
}
