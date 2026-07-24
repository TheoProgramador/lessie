namespace Lessie.Application.InterviewAnalysis;

public sealed class InterviewAnalysisRequest
{
    public string CandidateName { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string InterviewContext { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public string CustomInstructions { get; set; } = string.Empty;
}
