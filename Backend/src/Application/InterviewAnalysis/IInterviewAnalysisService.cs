namespace Lessie.Application.InterviewAnalysis;

public interface IInterviewAnalysisService
{
    Task<InterviewAnalysisResponse> AnalyzeAsync(
        Guid userId,
        InterviewAudioInput audio,
        InterviewAnalysisRequest request,
        CancellationToken cancellationToken);
}
