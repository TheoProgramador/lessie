using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lessie.Application.Chatbot;
using Lessie.Application.InterviewAnalysis;
using Lessie.Application.ProviderKeys;
using Microsoft.Extensions.Configuration;

namespace Lessie.Infrastructure.InterviewAnalysis;

internal sealed class InterviewAnalysisService(
    HttpClient httpClient,
    IConfiguration configuration,
    IProviderKeyService providerKeyService) : IInterviewAnalysisService
{
    private const string GroqTranscriptionModel = "whisper-large-v3-turbo";
    private const decimal GroqTurboUsdPerHour = 0.04m;
    private const int MaxTranscriptCharactersForAnalysis = 120_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InterviewAnalysisResponse> AnalyzeAsync(
        Guid userId,
        InterviewAudioInput audio,
        InterviewAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var groqApiKey = await providerKeyService.GetActiveGroqKeyAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(groqApiKey))
        {
            throw new ProviderKeyMissingException("Configure a API Key da Groq antes de transcrever entrevistas.");
        }

        var pollinationsToken = await providerKeyService.GetActivePollinationsKeyAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(pollinationsToken))
        {
            throw new ProviderKeyMissingException("Configure o token Pollinations antes de analisar entrevistas.");
        }

        var transcription = await TranscribeAsync(audio, groqApiKey, request, cancellationToken);
        await providerKeyService.MarkGroqKeyUsedAsync(userId, cancellationToken);

        var analysisModel = configuration["POLLINATIONS_MODEL"] ?? configuration["Pollinations:Model"] ?? "gpt-5.4";
        var analysis = await AnalyzeTranscriptAsync(transcription, request, pollinationsToken, analysisModel, cancellationToken);
        await providerKeyService.MarkPollinationsKeyUsedAsync(userId, cancellationToken);

        var durationSeconds = transcription.DurationSeconds > 0
            ? transcription.DurationSeconds
            : transcription.Segments.LastOrDefault()?.End ?? 0;
        var estimatedUsd = Math.Round((decimal)durationSeconds / 3600m * GroqTurboUsdPerHour, 6);
        var usdBrlRate = GetUsdBrlRate();

        return new InterviewAnalysisResponse
        {
            TranscriptionModel = GroqTranscriptionModel,
            AnalysisModel = analysisModel,
            DurationSeconds = durationSeconds,
            TranscriptText = transcription.Text,
            Segments = transcription.Segments,
            Analysis = analysis,
            EstimatedGroqCostUsd = estimatedUsd,
            EstimatedGroqCostBrl = Math.Round(estimatedUsd * usdBrlRate, 4)
        };
    }

    private async Task<GroqTranscriptionResult> TranscribeAsync(
        InterviewAudioInput audio,
        string apiKey,
        InterviewAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "openai/v1/audio/transcriptions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(audio.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(NormalizeContentType(audio.ContentType));
        content.Add(fileContent, "file", NormalizeFileName(audio.FileName));
        content.Add(new StringContent(GroqTranscriptionModel), "model");
        content.Add(new StringContent("pt"), "language");
        content.Add(new StringContent("verbose_json"), "response_format");
        content.Add(new StringContent("0"), "temperature");
        content.Add(new StringContent("segment"), "timestamp_granularities[]");

        var prompt = BuildTranscriptionPrompt(request);
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            content.Add(new StringContent(prompt, Encoding.UTF8), "prompt");
        }

        httpRequest.Content = content;

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new GroqAuthenticationException("Groq recusou a API Key informada.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new GroqProviderException($"Groq falhou ao transcrever a entrevista. {TrimProviderError(error)}");
        }

        var transcription = await response.Content.ReadFromJsonAsync<GroqTranscriptionResponse>(JsonOptions, cancellationToken);
        if (transcription is null || string.IsNullOrWhiteSpace(transcription.Text))
        {
            throw new GroqProviderException("Groq retornou uma transcricao vazia.");
        }

        return new GroqTranscriptionResult
        {
            Text = transcription.Text.Trim(),
            DurationSeconds = transcription.Duration,
            Segments = transcription.Segments
                .Select(segment => new InterviewTranscriptSegment
                {
                    Start = segment.Start,
                    End = segment.End,
                    StartTime = FormatTimestamp(segment.Start),
                    EndTime = FormatTimestamp(segment.End),
                    Text = segment.Text.Trim(),
                    AverageLogProbability = segment.AverageLogProbability,
                    NoSpeechProbability = segment.NoSpeechProbability,
                    CompressionRatio = segment.CompressionRatio
                })
                .Where(segment => !string.IsNullOrWhiteSpace(segment.Text))
                .ToList()
        };
    }

    private async Task<string> AnalyzeTranscriptAsync(
        GroqTranscriptionResult transcription,
        InterviewAnalysisRequest request,
        string token,
        string model,
        CancellationToken cancellationToken)
    {
        var timedTranscript = BuildTimedTranscript(transcription.Segments);
        var truncated = timedTranscript.Length > MaxTranscriptCharactersForAnalysis;
        var transcriptForAnalysis = truncated
            ? timedTranscript[..MaxTranscriptCharactersForAnalysis]
            : timedTranscript;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://gen.pollinations.ai/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(new PollinationsChatCompletionRequest(
            model,
            [
                new("system", BuildAnalysisSystemPrompt()),
                new("user", BuildAnalysisUserPrompt(request, transcriptForAnalysis, truncated, transcription))
            ],
            0.2m), options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new PollinationsAuthenticationException("Pollinations recusou o token informado.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PollinationsProviderException($"Pollinations falhou ao analisar a entrevista. {TrimProviderError(error)}");
        }

        var completion = await response.Content.ReadFromJsonAsync<PollinationsChatCompletionResponse>(JsonOptions, cancellationToken);
        var message = completion?.Choices.FirstOrDefault()?.Message.Content;
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new PollinationsProviderException("Pollinations retornou uma analise vazia.");
        }

        return message.Trim();
    }

    private static string BuildTranscriptionPrompt(InterviewAnalysisRequest request)
    {
        var values = new[]
        {
            "Entrevista de emprego em portugues do Brasil.",
            string.IsNullOrWhiteSpace(request.RoleTitle) ? string.Empty : $"Cargo: {request.RoleTitle.Trim()}.",
            string.IsNullOrWhiteSpace(request.CompanyName) ? string.Empty : $"Empresa: {request.CompanyName.Trim()}.",
            "Preserve termos tecnicos, nomes de tecnologias, senioridade, empresas e produto quando forem mencionados."
        };

        return string.Join(' ', values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildAnalysisSystemPrompt()
    {
        return $"""
            Voce e um avaliador auxiliar de entrevistas de emprego do Lessie.

            Analise SOMENTE evidencias observaveis na transcricao e no contexto fornecido.
            Nao faca diagnostico psicologico, clinico, psiquiatrico ou inferencias sobre atributos sensiveis.
            Quando falar de aspectos psicologicos, use apenas sinais comportamentais e comunicacionais observaveis:
            clareza, seguranca percebida, coerencia, objetividade, ansiedade aparente na fala, escuta, maturidade,
            capacidade de estruturar exemplos, colaboracao, postura diante de dificuldade e aderencia ao cargo.

            A resposta deve estar em portugues do Brasil e deve conter obrigatoriamente:
            1. A advertencia exatamente com o sentido abaixo, no topo.
            2. Resumo executivo.
            3. Estimativa de chance de aprovacao em percentual e faixa: baixa, media, boa ou forte.
            4. Justificativa da estimativa com evidencias da transcricao e tempos aproximados.
            5. Pontos fortes.
            6. Pontos de risco.
            7. Sinais comportamentais/comunicacionais observaveis.
            8. Perguntas ou temas em que o candidato pareceu melhor.
            9. Perguntas ou temas em que o candidato pareceu pior.
            10. Acoes praticas para melhorar na proxima entrevista.
            11. Frases alternativas que o candidato poderia usar.
            12. Trechos importantes citados com timestamp.

            Advertencia obrigatoria:
            {InterviewAnalysisWarnings.Estimate}
            """;
    }

    private static string BuildAnalysisUserPrompt(
        InterviewAnalysisRequest request,
        string transcript,
        bool truncated,
        GroqTranscriptionResult transcription)
    {
        return $"""
            Analise esta entrevista completa para apoiar melhoria de performance do candidato.

            Contexto informado:
            - Candidato: {ValueOrUnknown(request.CandidateName)}
            - Cargo: {ValueOrUnknown(request.RoleTitle)}
            - Empresa: {ValueOrUnknown(request.CompanyName)}
            - Contexto da entrevista: {ValueOrUnknown(request.InterviewContext)}
            - Descricao da vaga: {ValueOrUnknown(request.JobDescription)}
            - Instrucoes extras: {ValueOrUnknown(request.CustomInstructions)}

            Metadados da transcricao:
            - Modelo de transcricao: {GroqTranscriptionModel}
            - Duracao estimada: {FormatTimestamp(transcription.DurationSeconds > 0 ? transcription.DurationSeconds : transcription.Segments.LastOrDefault()?.End ?? 0)}
            - Segmentos transcritos: {transcription.Segments.Count}
            - Transcricao truncada para analise: {(truncated ? "sim, por limite de contexto" : "nao")}

            Transcricao com tempos:
            {transcript}
            """;
    }

    private static string BuildTimedTranscript(IReadOnlyCollection<InterviewTranscriptSegment> segments)
    {
        if (segments.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            builder.Append('[')
                .Append(segment.StartTime)
                .Append(" - ")
                .Append(segment.EndTime)
                .Append("] ")
                .AppendLine(segment.Text);
        }

        return builder.ToString().Trim();
    }

    private decimal GetUsdBrlRate()
    {
        var configured = configuration["USD_BRL_RATE"] ?? configuration["Currency:UsdBrlRate"];
        return decimal.TryParse(configured, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) && rate > 0
            ? rate
            : 5.17m;
    }

    private static string FormatTimestamp(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private static string ValueOrUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "nao informado" : value.Trim();
    }

    private static string NormalizeContentType(string contentType)
    {
        return string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
    }

    private static string NormalizeFileName(string fileName)
    {
        return string.IsNullOrWhiteSpace(fileName) ? "interview-audio.mp3" : Path.GetFileName(fileName);
    }

    private static string TrimProviderError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return string.Empty;
        }

        var normalized = error.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private sealed class GroqTranscriptionResult
    {
        public string Text { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public List<InterviewTranscriptSegment> Segments { get; set; } = [];
    }

    private sealed class GroqTranscriptionResponse
    {
        public string Text { get; set; } = string.Empty;
        public double Duration { get; set; }
        public List<GroqTranscriptionSegment> Segments { get; set; } = [];
    }

    private sealed class GroqTranscriptionSegment
    {
        public double Start { get; set; }
        public double End { get; set; }
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("avg_logprob")]
        public decimal? AverageLogProbability { get; set; }

        [JsonPropertyName("no_speech_prob")]
        public decimal? NoSpeechProbability { get; set; }

        [JsonPropertyName("compression_ratio")]
        public decimal? CompressionRatio { get; set; }
    }

    private sealed record PollinationsChatCompletionRequest(
        string Model,
        IReadOnlyCollection<PollinationsMessage> Messages,
        decimal Temperature);

    private sealed record PollinationsMessage(string Role, string Content);

    private sealed class PollinationsChatCompletionResponse
    {
        public List<PollinationsChoice> Choices { get; set; } = [];
    }

    private sealed class PollinationsChoice
    {
        public PollinationsChoiceMessage Message { get; set; } = new();
    }

    private sealed class PollinationsChoiceMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
