using Lessie.Application.ResumeImprovements;
using Lessie.Application.Tools;

namespace Lessie.Infrastructure.ResumeImprovements;

internal sealed class ResumeAtsTool(IResumeAtsAnalyzer analyzer) : ITool
{
    public string Name => "resume.ats.analyze";

    public Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        var analysis = analyzer.Analyze(request.Query);
        return Task.FromResult(new ToolResult
        {
            Success = true,
            ToolName = Name,
            Summary = $"ATS score {analysis.OverallScore}/100.",
            Data = analysis
        });
    }
}
