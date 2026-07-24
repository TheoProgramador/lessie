namespace Lessie.Application.InterviewAnalysis;

public sealed record InterviewAudioInput(
    string FileName,
    string ContentType,
    byte[] Content);
