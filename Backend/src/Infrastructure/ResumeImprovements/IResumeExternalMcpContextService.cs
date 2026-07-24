namespace Lessie.Infrastructure.ResumeImprovements;

internal interface IResumeExternalMcpContextService
{
    Task<string> BuildContextAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken);
}
