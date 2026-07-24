namespace Lessie.Application.ResumeImprovements;

public interface IResumeAtsAnalyzer
{
    ResumeAtsAnalysis Analyze(string resumeText, string jobContext = "");
}

public sealed class ResumeAtsAnalysis
{
    public string Provider { get; set; } = "ATS Resume MCP";
    public int OverallScore { get; set; }
    public DateTimeOffset AnalyzedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ResumeAtsSectionScore> Sections { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<string> KeywordsPresent { get; set; } = new();
    public List<string> KeywordsMissing { get; set; } = new();
    public List<string> KeywordsPartial { get; set; } = new();
    public List<string> JobSearchKeywords { get; set; } = new();
    public List<string> CriticalGaps { get; set; } = new();
    public string MatchRecommendation { get; set; } = string.Empty;
    public Dictionary<string, int> Subscores { get; set; } = new();
    public List<ResumeAtsRequirementCoverage> RequirementCoverage { get; set; } = new();
    public List<ResumeAtsKeywordStrategy> KeywordStrategy { get; set; } = new();
    public string CanonicalResumeJson { get; set; } = "{}";
}

public sealed class ResumeAtsSectionScore
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
}

public sealed class ResumeAtsRequirementCoverage
{
    public string Requirement { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public sealed class ResumeAtsKeywordStrategy
{
    public string Group { get; set; } = string.Empty;
    public string TargetSection { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public string Instruction { get; set; } = string.Empty;
}
