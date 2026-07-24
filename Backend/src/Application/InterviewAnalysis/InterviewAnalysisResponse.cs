namespace Lessie.Application.InterviewAnalysis;

public sealed class InterviewAnalysisResponse
{
    public string Warning { get; set; } = InterviewAnalysisWarnings.Estimate;
    public string TranscriptionModel { get; set; } = string.Empty;
    public string AnalysisModel { get; set; } = string.Empty;
    public decimal EstimatedGroqCostUsd { get; set; }
    public decimal EstimatedGroqCostBrl { get; set; }
    public double DurationSeconds { get; set; }
    public string TranscriptText { get; set; } = string.Empty;
    public IReadOnlyCollection<InterviewTranscriptSegment> Segments { get; set; } = [];
    public string Analysis { get; set; } = string.Empty;
}

public sealed class InterviewTranscriptSegment
{
    public double Start { get; set; }
    public double End { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public decimal? AverageLogProbability { get; set; }
    public decimal? NoSpeechProbability { get; set; }
    public decimal? CompressionRatio { get; set; }
}

public static class InterviewAnalysisWarnings
{
    public const string Estimate = "ATENCAO: esta analise e uma ESTIMATIVA gerada por IA. A IA pode cometer erros, interpretar mal falas, ignorar contexto, transcrever trechos incorretamente e nao substitui a avaliacao humana do entrevistador. Nada aqui e certeza, diagnostico psicologico ou decisao final de contratacao; use apenas como apoio para melhoria de entrevista.";
}
