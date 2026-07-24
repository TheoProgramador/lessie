using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Lessie.Application.Chatbot;
using Lessie.Application.ProviderKeys;
using Lessie.Application.ResumeImprovements;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using UglyToad.PdfPig;
using MdDocument = MigraDoc.DocumentObjectModel.Document;
using MdPageFormat = MigraDoc.DocumentObjectModel.PageFormat;
using MdParagraph = MigraDoc.DocumentObjectModel.Paragraph;
using MdSection = MigraDoc.DocumentObjectModel.Section;
using MdTextFormat = MigraDoc.DocumentObjectModel.TextFormat;
using MdUnit = MigraDoc.DocumentObjectModel.Unit;

namespace Lessie.Infrastructure.ResumeImprovements;

internal sealed partial class ResumeImprovementService(
    HttpClient httpClient,
    IConfiguration configuration,
    IProviderKeyService providerKeyService,
    IResumeAtsAnalyzer atsAnalyzer,
    LessieDbContext dbContext,
    IResumeExternalMcpContextService externalMcpContextService) : IResumeImprovementService
{
    private const int MaxResumeCharacters = 24000;
    private const int MaxImageCount = 4;
    private const int MaxCompactMessageCharacters = 1200;
    private const int MaxChatSummaryCharacters = 6000;
    private const int MaxRagChunkCharacters = 1600;
    private const int MaxRetrievedChunks = 6;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "para",
        "como",
        "com",
        "uma",
        "por",
        "dos",
        "das",
        "que",
        "este",
        "esta",
        "esse",
        "essa",
        "mais",
        "sobre",
        "entre",
        "quando",
        "onde",
        "curriculo",
        "usuario",
        "vaga",
        "trabalho",
        "impacto"
    };

    static ResumeImprovementService()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    private const string SystemPrompt = """
        Voce e um especialista senior em curriculos, recrutamento tecnico e posicionamento profissional.

        Trabalhe sempre em portugues do Brasil.
        Seu objetivo e melhorar o curriculo do usuario com base no que ele realmente fez e no impacto gerado.
        Toda otimizacao deve priorizar a estrutura: trabalho feito + impacto atingido.

        Regras:
        - Nao invente empresas, cargos, numeros, tecnologias, certificacoes ou resultados.
        - Quando faltar informacao critica, faca no maximo 3 perguntas objetivas antes de fechar a versao final.
        - Nao enrole nem repita pedidos de confirmacao. Se o usuario autorizar aplicacao, revisao, melhoria, ajuste ou otimizacao, aplique imediatamente.
        - Sempre que aplicar qualquer alteracao no curriculo, devolva obrigatoriamente a versao completa atualizada entre os marcadores [CURRICULO_OTIMIZADO] e [/CURRICULO_OTIMIZADO].
        - Quando voce ainda nao tiver autorizacao para aplicar mudancas relevantes, peca autorizacao em uma pergunta direta, explicando que apos a autorizacao voce vai aplicar as alteracoes e medir novamente o ATS.
        - Se o usuario pedir "sim", "pode aplicar", "aplique", "faça", "ok", "manda", "pode seguir" ou equivalente, trate isso como autorizacao e nao faca nova pergunta antes de atualizar o curriculo.
        - Use prints de vagas para aumentar aderencia do curriculo a vaga.
        - Quando houver diagnostico ATS do CV Mirror MCP, integre essas opinioes na mesma analise e priorize correcoes que melhorem leitura por ATS.
        - Quando houver keywords ausentes no diagnostico ATS, inclua somente as que forem comprovadas pelo curriculo, historico, links ou respostas do usuario.
        - Quando o diagnostico recomendar desenvolver gaps, nao finja aderencia total: separe melhorias aplicaveis agora de perguntas ou plano de desenvolvimento.
        - Use a cobertura de requisitos para priorizar o topo do curriculo e reordenar bullets mais aderentes a vaga.
        - Quando houver uma versao melhorada, devolva ela entre os marcadores:
          [CURRICULO_OTIMIZADO]
          ...
          [/CURRICULO_OTIMIZADO]
        - Fora dos marcadores, seja breve: diga o que foi aplicado, o que ainda precisa de confirmacao ou quais dados faltam.
        - Escreva bullets fortes, orientados a acao, evidenciando escopo, tecnologia, colaboracao e impacto.
        """;

    public async Task<ResumeImprovementAnalyzeResponse> AnalyzeAsync(
        Guid userId,
        ResumeFileInput resume,
        IReadOnlyCollection<ResumeFileInput> jobScreenshots,
        ResumeImprovementAdditionalContext additionalContext,
        CancellationToken cancellationToken)
    {
        var resumeText = TrimContext(ExtractResumeText(resume));
        if (string.IsNullOrWhiteSpace(resumeText))
        {
            throw new InvalidOperationException("Nao foi possivel extrair texto do curriculo enviado.");
        }

        var previousRagContext = await BuildPreviousRagContextAsync(userId, resumeText, cancellationToken);
        var jobScreenshotsContext = await BuildJobScreenshotsContextAsync(userId, jobScreenshots, cancellationToken);
        var additionalContexts = await BuildAdditionalContextsAsync(additionalContext, cancellationToken);
        var additionalContextText = FormatAdditionalContexts(additionalContexts);
        var bestPracticesContext = await BuildResumeBestPracticesContextAsync(userId, cancellationToken);
        var atsLintContext = await BuildCvMirrorContextAsync(resume, cancellationToken);
        var localAtsAnalysis = atsAnalyzer.Analyze(resumeText, $"{jobScreenshotsContext}\n{additionalContext.JobDescription}");
        var externalMcpContext = await externalMcpContextService.BuildContextAsync(
            resumeText,
            $"{jobScreenshotsContext}\n{additionalContext.JobDescription}",
            cancellationToken);
        var content = new List<object>
        {
            TextPart($"""
                Analise este curriculo e inicie uma conversa para melhora-lo.

                Se houver informacoes suficientes, gere uma primeira versao otimizada.
                Se faltarem dados de impacto, pergunte ao usuario de forma objetiva, no maximo 3 perguntas.
                Se voce ainda nao for aplicar, peca autorizacao direta para aplicar as melhorias e medir o ATS.
                Quando aplicar qualquer melhoria, devolva o curriculo completo entre [CURRICULO_OTIMIZADO] e [/CURRICULO_OTIMIZADO], pois o sistema recalcula o ATS a partir desses marcadores.

                Curriculo extraido:
                {resumeText}

                Contexto recuperado de historicos anteriores:
                {previousRagContext}

                Contexto extraido dos prints de vagas:
                {jobScreenshotsContext}

                Contextos adicionais fornecidos pelo usuario ou coletados publicamente:
                {additionalContextText}

                Diagnostico ATS local via CV Mirror MCP (Workday, Greenhouse, Lever, Taleo e iCIMS):
                {atsLintContext}

                Diagnostico estruturado via ATS Resume MCP interno:
                {FormatAtsAnalysisForPrompt(localAtsAnalysis)}

                Contexto opcional de MCPs externos de curriculo (RChilli, FormaCV, CV Forge):
                {externalMcpContext}

                Integre esse diagnostico na mesma analise: corrija riscos de parse, clareza de secoes e aderencia ATS sem sacrificar a verdade do curriculo.

                Boas praticas atuais pesquisadas online para curriculos:
                {bestPracticesContext}
                """)
        };

        var response = await SendPollinationsAsync(
            userId,
            [new AiMessage("user", content)],
            cancellationToken);
        var parsed = ParseResponse(response);
        var jobContext = string.IsNullOrWhiteSpace(jobScreenshotsContext) ? string.Empty : TrimTo(jobScreenshotsContext, 4000);
        var currentResumeForAts = string.IsNullOrWhiteSpace(parsed.OptimizedResume)
            ? resumeText
            : parsed.OptimizedResume;
        var currentAtsAnalysis = atsAnalyzer.Analyze(currentResumeForAts, $"{jobContext}\n{additionalContext.JobDescription}");
        var session = await CreateSessionAsync(
            userId,
            resume.FileName,
            resumeText,
            jobContext,
            atsLintContext,
            additionalContexts,
            additionalContext,
            parsed,
            currentAtsAnalysis,
            cancellationToken);

        return new ResumeImprovementAnalyzeResponse
        {
            SessionId = session.Id,
            Message = parsed.Message,
            ResumeText = resumeText,
            JobContext = jobContext,
            OptimizedResume = parsed.OptimizedResume,
            ReadyToExport = parsed.ReadyToExport,
            AtsAnalysis = currentAtsAnalysis
        };
    }

    public async Task<ResumeImprovementChatResponse> ChatAsync(
        Guid userId,
        ResumeImprovementChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new InvalidOperationException("Mensagem obrigatoria.");
        }

        var session = request.SessionId.HasValue
            ? await dbContext.ResumeImprovementSessions
                .FirstOrDefaultAsync(
                    item => item.Id == request.SessionId.Value && item.UserId == userId,
                    cancellationToken)
            : null;
        if (request.SessionId.HasValue && session is null)
        {
            throw new InvalidOperationException("Historico de melhoria nao encontrado.");
        }

        var resumeContext = session is null
            ? TrimContext(request.ResumeText)
            : await BuildRagContextAsync(session.Id, request.Message, cancellationToken);
        var atsLintContext = session is null
            ? string.Empty
            : await BuildAtsLintContextAsync(session.Id, cancellationToken);
        var savedAtsAnalysis = session is null ? null : DeserializeAtsAnalysis(session.AtsAnalysisJson);

        if (session is not null && request.ForkFromSession)
        {
            session = await ForkSessionAsync(session, "nova melhoria", cancellationToken);
        }

        if (session is not null)
        {
            ApplyProfileLinks(session, request.LinkedInProfileUrl, request.GitHubProfileUrl, request.PortfolioUrl);
        }

        var jobContext = session?.JobContextSummary ?? TrimContext(request.JobContext);
        var optimizedResume = session?.CurrentOptimizedResume ?? request.OptimizedResume;
        var chatSummary = session?.ChatSummary ?? BuildCompactHistory(request.History);
        var profileLinksContext = session is null
            ? FormatProfileLinksForPrompt(request.LinkedInProfileUrl, request.GitHubProfileUrl, request.PortfolioUrl)
            : FormatProfileLinksForPrompt(session);
        var recentMessages = session is null
            ? request.History
                .Where(message => !string.IsNullOrWhiteSpace(message.Role) && !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(8)
                .ToArray()
            : await LoadRecentSessionMessagesAsync(session.Id, cancellationToken);

        var messages = new List<AiMessage>
        {
            new("user", $"""
                Contexto compacto da conversa ate agora:
                {TrimContext(chatSummary)}

                Mensagens recentes, com prioridade sobre o resumo caso haja conflito:
                {FormatRecentMessagesForPrompt(recentMessages)}

                Politica de execucao desta rodada:
                - A mensagem atual do usuario tem prioridade maxima.
                - Se a mensagem atual autorizar ou pedir uma alteracao, aplique agora na ultima versao otimizada e devolva o curriculo completo entre [CURRICULO_OTIMIZADO] e [/CURRICULO_OTIMIZADO].
                - Nao responda apenas com promessa de aplicacao quando ja houver autorizacao.
                - O sistema mede o ATS automaticamente a partir do curriculo devolvido nos marcadores; portanto, sempre que modificar o curriculo, use os marcadores.
                - Se ainda faltar autorizacao para mudancas relevantes, peca autorizacao em uma unica pergunta direta e informe que apos aplicar o sistema medira novamente o ATS.
                - Se faltar dado factual para melhorar impacto, faca no maximo 3 perguntas objetivas e aplique todas as melhorias que nao dependem dessas respostas.

                Contexto da vaga/prints:
                {TrimContext(jobContext)}

                Links publicos persistidos na sessao:
                {profileLinksContext}

                Trechos recuperados do curriculo para RAG:
                {TrimContext(resumeContext)}

                Diagnostico ATS salvo via CV Mirror MCP:
                {TrimContext(atsLintContext)}

                Diagnostico estruturado salvo via ATS Resume MCP interno:
                {FormatAtsAnalysisForPrompt(savedAtsAnalysis)}

                Ultima versao otimizada disponivel:
                {TrimContext(optimizedResume)}
                """)
        };

        if (session is null)
        {
            messages.AddRange(recentMessages
                .Select(message => new AiMessage(message.Role.Trim().ToLowerInvariant(), CompactMessageForPrompt(message.Content))));
        }

        messages.Add(new AiMessage("user", request.Message.Trim()));
        var sentPayloadPreview = BuildAiPayloadPreview(messages);

        var response = await SendPollinationsAsync(userId, messages, cancellationToken);
        var parsed = ParseResponse(response);
        if (ShouldForceResumeUpdate(request.Message) && string.IsNullOrWhiteSpace(parsed.OptimizedResume) && !string.IsNullOrWhiteSpace(optimizedResume))
        {
            messages.Add(new AiMessage("assistant", parsed.Message));
            messages.Add(new AiMessage("user", $"""
                Voce ainda nao aplicou a alteracao autorizada no curriculo.

                Aplique agora o pedido do usuario na ultima versao otimizada e devolva obrigatoriamente o curriculo completo entre:
                [CURRICULO_OTIMIZADO]
                ...
                [/CURRICULO_OTIMIZADO]

                Pedido do usuario:
                {request.Message.Trim()}

                Ultima versao otimizada:
                {TrimContext(optimizedResume)}
                """));

            sentPayloadPreview = BuildAiPayloadPreview(messages);
            response = await SendPollinationsAsync(userId, messages, cancellationToken);
            var retryParsed = ParseResponse(response);
            if (!string.IsNullOrWhiteSpace(retryParsed.OptimizedResume))
            {
                parsed = retryParsed;
            }
        }

        var currentOptimizedResume = string.IsNullOrWhiteSpace(parsed.OptimizedResume)
            ? optimizedResume
            : parsed.OptimizedResume;
        var readyToExport = parsed.ReadyToExport || !string.IsNullOrWhiteSpace(currentOptimizedResume);
        var currentAtsAnalysis = atsAnalyzer.Analyze(currentOptimizedResume, jobContext);

        if (session is not null)
        {
            await UpdateSessionAfterChatAsync(
                session,
                request.Message,
                parsed.Message,
                currentOptimizedResume,
                currentAtsAnalysis,
                readyToExport,
                cancellationToken);
        }

        return new ResumeImprovementChatResponse
        {
            SessionId = session?.Id ?? Guid.Empty,
            Message = parsed.Message,
            SentPayloadPreview = sentPayloadPreview,
            OptimizedResume = currentOptimizedResume,
            ReadyToExport = readyToExport,
            AtsAnalysis = currentAtsAnalysis
        };
    }

    public async Task<ResumeImprovementChatResponse> OptimizeForJobAsync(
        Guid userId,
        Guid sessionId,
        IReadOnlyCollection<ResumeFileInput> jobScreenshots,
        ResumeImprovementProfileLinksRequest profileLinks,
        bool forkFromSession,
        CancellationToken cancellationToken)
    {
        var supportedImages = jobScreenshots.Where(IsSupportedImage).Take(MaxImageCount).ToArray();
        if (supportedImages.Length == 0)
        {
            throw new InvalidOperationException("Envie ao menos um print de vaga em formato de imagem.");
        }

        var jobScreenshotsContext = await BuildJobScreenshotsContextAsync(userId, supportedImages, cancellationToken);
        if (string.IsNullOrWhiteSpace(jobScreenshotsContext))
        {
            throw new InvalidOperationException("Nao foi possivel extrair o texto dos prints de vaga.");
        }
        var bestPracticesContext = await BuildResumeBestPracticesContextAsync(userId, cancellationToken);

        var session = await dbContext.ResumeImprovementSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException("Historico de melhoria nao encontrado.");
        }

        var resumeContext = await BuildRagContextAsync(
            session.Id,
            "otimizar curriculo para vaga requisitos tecnologias responsabilidades impacto aderencia",
            cancellationToken);
        var atsLintContext = await BuildAtsLintContextAsync(session.Id, cancellationToken);
        var savedAtsAnalysis = DeserializeAtsAnalysis(session.AtsAnalysisJson);

        if (forkFromSession)
        {
            session = await ForkSessionAsync(session, "otimizacao de vaga", cancellationToken);
        }

        ApplyProfileLinks(session, profileLinks.LinkedInProfileUrl, profileLinks.GitHubProfileUrl, profileLinks.PortfolioUrl);
        var optimizedResume = session.CurrentOptimizedResume;
        var profileLinksContext = FormatProfileLinksForPrompt(session);
        var externalMcpContext = await externalMcpContextService.BuildContextAsync(
            optimizedResume,
            $"{session.JobContextSummary}\n{jobScreenshotsContext}",
            cancellationToken);

        var content = new List<object>
        {
            TextPart($"""
                Otimize o curriculo desta sessao com base nos prints de vaga anexados.

                Primeiro leia os requisitos, responsabilidades, senioridade, tecnologias e palavras-chave da vaga.
                Depois ajuste a versao otimizada do curriculo para aumentar aderencia, sem inventar experiencias.
                Preserve a estrutura profissional e priorize trabalho feito + impacto atingido.
                Esta acao ja foi solicitada pelo usuario, portanto aplique a otimizacao agora e devolva o curriculo completo entre [CURRICULO_OTIMIZADO] e [/CURRICULO_OTIMIZADO].
                Se faltar alguma informacao importante, faca no maximo 3 perguntas objetivas fora dos marcadores, mas aplique todas as melhorias possiveis sem depender dessas respostas.

                Contexto compacto da conversa ate agora:
                {TrimContext(session.ChatSummary)}

                Contexto anterior de vaga/prints:
                {TrimContext(session.JobContextSummary)}

                Links publicos persistidos na sessao:
                {profileLinksContext}

                Contexto extraido dos novos prints de vaga:
                {TrimContext(jobScreenshotsContext)}

                Boas praticas atuais pesquisadas online para curriculos:
                {TrimContext(bestPracticesContext)}

                Trechos recuperados do curriculo para RAG:
                {TrimContext(resumeContext)}

                Diagnostico ATS salvo via CV Mirror MCP:
                {TrimContext(atsLintContext)}

                Diagnostico estruturado salvo via ATS Resume MCP interno:
                {FormatAtsAnalysisForPrompt(savedAtsAnalysis)}

                Contexto opcional de MCPs externos de curriculo (RChilli, FormaCV, CV Forge):
                {externalMcpContext}

                Ultima versao otimizada disponivel:
                {TrimContext(optimizedResume)}

                Fora dos marcadores do curriculo, resuma em poucas linhas quais requisitos da vaga foram usados na otimizacao.
                """)
        };

        var aiMessages = new List<AiMessage> { new("user", content) };
        var sentPayloadPreview = BuildAiPayloadPreview(aiMessages);
        var response = await SendPollinationsAsync(
            userId,
            aiMessages,
            cancellationToken);
        var parsed = ParseResponse(response);
        var currentOptimizedResume = string.IsNullOrWhiteSpace(parsed.OptimizedResume)
            ? optimizedResume
            : parsed.OptimizedResume;
        var readyToExport = parsed.ReadyToExport || !string.IsNullOrWhiteSpace(currentOptimizedResume);
        var userMessage = "Otimize o curriculo com base nos novos prints de vaga anexados.";
        session.JobContextSummary = TrimTo(
            string.IsNullOrWhiteSpace(session.JobContextSummary)
                ? jobScreenshotsContext
                : $"{session.JobContextSummary}\n\n{jobScreenshotsContext}",
            4000);
        var currentAtsAnalysis = atsAnalyzer.Analyze(currentOptimizedResume, session.JobContextSummary);

        await UpdateSessionAfterChatAsync(
            session,
            userMessage,
            parsed.Message,
            currentOptimizedResume,
            currentAtsAnalysis,
            readyToExport,
            cancellationToken);

        return new ResumeImprovementChatResponse
        {
            SessionId = session.Id,
            Message = parsed.Message,
            SentPayloadPreview = sentPayloadPreview,
            OptimizedResume = currentOptimizedResume,
            ReadyToExport = readyToExport,
            AtsAnalysis = currentAtsAnalysis
        };
    }

    public async Task<IReadOnlyCollection<ResumeImprovementHistoryItem>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ResumeImprovementSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.UpdatedAt)
            .Take(30)
            .Select(session => new ResumeImprovementHistoryItem
            {
                Id = session.Id,
                Title = session.Title,
                ResumeFileName = session.ResumeFileName,
                ReadyToExport = session.ReadyToExport,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ResumeImprovementSessionDetail?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var sessionRow = await dbContext.ResumeImprovementSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && item.UserId == userId)
            .Select(item => new
            {
                Id = item.Id,
                Title = item.Title,
                ResumeFileName = item.ResumeFileName,
                JobContext = item.JobContextSummary,
                OptimizedResume = item.CurrentOptimizedResume,
                ReadyToExport = item.ReadyToExport,
                HasResumeContext = item.DocumentChunks.Any(),
                LinkedInProfileUrl = item.LinkedInProfileUrl,
                GitHubProfileUrl = item.GitHubProfileUrl,
                PortfolioUrl = item.PortfolioUrl,
                item.AtsAnalysisJson
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sessionRow is null)
        {
            return null;
        }

        var session = new ResumeImprovementSessionDetail
        {
            Id = sessionRow.Id,
            Title = sessionRow.Title,
            ResumeFileName = sessionRow.ResumeFileName,
            JobContext = sessionRow.JobContext,
            OptimizedResume = sessionRow.OptimizedResume,
            ReadyToExport = sessionRow.ReadyToExport,
            HasResumeContext = sessionRow.HasResumeContext,
            LinkedInProfileUrl = sessionRow.LinkedInProfileUrl,
            GitHubProfileUrl = sessionRow.GitHubProfileUrl,
            PortfolioUrl = sessionRow.PortfolioUrl,
            AtsAnalysis = DeserializeAtsAnalysis(sessionRow.AtsAnalysisJson)
        };

        session.Messages = await dbContext.ResumeImprovementMessages
            .AsNoTracking()
            .Where(message => message.ResumeImprovementSessionId == sessionId)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ChatMessageDto
            {
                Role = message.Role,
                Content = FormatStoredMessageForDisplay(message.Content, message.CompactContent)
            })
            .ToListAsync(cancellationToken);

        return session;
    }

    public async Task<ResumeImprovementSaveResponse> SaveOptimizedResumeAsync(
        Guid userId,
        Guid sessionId,
        ResumeImprovementSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OptimizedResume))
        {
            throw new InvalidOperationException("Nao ha curriculo otimizado para salvar.");
        }

        var session = await dbContext.ResumeImprovementSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException("Historico de melhoria nao encontrado.");
        }

        var now = DateTimeOffset.UtcNow;
        if (request.ForkFromSession)
        {
            session = await ForkSessionAsync(session, "edicao manual", cancellationToken);
        }

        session.CurrentOptimizedResume = request.OptimizedResume.Trim();
        var currentAtsAnalysis = atsAnalyzer.Analyze(session.CurrentOptimizedResume, session.JobContextSummary);
        StoreAtsAnalysis(session, currentAtsAnalysis);
        session.ReadyToExport = true;
        session.UpdatedAt = now;
        await ReplaceOptimizedResumeChunksAsync(session.Id, session.CurrentOptimizedResume, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResumeImprovementSaveResponse
        {
            SessionId = session.Id,
            OptimizedResume = session.CurrentOptimizedResume,
            ReadyToExport = session.ReadyToExport,
            UpdatedAt = session.UpdatedAt,
            AtsAnalysis = currentAtsAnalysis
        };
    }

    public async Task<ResumeImprovementRenameResponse> RenameSessionAsync(
        Guid userId,
        Guid sessionId,
        ResumeImprovementRenameRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Informe um titulo para o historico.");
        }

        var session = await dbContext.ResumeImprovementSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException("Historico de melhoria nao encontrado.");
        }

        session.Title = TrimTo(title, 300);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResumeImprovementRenameResponse
        {
            SessionId = session.Id,
            Title = session.Title,
            UpdatedAt = session.UpdatedAt
        };
    }

    public async Task<ResumeImprovementProfileLinksResponse> UpdateProfileLinksAsync(
        Guid userId,
        Guid sessionId,
        ResumeImprovementProfileLinksRequest request,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ResumeImprovementSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException("Historico de melhoria nao encontrado.");
        }

        session.LinkedInProfileUrl = TrimTo(request.LinkedInProfileUrl.Trim(), 1000);
        session.GitHubProfileUrl = TrimTo(request.GitHubProfileUrl.Trim(), 1000);
        session.PortfolioUrl = TrimTo(request.PortfolioUrl.Trim(), 1000);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResumeImprovementProfileLinksResponse
        {
            SessionId = session.Id,
            LinkedInProfileUrl = session.LinkedInProfileUrl,
            GitHubProfileUrl = session.GitHubProfileUrl,
            PortfolioUrl = session.PortfolioUrl,
            UpdatedAt = session.UpdatedAt
        };
    }

    public async Task<bool> DeleteSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ResumeImprovementSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        if (session is null)
        {
            return false;
        }

        dbContext.ResumeImprovementSessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public ResumeExportResult Export(ResumeExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("Nao ha curriculo otimizado para exportar.");
        }

        var format = request.Format.Trim().ToLowerInvariant();
        return format switch
        {
            "pdf" => new ResumeExportResult(
                CreatePdf(request.Content),
                "application/pdf",
                $"curriculo-otimizado-{DateTime.UtcNow:yyyyMMddHHmm}.pdf"),
            _ => new ResumeExportResult(
                CreateDocx(request.Content),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"curriculo-otimizado-{DateTime.UtcNow:yyyyMMddHHmm}.docx")
        };
    }

    private async Task<ResumeImprovementSession> CreateSessionAsync(
        Guid userId,
        string resumeFileName,
        string resumeText,
        string jobContext,
        string atsLintContext,
        IReadOnlyCollection<AdditionalResumeContext> additionalContexts,
        ResumeImprovementAdditionalContext submittedContext,
        ParsedResumeResponse parsed,
        ResumeAtsAnalysis atsAnalysis,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ResumeImprovementSession
        {
            UserId = userId,
            Title = BuildSessionTitle(resumeText, resumeFileName),
            ResumeFileName = TrimTo(resumeFileName, 512),
            JobContextSummary = TrimTo(jobContext, 4000),
            ChatSummary = BuildCompactChatSummary(string.Empty, "assistant", parsed.Message),
            CurrentOptimizedResume = parsed.OptimizedResume,
            AtsAnalysisJson = SerializeAtsAnalysis(atsAnalysis),
            CanonicalResumeJson = atsAnalysis.CanonicalResumeJson,
            LinkedInProfileUrl = TrimTo(submittedContext.LinkedInProfileUrl, 1000),
            GitHubProfileUrl = TrimTo(submittedContext.GitHubProfileUrl, 1000),
            PortfolioUrl = TrimTo(submittedContext.PortfolioUrl, 1000),
            ReadyToExport = parsed.ReadyToExport,
            CreatedAt = now,
            UpdatedAt = now,
            LastMessageAt = now
        };

        dbContext.ResumeImprovementSessions.Add(session);
        dbContext.ResumeImprovementMessages.Add(new ResumeImprovementMessage
        {
            ResumeImprovementSessionId = session.Id,
            Role = "assistant",
            CompactContent = CompactMessage(parsed.Message),
            Content = parsed.Message,
            CreatedAt = now
        });

        AddDocumentChunks(session.Id, "OriginalResume", resumeText, now);
        if (!string.IsNullOrWhiteSpace(atsLintContext))
        {
            AddDocumentChunks(session.Id, "AtsLint", atsLintContext, now);
        }

        foreach (var additionalContext in additionalContexts)
        {
            AddDocumentChunks(session.Id, additionalContext.Source, additionalContext.Content, now);
        }

        if (!string.IsNullOrWhiteSpace(parsed.OptimizedResume))
        {
            AddDocumentChunks(session.Id, "OptimizedResume", parsed.OptimizedResume, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task<ResumeImprovementSession> ForkSessionAsync(
        ResumeImprovementSession source,
        string suffix,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var fork = new ResumeImprovementSession
        {
            UserId = source.UserId,
            Title = BuildForkTitle(source.Title, suffix),
            ResumeFileName = source.ResumeFileName,
            JobContextSummary = source.JobContextSummary,
            ChatSummary = source.ChatSummary,
            CurrentOptimizedResume = source.CurrentOptimizedResume,
            AtsAnalysisJson = source.AtsAnalysisJson,
            CanonicalResumeJson = source.CanonicalResumeJson,
            LinkedInProfileUrl = source.LinkedInProfileUrl,
            GitHubProfileUrl = source.GitHubProfileUrl,
            PortfolioUrl = source.PortfolioUrl,
            ReadyToExport = source.ReadyToExport,
            CreatedAt = now,
            UpdatedAt = now,
            LastMessageAt = source.LastMessageAt
        };

        dbContext.ResumeImprovementSessions.Add(fork);

        var messages = await dbContext.ResumeImprovementMessages
            .AsNoTracking()
            .Where(message => message.ResumeImprovementSessionId == source.Id)
            .OrderBy(message => message.CreatedAt)
            .ToArrayAsync(cancellationToken);

        foreach (var message in messages)
        {
            dbContext.ResumeImprovementMessages.Add(new ResumeImprovementMessage
            {
                ResumeImprovementSessionId = fork.Id,
                Role = message.Role,
                CompactContent = message.CompactContent,
                Content = string.IsNullOrWhiteSpace(message.Content) ? message.CompactContent : message.Content,
                CreatedAt = message.CreatedAt
            });
        }

        var chunks = await dbContext.ResumeImprovementDocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.ResumeImprovementSessionId == source.Id && chunk.Source != "OptimizedResume")
            .OrderBy(chunk => chunk.Source)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ToArrayAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            dbContext.ResumeImprovementDocumentChunks.Add(new ResumeImprovementDocumentChunk
            {
                ResumeImprovementSessionId = fork.Id,
                Source = chunk.Source,
                ChunkIndex = chunk.ChunkIndex,
                Content = chunk.Content,
                Keywords = chunk.Keywords,
                CreatedAt = now
            });
        }

        return fork;
    }

    private async Task UpdateSessionAfterChatAsync(
        ResumeImprovementSession session,
        string userMessage,
        string assistantMessage,
        string optimizedResume,
        ResumeAtsAnalysis atsAnalysis,
        bool readyToExport,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        session.ChatSummary = BuildCompactChatSummary(session.ChatSummary, "user", userMessage);
        session.ChatSummary = BuildCompactChatSummary(session.ChatSummary, "assistant", assistantMessage);
        session.CurrentOptimizedResume = optimizedResume;
        StoreAtsAnalysis(session, atsAnalysis);
        session.ReadyToExport = readyToExport;
        session.UpdatedAt = now;
        session.LastMessageAt = now;

        dbContext.ResumeImprovementMessages.Add(new ResumeImprovementMessage
        {
            ResumeImprovementSessionId = session.Id,
            Role = "user",
            CompactContent = CompactMessage(userMessage),
            Content = userMessage,
            CreatedAt = now.AddMilliseconds(-1)
        });
        dbContext.ResumeImprovementMessages.Add(new ResumeImprovementMessage
        {
            ResumeImprovementSessionId = session.Id,
            Role = "assistant",
            CompactContent = CompactMessage(assistantMessage),
            Content = assistantMessage,
            CreatedAt = now
        });

        await ReplaceOptimizedResumeChunksAsync(session.Id, optimizedResume, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> BuildRagContextAsync(Guid sessionId, string query, CancellationToken cancellationToken)
    {
        var chunks = await dbContext.ResumeImprovementDocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.ResumeImprovementSessionId == sessionId)
            .OrderBy(chunk => chunk.Source)
            .ThenBy(chunk => chunk.ChunkIndex)
            .Select(chunk => new RagChunk(chunk.Source, chunk.ChunkIndex, chunk.Content, chunk.Keywords))
            .ToArrayAsync(cancellationToken);

        if (chunks.Length == 0)
        {
            return string.Empty;
        }

        var queryWords = ExtractKeywords(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreChunk(chunk, queryWords)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Source == "OptimizedResume" ? 0 : 1)
            .ThenBy(item => item.Chunk.ChunkIndex)
            .Take(MaxRetrievedChunks)
            .Select(item => $"[{item.Chunk.Source} #{item.Chunk.ChunkIndex + 1}]\n{item.Chunk.Content}");

        return string.Join("\n\n", selected);
    }

    private async Task<IReadOnlyCollection<ChatMessageDto>> LoadRecentSessionMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var messages = await dbContext.ResumeImprovementMessages
            .AsNoTracking()
            .Where(message => message.ResumeImprovementSessionId == sessionId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(8)
            .Select(message => new
            {
                message.Role,
                message.Content,
                message.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        return messages
            .OrderBy(message => message.CreatedAt)
            .Select(message => new ChatMessageDto
            {
                Role = message.Role,
                Content = message.Content
            })
            .ToArray();
    }

    private async Task<string> BuildAtsLintContextAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var chunks = await dbContext.ResumeImprovementDocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.ResumeImprovementSessionId == sessionId && chunk.Source == "AtsLint")
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => chunk.Content)
            .ToArrayAsync(cancellationToken);

        return TrimTo(string.Join("\n\n", chunks), 5000);
    }

    private async Task<string> BuildPreviousRagContextAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        var chunks = await dbContext.ResumeImprovementDocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.Session != null && chunk.Session.UserId == userId)
            .OrderByDescending(chunk => chunk.Session!.UpdatedAt)
            .Take(60)
            .Select(chunk => new RagChunk(chunk.Source, chunk.ChunkIndex, chunk.Content, chunk.Keywords))
            .ToArrayAsync(cancellationToken);

        if (chunks.Length == 0)
        {
            return string.Empty;
        }

        var queryWords = ExtractKeywords(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreChunk(chunk, queryWords)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Source == "OptimizedResume" ? 0 : 1)
            .ThenBy(item => item.Chunk.ChunkIndex)
            .Take(4)
            .Select(item => $"[{item.Chunk.Source} anterior #{item.Chunk.ChunkIndex + 1}]\n{item.Chunk.Content}");

        return string.Join("\n\n", selected);
    }

    private async Task<string> BuildJobScreenshotsContextAsync(
        Guid userId,
        IReadOnlyCollection<ResumeFileInput> jobScreenshots,
        CancellationToken cancellationToken)
    {
        var supportedImages = jobScreenshots.Where(IsSupportedImage).Take(MaxImageCount).ToArray();
        if (supportedImages.Length == 0)
        {
            return string.Empty;
        }

        var content = new List<object>
        {
            TextPart("""
                Leia estes prints de vaga e extraia somente informacoes uteis para adaptar um curriculo.

                Responda em portugues do Brasil, de forma objetiva, com:
                - cargo e senioridade percebidos;
                - local/modelo de trabalho;
                - responsabilidades;
                - requisitos tecnicos;
                - ferramentas, tecnologias e palavras-chave;
                - pontos de aderencia que devem aparecer no curriculo.

                Nao invente informacoes que nao estejam visiveis nos prints.
                """)
        };

        foreach (var image in supportedImages)
        {
            content.Add(ImagePart(image));
        }

        var visionModel = configuration["POLLINATIONS_VISION_MODEL"]
            ?? configuration["Pollinations:VisionModel"]
            ?? "kimi";
        var configuredModel = GetPollinationsModel();

        try
        {
            return await SendPollinationsAsync(
                userId,
                [new AiMessage("user", content)],
                cancellationToken,
                visionModel);
        }
        catch (PollinationsProviderException) when (!visionModel.Equals(configuredModel, StringComparison.OrdinalIgnoreCase))
        {
            return await SendPollinationsAsync(
                userId,
                [new AiMessage("user", content)],
                cancellationToken,
                configuredModel);
        }
    }

    private async Task<IReadOnlyCollection<AdditionalResumeContext>> BuildAdditionalContextsAsync(
        ResumeImprovementAdditionalContext additionalContext,
        CancellationToken cancellationToken)
    {
        var contexts = new List<AdditionalResumeContext>();

        if (additionalContext.LinkedInProfile is not null)
        {
            var linkedInText = TrimContext(ExtractResumeText(additionalContext.LinkedInProfile));
            if (!string.IsNullOrWhiteSpace(linkedInText))
            {
                contexts.Add(new AdditionalResumeContext(
                    "LinkedInProfile",
                    $"Perfil LinkedIn extraido de {additionalContext.LinkedInProfile.FileName}:\n{linkedInText}"));
            }
        }

        var linkedInUrlContext = BuildLinkedInProfileUrlContext(additionalContext.LinkedInProfileUrl);
        if (!string.IsNullOrWhiteSpace(linkedInUrlContext))
        {
            contexts.Add(new AdditionalResumeContext("LinkedInProfileUrl", linkedInUrlContext));
        }

        var githubContext = await BuildGitHubProfileContextAsync(additionalContext.GitHubProfileUrl, cancellationToken);
        if (!string.IsNullOrWhiteSpace(githubContext))
        {
            contexts.Add(new AdditionalResumeContext("GitHubProfile", githubContext));
        }

        var portfolioContext = await BuildUrlContextAsync(
            additionalContext.PortfolioUrl,
            "Portfolio publico informado pelo usuario",
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(portfolioContext))
        {
            contexts.Add(new AdditionalResumeContext("Portfolio", portfolioContext));
        }

        AddTextContext(contexts, "PersonalInfo", "Informacoes pessoais/profissionais adicionais", additionalContext.PersonalInfo);
        AddTextContext(contexts, "CustomInstructions", "Instrucoes customizadas do usuario", additionalContext.CustomInstructions);
        AddTextContext(contexts, "JobDescription", "Descricao de vaga colada pelo usuario", additionalContext.JobDescription);

        return contexts;
    }

    private static void AddTextContext(
        ICollection<AdditionalResumeContext> contexts,
        string source,
        string label,
        string value)
    {
        var text = TrimContext(value);
        if (!string.IsNullOrWhiteSpace(text))
        {
            contexts.Add(new AdditionalResumeContext(source, $"{label}:\n{text}"));
        }
    }

    private static string FormatAdditionalContexts(IReadOnlyCollection<AdditionalResumeContext> contexts)
    {
        if (contexts.Count == 0)
        {
            return "Nenhum contexto adicional informado.";
        }

        return string.Join("\n\n", contexts.Select(context => $"[{context.Source}]\n{TrimContext(context.Content)}"));
    }

    private static string BuildLinkedInProfileUrlContext(string url)
    {
        if (!TryCreateHttpUri(url, out var uri) || !IsLinkedInProfileUri(uri))
        {
            return string.Empty;
        }

        return $"""
            Perfil publico do LinkedIn informado pelo usuario:
            {uri}

            Use esta URL como referencia de identidade profissional e, quando precisar de conteudo detalhado, priorize o PDF/DOCX do LinkedIn anexado pelo usuario. Nao invente experiencias que nao estejam no curriculo, no PDF do LinkedIn ou nas respostas do usuario.
            """;
    }

    private async Task<string> BuildGitHubProfileContextAsync(string url, CancellationToken cancellationToken)
    {
        var username = ExtractGitHubUsername(url);
        if (string.IsNullOrWhiteSpace(username))
        {
            return string.Empty;
        }

        try
        {
            using var userDocument = JsonDocument.Parse(await ReadUrlTextAsync(
                new Uri($"https://api.github.com/users/{Uri.EscapeDataString(username)}"),
                cancellationToken));
            using var reposDocument = JsonDocument.Parse(await ReadUrlTextAsync(
                new Uri($"https://api.github.com/users/{Uri.EscapeDataString(username)}/repos?per_page=20&sort=updated"),
                cancellationToken));

            var user = userDocument.RootElement;
            var lines = new List<string>
            {
                $"GitHub publico: https://github.com/{username}",
                $"Nome: {ReadNullable(user, "name") ?? username}",
                $"Bio: {ReadNullable(user, "bio") ?? "(sem bio publica)"}",
                $"Empresa: {ReadNullable(user, "company") ?? "(nao informado)"}",
                $"Localizacao: {ReadNullable(user, "location") ?? "(nao informado)"}",
                $"Repositorios publicos: {ReadNullable(user, "public_repos") ?? "0"}"
            };

            var repos = reposDocument.RootElement.ValueKind == JsonValueKind.Array
                ? reposDocument.RootElement.EnumerateArray()
                    .Where(repo => !ReadBool(repo, "fork"))
                    .Take(12)
                    .Select(repo =>
                    {
                        var name = ReadNullable(repo, "name") ?? "(sem nome)";
                        var language = ReadNullable(repo, "language") ?? "linguagem nao informada";
                        var description = ReadNullable(repo, "description") ?? "sem descricao";
                        var stars = ReadNullable(repo, "stargazers_count") ?? "0";
                        return $"- {name} ({language}, stars {stars}): {description}";
                    })
                    .ToArray()
                : [];

            if (repos.Length > 0)
            {
                lines.Add("Repositorios recentes relevantes:");
                lines.AddRange(repos);
            }

            return TrimTo(string.Join("\n", lines), 5000);
        }
        catch
        {
            return await BuildUrlContextAsync(
                $"https://github.com/{username}",
                "GitHub publico informado pelo usuario",
                cancellationToken);
        }
    }

    private async Task<string> BuildUrlContextAsync(string url, string label, CancellationToken cancellationToken)
    {
        if (!TryCreateHttpUri(url, out var uri))
        {
            return string.Empty;
        }

        try
        {
            var raw = await ReadUrlTextAsync(uri, cancellationToken);
            var text = NormalizeWebPageText(raw);
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : TrimTo($"{label}: {uri}\n{text}", 5000);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> ReadUrlTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Lessie/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return TrimTo(content, 20000);
    }

    private static string NormalizeWebPageText(string value)
    {
        var withoutScripts = Regex.Replace(value, @"<script[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        withoutScripts = Regex.Replace(withoutScripts, @"<style[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withoutScripts, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static string ExtractGitHubUsername(string value)
    {
        if (!TryCreateHttpUri(value, out var uri))
        {
            return string.Empty;
        }

        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static bool IsLinkedInProfileUri(Uri uri)
    {
        if (!uri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[0].Equals("in", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateHttpUri(string value, out Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)
            && parsed.Scheme is "http" or "https")
        {
            uri = parsed;
            return true;
        }

        uri = new Uri("http://localhost");
        return false;
    }

    private async Task<string> BuildResumeBestPracticesContextAsync(Guid userId, CancellationToken cancellationToken)
    {
        var searchModel = configuration["POLLINATIONS_SEARCH_MODEL"]
            ?? configuration["Pollinations:SearchModel"]
            ?? "gemini-search";

        try
        {
            var response = await SendPollinationsAsync(
                userId,
                [
                    new AiMessage("user", """
                        Pesquise online boas praticas atuais para criar e otimizar curriculos profissionais, especialmente curriculos de tecnologia e sistemas financeiros.

                        Sintetize em portugues do Brasil, sem texto promocional, priorizando:
                        - estrutura compativel com ATS;
                        - bullets orientados a trabalho feito + impacto atingido;
                        - uso de palavras-chave da vaga sem keyword stuffing;
                        - clareza de senioridade, escopo, tecnologias e resultados;
                        - o que evitar em curriculos modernos;
                        - adaptacao do curriculo para uma vaga especifica.

                        Inclua apenas recomendacoes praticas e confiaveis para serem usadas como criterio de avaliacao do curriculo.
                        """)
                ],
                cancellationToken,
                searchModel);

            return TrimTo(response, 4000);
        }
        catch (PollinationsProviderException)
        {
            return string.Empty;
        }
    }

    private async Task<string> BuildCvMirrorContextAsync(ResumeFileInput resume, CancellationToken cancellationToken)
    {
        var settings = CvMirrorSettings.FromConfiguration(configuration);
        if (!settings.Enabled)
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(resume.FileName).ToLowerInvariant();
        if (extension is not ".pdf" and not ".docx")
        {
            return string.Empty;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"lessie-cv-mirror-{Guid.NewGuid():N}{extension}");
        try
        {
            await File.WriteAllBytesAsync(tempPath, resume.Content, cancellationToken);

            using var process = StartCvMirrorProcess(settings, tempPath);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(settings.Timeout);

            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return TrimTo($"CV Mirror MCP nao retornou analise ATS. Detalhe: {stderr}", 1200);
            }

            return FormatCvMirrorReport(stdout);
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort cleanup of a temporary CV copy.
            }
        }
    }

    private static Process StartCvMirrorProcess(CvMirrorSettings settings, string resumePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = settings.Command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in settings.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(resumePath);

        var workingDirectory = settings.ResolveWorkingDirectory();
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("CV Mirror MCP process could not be started.");
    }

    private static string FormatCvMirrorReport(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var lines = new List<string>
            {
                $"Formato: {ReadString(root, "format")}",
                $"Paginas: {ReadNullable(root, "pages")}",
                $"Palavras: {ReadNullable(root, "wordCount")}"
            };

            if (root.TryGetProperty("vendors", out var vendors))
            {
                foreach (var vendor in vendors.EnumerateObject())
                {
                    var name = vendor.Value.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString()
                        : vendor.Name;
                    lines.Add($"ATS: {name}");

                    if (!vendor.Value.TryGetProperty("findings", out var findings) || findings.GetArrayLength() == 0)
                    {
                        lines.Add("- OK: nenhum problema critico detectado.");
                        continue;
                    }

                    foreach (var finding in findings.EnumerateArray().Take(8))
                    {
                        var severity = ReadString(finding, "severity").ToUpperInvariant();
                        var code = ReadString(finding, "code");
                        var message = ReadString(finding, "message");
                        var fix = ReadString(finding, "fix");
                        lines.Add($"- {severity} {code}: {message}");
                        if (!string.IsNullOrWhiteSpace(fix))
                        {
                            lines.Add($"  Correcao sugerida: {fix}");
                        }
                    }
                }
            }

            return TrimTo(string.Join("\n", lines), 5000);
        }
        catch
        {
            return TrimTo(json, 5000);
        }
    }

    private static int ScoreChunk(RagChunk chunk, HashSet<string> queryWords)
    {
        if (queryWords.Count == 0)
        {
            return chunk.Source == "OptimizedResume" ? 2 : 1;
        }

        var chunkWords = ExtractKeywords(chunk.Content + " " + chunk.Keywords).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return queryWords.Count(chunkWords.Contains) + (chunk.Source == "OptimizedResume" ? 1 : 0);
    }

    private void AddDocumentChunks(Guid sessionId, string source, string content, DateTimeOffset now)
    {
        var chunks = ChunkText(content).ToArray();
        for (var index = 0; index < chunks.Length; index++)
        {
            dbContext.ResumeImprovementDocumentChunks.Add(new ResumeImprovementDocumentChunk
            {
                ResumeImprovementSessionId = sessionId,
                Source = source,
                ChunkIndex = index,
                Content = chunks[index],
                Keywords = string.Join(", ", ExtractKeywords(chunks[index]).Take(40)),
                CreatedAt = now
            });
        }
    }

    private async Task ReplaceOptimizedResumeChunksAsync(
        Guid sessionId,
        string optimizedResume,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previousChunks = await dbContext.ResumeImprovementDocumentChunks
            .Where(chunk => chunk.ResumeImprovementSessionId == sessionId && chunk.Source == "OptimizedResume")
            .ToArrayAsync(cancellationToken);
        dbContext.ResumeImprovementDocumentChunks.RemoveRange(previousChunks);

        if (!string.IsNullOrWhiteSpace(optimizedResume))
        {
            AddDocumentChunks(sessionId, "OptimizedResume", optimizedResume, now);
        }
    }

    private static IEnumerable<string> ChunkText(string content)
    {
        var normalized = Regex.Replace(content, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        for (var index = 0; index < normalized.Length; index += MaxRagChunkCharacters)
        {
            var length = Math.Min(MaxRagChunkCharacters, normalized.Length - index);
            yield return normalized.Substring(index, length).Trim();
        }
    }

    private static string BuildCompactHistory(IEnumerable<ChatMessageDto> history)
    {
        var summary = string.Empty;
        foreach (var message in history.TakeLast(10))
        {
            summary = BuildCompactChatSummary(summary, message.Role, message.Content);
        }

        return summary;
    }

    private static string FormatRecentMessagesForPrompt(IEnumerable<ChatMessageDto> messages)
    {
        var formatted = messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Role) && !string.IsNullOrWhiteSpace(message.Content))
            .Select(message =>
            {
                var label = message.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "Usuario" : "IA";
                return $"{label}: {CompactMessageForPrompt(message.Content)}";
            })
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        return formatted.Length == 0
            ? "Sem mensagens recentes alem do pedido atual."
            : string.Join("\n", formatted);
    }

    private static string FormatProfileLinksForPrompt(ResumeImprovementSession session)
    {
        return FormatProfileLinksForPrompt(session.LinkedInProfileUrl, session.GitHubProfileUrl, session.PortfolioUrl);
    }

    private static string FormatProfileLinksForPrompt(string linkedInProfileUrl, string gitHubProfileUrl, string portfolioUrl)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(linkedInProfileUrl))
        {
            lines.Add($"LinkedIn publico: {linkedInProfileUrl}");
        }

        if (!string.IsNullOrWhiteSpace(gitHubProfileUrl))
        {
            lines.Add($"GitHub: {gitHubProfileUrl}");
        }

        if (!string.IsNullOrWhiteSpace(portfolioUrl))
        {
            lines.Add($"Portfolio: {portfolioUrl}");
        }

        return lines.Count == 0
            ? "Nenhum link publico persistido nesta sessao."
            : string.Join("\n", lines);
    }

    private static void ApplyProfileLinks(
        ResumeImprovementSession session,
        string linkedInProfileUrl,
        string gitHubProfileUrl,
        string portfolioUrl)
    {
        session.LinkedInProfileUrl = TrimTo(linkedInProfileUrl.Trim(), 1000);
        session.GitHubProfileUrl = TrimTo(gitHubProfileUrl.Trim(), 1000);
        session.PortfolioUrl = TrimTo(portfolioUrl.Trim(), 1000);
    }

    private static bool ShouldForceResumeUpdate(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = RemoveDiacritics(message).ToLowerInvariant();
        string[] triggers =
        [
            "sim",
            "ok",
            "pode",
            "aplica",
            "aplique",
            "aplicar",
            "faca",
            "fazer",
            "manda",
            "siga",
            "seguir",
            "autorizo",
            "melhore",
            "ajuste",
            "otimize",
            "reescreva",
            "refaca",
            "altere",
            "inclua",
            "remova",
            "corrija"
        ];

        return triggers.Any(trigger => Regex.IsMatch(normalized, $@"(^|\W){Regex.Escape(trigger)}(\W|$)"));
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string BuildCompactChatSummary(string previousSummary, string role, string content)
    {
        var compact = CompactMessageForPrompt(RemoveOptimizedResumeBlock(content));
        if (string.IsNullOrWhiteSpace(compact))
        {
            return TrimTo(previousSummary, MaxChatSummaryCharacters);
        }

        var label = role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "Usuario" : "IA";
        var summary = string.IsNullOrWhiteSpace(previousSummary)
            ? $"{label}: {compact}"
            : $"{previousSummary}\n{label}: {compact}";

        return TrimFromStart(summary, MaxChatSummaryCharacters);
    }

    private static string CompactMessage(string content)
    {
        var withoutResume = RemoveOptimizedResumeBlock(content);
        var normalized = withoutResume
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized
            .Split('\n')
            .Select(line => Regex.Replace(line.Trim(), @"[ \t]+", " "))
            .ToArray();
        var compact = string.Join("\n", CollapseBlankLines(lines)).Trim();
        return TrimTo(compact, MaxCompactMessageCharacters);
    }

    private static string FormatStoredMessageForDisplay(string content, string compactContent)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return compactContent;
        }

        if (content.Length >= MaxCompactMessageCharacters
            && content.Equals(compactContent, StringComparison.Ordinal))
        {
            return $"{content}\n\n[Mensagem antiga recuperada do historico compacto; o texto completo nao foi salvo na versao anterior.]";
        }

        return content;
    }

    private static string BuildAiPayloadPreview(IEnumerable<AiMessage> messages)
    {
        var lines = messages.Select(message =>
        {
            var role = message.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "Usuario" : "IA";
            return $"### {role}\n{CompactMessage(FormatAiContentForPreview(message.Content))}";
        });

        return TrimTo(string.Join("\n\n", lines), 8000);
    }

    private static string FormatAiContentForPreview(object content)
    {
        if (content is string text)
        {
            return text;
        }

        if (content is IEnumerable<object> parts)
        {
            return string.Join("\n\n", parts.Select(FormatAiPartForPreview));
        }

        return content.ToString() ?? string.Empty;
    }

    private static string FormatAiPartForPreview(object part)
    {
        var type = part.GetType().GetProperty("type")?.GetValue(part)?.ToString();
        if (type?.Equals("text", StringComparison.OrdinalIgnoreCase) == true)
        {
            return part.GetType().GetProperty("text")?.GetValue(part)?.ToString() ?? string.Empty;
        }

        if (type?.Equals("image_url", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "[imagem enviada ao modelo]";
        }

        return part.ToString() ?? string.Empty;
    }

    private static string CompactMessageForPrompt(string content)
    {
        var withoutResume = RemoveOptimizedResumeBlock(content);
        var compact = Regex.Replace(withoutResume, @"\s+", " ").Trim();
        return TrimTo(compact, MaxCompactMessageCharacters);
    }

    private static IEnumerable<string> CollapseBlankLines(IEnumerable<string> lines)
    {
        var previousBlank = false;
        foreach (var line in lines)
        {
            var blank = string.IsNullOrWhiteSpace(line);
            if (blank && previousBlank)
            {
                continue;
            }

            yield return line;
            previousBlank = blank;
        }
    }

    private static string RemoveOptimizedResumeBlock(string content)
    {
        return OptimizedResumeRegex().Replace(content, string.Empty).Trim();
    }

    private static string BuildSessionTitle(string resumeText, string resumeFileName)
    {
        var firstLine = resumeText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length is >= 3 and <= 80);

        return TrimTo(firstLine ?? Path.GetFileNameWithoutExtension(resumeFileName), 300);
    }

    private static string BuildForkTitle(string sourceTitle, string suffix)
    {
        var title = string.IsNullOrWhiteSpace(sourceTitle) ? "Curriculo" : sourceTitle.Trim();
        var marker = $" - {suffix}";
        if (title.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            return TrimTo(title, 300);
        }

        return TrimTo($"{title}{marker}", 300);
    }

    private static IReadOnlyCollection<string> ExtractKeywords(string content)
    {
        return WordRegex().Matches(content.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length >= 4 && !StopWords.Contains(word))
            .GroupBy(word => word)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .Take(80)
            .ToArray();
    }

    private static string TrimTo(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength].Trim();
    }

    private static string TrimFromStart(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[^maxLength..].Trim();
    }

    private static string FormatAtsAnalysisForPrompt(ResumeAtsAnalysis? analysis)
    {
        if (analysis is null)
        {
            return "Sem diagnostico ATS estruturado salvo ainda.";
        }

        var sections = string.Join(
            "\n",
            analysis.Sections.Select(section => $"- {section.Name}: {section.Score}/100 ({section.Status}) - {section.Summary}"));
        var recommendations = analysis.Recommendations.Count == 0
            ? "Sem recomendacoes pendentes."
            : string.Join("\n", analysis.Recommendations.Select(item => $"- {item}"));
        var keywordsPresent = analysis.KeywordsPresent.Count == 0
            ? "Nenhuma keyword especifica identificada."
            : string.Join(", ", analysis.KeywordsPresent.Take(12));
        var keywordsMissing = analysis.KeywordsMissing.Count == 0
            ? "Nenhuma keyword ausente critica identificada."
            : string.Join(", ", analysis.KeywordsMissing.Take(12));
        var gaps = analysis.CriticalGaps.Count == 0
            ? "Sem gaps criticos estruturados."
            : string.Join("\n", analysis.CriticalGaps.Take(6).Select(item => $"- {item}"));
        var subscores = analysis.Subscores.Count == 0
            ? "Sem subscores."
            : string.Join(", ", analysis.Subscores.Select(item => $"{item.Key}: {item.Value}"));
        var coverage = analysis.RequirementCoverage.Count == 0
            ? "Sem cobertura de requisitos."
            : string.Join(
                "\n",
                analysis.RequirementCoverage
                    .Take(6)
                    .Select(item => $"- {item.Status}: {item.Requirement} ({item.Evidence})"));
        var keywordStrategy = analysis.KeywordStrategy.Count == 0
            ? "Sem estrategia de keywords por secao."
            : string.Join(
                "\n",
                analysis.KeywordStrategy
                    .Take(6)
                    .Select(item => $"- {item.Group} -> {item.TargetSection}: {string.Join(", ", item.Keywords.Take(10))}. {item.Instruction}"));

        return $"""
            Provider: {analysis.Provider}
            Score geral: {analysis.OverallScore}/100
            Recomendacao de matching: {analysis.MatchRecommendation}
            Subscores ponderados: {subscores}
            Keywords presentes: {keywordsPresent}
            Keywords ausentes: {keywordsMissing}
            Gaps criticos:
            {gaps}
            Estrategia de keywords por secao:
            {keywordStrategy}
            Cobertura de requisitos:
            {coverage}
            Secoes:
            {sections}
            Recomendacoes:
            {recommendations}
            """;
    }

    private static string SerializeAtsAnalysis(ResumeAtsAnalysis analysis)
    {
        return JsonSerializer.Serialize(analysis, JsonOptions);
    }

    private static ResumeAtsAnalysis? DeserializeAtsAnalysis(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ResumeAtsAnalysis>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void StoreAtsAnalysis(ResumeImprovementSession session, ResumeAtsAnalysis analysis)
    {
        session.AtsAnalysisJson = SerializeAtsAnalysis(analysis);
        session.CanonicalResumeJson = string.IsNullOrWhiteSpace(analysis.CanonicalResumeJson)
            ? "{}"
            : analysis.CanonicalResumeJson;
    }

    private async Task<string> SendPollinationsAsync(
        Guid userId,
        IReadOnlyCollection<AiMessage> conversationMessages,
        CancellationToken cancellationToken,
        string? modelOverride = null)
    {
        var apiKey = await providerKeyService.GetActivePollinationsKeyAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderKeyMissingException("Configure o token Pollinations antes de melhorar curriculos.");
        }

        var messages = new List<AiMessage>
        {
            new("system", SystemPrompt)
        };
        messages.AddRange(conversationMessages);

        var model = modelOverride ?? GetPollinationsModel();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(new AiChatCompletionRequest(model, messages, 0.25m), options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new PollinationsAuthenticationException("Pollinations recusou o token informado.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var providerError = await response.Content.ReadAsStringAsync(cancellationToken);
            providerError = Regex.Replace(providerError, @"\s+", " ").Trim();
            var detail = string.IsNullOrWhiteSpace(providerError)
                ? response.StatusCode.ToString()
                : TrimTo(providerError, 600);
            throw new PollinationsProviderException($"Pollinations falhou ao melhorar o curriculo. Detalhe: {detail}");
        }

        var completion = await response.Content.ReadFromJsonAsync<AiChatCompletionResponse>(JsonOptions, cancellationToken);
        var message = completion?.Choices.FirstOrDefault()?.Message.Content;
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new PollinationsProviderException("Pollinations retornou uma resposta vazia.");
        }

        await providerKeyService.MarkPollinationsKeyUsedAsync(userId, cancellationToken);
        return message;
    }

    private string GetPollinationsModel()
    {
        return configuration["POLLINATIONS_MODEL"] ?? configuration["Pollinations:Model"] ?? "gpt-5.4";
    }

    private static string ExtractResumeText(ResumeFileInput file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return extension switch
        {
            ".docx" => ExtractDocxText(file.Content),
            ".pdf" => ExtractPdfText(file.Content),
            _ => throw new InvalidOperationException("Envie um curriculo em PDF ou DOCX.")
        };
    }

    private static string ExtractDocxText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        return string.Join(
            "\n",
            body.Descendants<Paragraph>()
                .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)).Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string ExtractPdfText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = PdfDocument.Open(stream);
        return string.Join("\n", document.GetPages().Select(page => page.Text));
    }

    private static ParsedResumeResponse ParseResponse(string response)
    {
        var match = OptimizedResumeRegex().Match(response);
        if (!match.Success)
        {
            return new ParsedResumeResponse(
                NormalizeAssistantMessage(
                    response.Trim(),
                    "Nao recebi uma versao completa do curriculo nesta resposta. Mantive a versao atual sem substituir o texto."),
                string.Empty,
                false);
        }

        var optimized = match.Groups["content"].Value.Trim();
        var message = OptimizedResumeRegex().Replace(response, string.Empty).Trim();
        if (!LooksLikeCompleteResume(optimized))
        {
            return new ParsedResumeResponse(
                NormalizeAssistantMessage(
                    message,
                    "A resposta do provedor veio incompleta e nao substitui o curriculo atual. Tente otimizar novamente em alguns instantes."),
                string.Empty,
                false);
        }

        message = NormalizeAssistantMessage(
            message,
            "Preparei uma versao otimizada do curriculo com base no diagnostico enviado. Revise abaixo e me diga se deseja ajustar algum ponto.");

        return new ParsedResumeResponse(message, optimized, true);
    }

    private static string NormalizeAssistantMessage(string message, string fallback)
    {
        message = Regex.Replace(message ?? string.Empty, @"\s+", " ").Trim();
        if (message.Length < 12 || WordRegex().Matches(message).Count < 3)
        {
            return fallback;
        }

        return message;
    }

    private static bool LooksLikeCompleteResume(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var words = WordRegex().Matches(value).Count;
        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;

        return value.Trim().Length >= 180 && words >= 30 && lines >= 4;
    }

    private static string TrimContext(string value)
    {
        value = value.Trim();
        return value.Length <= MaxResumeCharacters ? value : value[..MaxResumeCharacters];
    }

    private static object TextPart(string text)
    {
        return new { type = "text", text };
    }

    private static object ImagePart(ResumeFileInput image)
    {
        var contentType = string.IsNullOrWhiteSpace(image.ContentType) ? GuessImageContentType(image.FileName) : image.ContentType;
        var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(image.Content)}";
        return new
        {
            type = "image_url",
            image_url = new
            {
                url = dataUrl
            }
        };
    }

    private static bool IsSupportedImage(ResumeFileInput file)
    {
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? GuessImageContentType(file.FileName) : file.ContentType;
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static string GuessImageContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    private static byte[] CreateDocx(string content)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            foreach (var line in ParseFormattedLines(content))
            {
                body.AppendChild(CreateParagraph(line));
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateParagraph(FormattedResumeLine line)
    {
        var paragraph = new Paragraph();
        var properties = new ParagraphProperties();

        if (line.Kind == ResumeLineKind.Blank)
        {
            paragraph.Append(properties);
            return paragraph;
        }

        if (line.Kind == ResumeLineKind.Heading1 || line.Kind == ResumeLineKind.Heading2)
        {
            var fontSize = line.Kind == ResumeLineKind.Heading1 ? "28" : "24";
            properties.Append(new SpacingBetweenLines { Before = "240", After = "100" });
            paragraph.Append(properties);
            AppendFormattedRuns(paragraph, CleanHeading(line.Text), true, fontSize);
            return paragraph;
        }

        if (line.Kind == ResumeLineKind.Bullet)
        {
            paragraph.Append(properties);
            AppendFormattedRuns(paragraph, line.Text, false, null, "\u2022 ");
            return paragraph;
        }

        paragraph.Append(properties);
        AppendFormattedRuns(paragraph, line.Text, false, null);
        return paragraph;
    }

    private static byte[] CreatePdf(string content)
    {
        var document = new MdDocument();
        document.Info.Title = "Curriculo otimizado";
        ConfigurePdfDocument(document);

        var section = document.AddSection();
        section.PageSetup.PageFormat = MdPageFormat.A4;
        section.PageSetup.TopMargin = MdUnit.FromCentimeter(1.7);
        section.PageSetup.BottomMargin = MdUnit.FromCentimeter(1.7);
        section.PageSetup.LeftMargin = MdUnit.FromCentimeter(1.8);
        section.PageSetup.RightMargin = MdUnit.FromCentimeter(1.8);

        foreach (var line in ParseFormattedLines(content))
        {
            AppendPdfParagraph(section, line);
        }

        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void ConfigurePdfDocument(MdDocument document)
    {
        var normal = document.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 10.5;
        normal.ParagraphFormat.SpaceAfter = MdUnit.FromPoint(5);

        var heading1 = document.Styles.AddStyle("ResumeHeading1", "Normal");
        heading1.Font.Size = 16;
        heading1.Font.Bold = true;
        heading1.ParagraphFormat.SpaceBefore = MdUnit.FromPoint(10);
        heading1.ParagraphFormat.SpaceAfter = MdUnit.FromPoint(6);
        heading1.ParagraphFormat.KeepWithNext = true;

        var heading2 = document.Styles.AddStyle("ResumeHeading2", "Normal");
        heading2.Font.Size = 13;
        heading2.Font.Bold = true;
        heading2.ParagraphFormat.SpaceBefore = MdUnit.FromPoint(8);
        heading2.ParagraphFormat.SpaceAfter = MdUnit.FromPoint(4);
        heading2.ParagraphFormat.KeepWithNext = true;

        var list = document.Styles.AddStyle("ResumeList", "Normal");
        list.ParagraphFormat.LeftIndent = MdUnit.FromCentimeter(0.45);
        list.ParagraphFormat.FirstLineIndent = MdUnit.FromCentimeter(-0.25);
        list.ParagraphFormat.SpaceAfter = MdUnit.FromPoint(3);
    }

    private static void AppendPdfParagraph(MdSection section, FormattedResumeLine line)
    {
        var paragraph = section.AddParagraph();
        switch (line.Kind)
        {
            case ResumeLineKind.Heading1:
                paragraph.Style = "ResumeHeading1";
                AppendPdfFormattedText(paragraph, CleanHeading(line.Text), true);
                break;
            case ResumeLineKind.Heading2:
                paragraph.Style = "ResumeHeading2";
                AppendPdfFormattedText(paragraph, CleanHeading(line.Text), true);
                break;
            case ResumeLineKind.Bullet:
                paragraph.Style = "ResumeList";
                AppendPdfFormattedText(paragraph, line.Text, false, "\u2022 ");
                break;
            case ResumeLineKind.Numbered:
                paragraph.Style = "ResumeList";
                AppendPdfFormattedText(paragraph, line.Text, false);
                break;
            case ResumeLineKind.Paragraph:
                AppendPdfFormattedText(paragraph, line.Text, false);
                break;
            default:
                break;
        }
    }

    private static void AppendPdfFormattedText(MdParagraph paragraph, string text, bool boldAll, string prefix = "")
    {
        if (!string.IsNullOrEmpty(prefix))
        {
            if (boldAll)
            {
                paragraph.AddFormattedText(prefix, MdTextFormat.Bold);
            }
            else
            {
                paragraph.AddText(prefix);
            }
        }

        var currentIndex = 0;
        foreach (Match match in BoldTextRegex().Matches(text))
        {
            if (match.Index > currentIndex)
            {
                AddPdfTextRun(paragraph, CleanInlineMarkdown(text[currentIndex..match.Index]), boldAll);
            }

            AddPdfTextRun(paragraph, CleanInlineMarkdown(match.Groups["value"].Value), true);
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            AddPdfTextRun(paragraph, CleanInlineMarkdown(text[currentIndex..]), boldAll);
        }
    }

    private static void AddPdfTextRun(MdParagraph paragraph, string text, bool bold)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (bold)
        {
            paragraph.AddFormattedText(text, MdTextFormat.Bold);
        }
        else
        {
            paragraph.AddText(text);
        }
    }

    private static IReadOnlyList<string> BuildPdfPageStreams(IReadOnlyCollection<FormattedResumeLine> formattedLines)
    {
        const decimal pageTop = 760m;
        const decimal pageBottom = 50m;
        const decimal leftMargin = 50m;
        var pages = new List<string>();
        var commands = new StringBuilder();
        var y = pageTop;

        foreach (var line in formattedLines)
        {
            var style = GetPdfStyle(line);
            y -= style.Before;

            var wrappedLines = WrapPdfSegments(BuildPdfSegments(line, style), style.MaxCharacters).ToArray();
            foreach (var wrappedLine in wrappedLines)
            {
                if (y - style.LineHeight < pageBottom)
                {
                    pages.Add(commands.ToString());
                    commands.Clear();
                    y = pageTop;
                }

                AppendPdfTextLine(commands, leftMargin + style.LeftIndent, y, style.FontSize, wrappedLine);
                y -= style.LineHeight;
            }

            y -= style.After;
        }

        if (commands.Length > 0 || pages.Count == 0)
        {
            pages.Add(commands.ToString());
        }

        return pages;

        static PdfLineStyle GetPdfStyle(FormattedResumeLine line)
        {
            return line.Kind switch
            {
                ResumeLineKind.Heading1 => new PdfLineStyle(16m, 22m, 10m, 6m, 0m, true, string.Empty, 58),
                ResumeLineKind.Heading2 => new PdfLineStyle(13m, 18m, 8m, 4m, 0m, true, string.Empty, 70),
                ResumeLineKind.Bullet => new PdfLineStyle(11m, 15m, 2m, 2m, 18m, false, "- ", 78),
                ResumeLineKind.Numbered => new PdfLineStyle(11m, 15m, 2m, 2m, 18m, false, string.Empty, 78),
                _ => new PdfLineStyle(11m, 15m, 2m, 4m, 0m, false, string.Empty, 88)
            };
        }
    }

    private static IReadOnlyList<PdfTextSegment> BuildPdfSegments(FormattedResumeLine line, PdfLineStyle style)
    {
        var segments = new List<PdfTextSegment>();
        if (!string.IsNullOrWhiteSpace(style.Prefix))
        {
            segments.Add(new PdfTextSegment(style.Prefix, false));
        }

        var text = line.Kind is ResumeLineKind.Heading1 or ResumeLineKind.Heading2
            ? CleanHeading(line.Text)
            : line.Text;
        var currentIndex = 0;
        foreach (Match match in BoldTextRegex().Matches(text))
        {
            if (match.Index > currentIndex)
            {
                segments.Add(new PdfTextSegment(CleanInlineMarkdown(text[currentIndex..match.Index]), style.Bold));
            }

            segments.Add(new PdfTextSegment(CleanInlineMarkdown(match.Groups["value"].Value), true));
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            segments.Add(new PdfTextSegment(CleanInlineMarkdown(text[currentIndex..]), style.Bold));
        }

        return segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment.Text))
            .ToArray();
    }

    private static IEnumerable<IReadOnlyList<PdfTextSegment>> WrapPdfSegments(IReadOnlyList<PdfTextSegment> segments, int maxCharacters)
    {
        var current = new List<PdfTextSegment>();
        var currentLength = 0;

        foreach (var segment in segments)
        {
            var words = segment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var token = currentLength == 0 ? word : " " + word;
                if (currentLength > 0 && currentLength + token.Length > maxCharacters)
                {
                    yield return current;
                    current = new List<PdfTextSegment>();
                    currentLength = 0;
                    token = word;
                }

                current.Add(new PdfTextSegment(token, segment.Bold));
                currentLength += token.Length;
            }
        }

        if (current.Count > 0)
        {
            yield return current;
        }
    }

    private static void AppendPdfTextLine(
        StringBuilder commands,
        decimal x,
        decimal y,
        decimal fontSize,
        IReadOnlyList<PdfTextSegment> segments)
    {
        var currentX = x;
        foreach (var segment in segments)
        {
            var text = RemovePdfUnsafeCharacters(segment.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            commands.Append(CultureInfo.InvariantCulture, $"BT /{(segment.Bold ? "F2" : "F1")} {fontSize:0.##} Tf {currentX:0.##} {y:0.##} Td ");
            commands.Append('(').Append(EscapePdf(text)).AppendLine(") Tj ET");
            currentX += EstimatePdfTextWidth(text, fontSize, segment.Bold);
        }
    }

    private static decimal EstimatePdfTextWidth(string text, decimal fontSize, bool bold)
    {
        var factor = bold ? 0.56m : 0.52m;
        return text.Length * fontSize * factor;
    }

    private static IReadOnlyCollection<FormattedResumeLine> ParseFormattedLines(string content)
    {
        var lines = StripResumeMarkers(content)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(ParseFormattedLine)
            .Where(line => line.Kind != ResumeLineKind.Blank)
            .ToArray();

        return lines.Length > 0
            ? lines
            : [new FormattedResumeLine(ResumeLineKind.Paragraph, CleanInlineMarkdown(content))];
    }

    private static FormattedResumeLine ParseFormattedLine(string rawLine)
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("```", StringComparison.Ordinal))
        {
            return new FormattedResumeLine(ResumeLineKind.Blank, string.Empty);
        }

        var headingMatch = MarkdownHeadingRegex().Match(line);
        if (headingMatch.Success)
        {
            var level = headingMatch.Groups["level"].Value.Length;
            return new FormattedResumeLine(
                level == 1 ? ResumeLineKind.Heading1 : ResumeLineKind.Heading2,
                CleanInlineMarkdown(headingMatch.Groups["text"].Value));
        }

        var bulletMatch = BulletListRegex().Match(line);
        if (bulletMatch.Success)
        {
            return new FormattedResumeLine(ResumeLineKind.Bullet, bulletMatch.Groups["text"].Value.Trim());
        }

        var numberedMatch = NumberedListRegex().Match(line);
        if (numberedMatch.Success)
        {
            return new FormattedResumeLine(
                ResumeLineKind.Numbered,
                $"{numberedMatch.Groups["number"].Value} {numberedMatch.Groups["text"].Value.Trim()}");
        }

        if (IsHeading(line))
        {
            return new FormattedResumeLine(ResumeLineKind.Heading2, CleanHeading(line));
        }

        return new FormattedResumeLine(ResumeLineKind.Paragraph, line);
    }

    private static IEnumerable<string> ToPdfLines(FormattedResumeLine line)
    {
        var text = line.Kind switch
        {
            ResumeLineKind.Heading1 => CleanInlineMarkdown(line.Text).ToUpperInvariant(),
            ResumeLineKind.Heading2 => CleanHeading(line.Text),
            ResumeLineKind.Bullet => "- " + CleanInlineMarkdown(line.Text),
            _ => CleanInlineMarkdown(line.Text)
        };

        return WrapPdfLine(text);
    }

    private static IEnumerable<string> WrapPdfLine(string line)
    {
        const int width = 88;
        if (line.Length <= width)
        {
            yield return RemovePdfUnsafeCharacters(line);
            yield break;
        }

        for (var index = 0; index < line.Length; index += width)
        {
            yield return RemovePdfUnsafeCharacters(line[index..Math.Min(line.Length, index + width)]);
        }
    }

    private static string RemovePdfUnsafeCharacters(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && character <= 127)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string EscapePdf(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static bool IsHeading(string line)
    {
        var cleanLine = CleanInlineMarkdown(line);
        return line.Length <= 80
            && !line.StartsWith("- ", StringComparison.Ordinal)
            && !line.StartsWith("* ", StringComparison.Ordinal)
            && (cleanLine.All(character => !char.IsLetter(character) || char.IsUpper(character))
                || cleanLine.EndsWith(':'));
    }

    private static string CleanHeading(string line)
    {
        return CleanInlineMarkdown(line.Trim().TrimStart('#').Trim().TrimEnd(':'));
    }

    private static string StripResumeMarkers(string content)
    {
        var optimizedMatch = OptimizedResumeRegex().Match(content);
        if (optimizedMatch.Success)
        {
            return optimizedMatch.Groups["content"].Value;
        }

        return content
            .Replace("[CURRICULO_OTIMIZADO]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[/CURRICULO_OTIMIZADO]", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanInlineMarkdown(string value)
    {
        return value
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
    }

    private static void AppendFormattedRuns(Paragraph paragraph, string text, bool boldAll, string? fontSize, string prefix = "")
    {
        if (!string.IsNullOrEmpty(prefix))
        {
            paragraph.Append(CreateRun(prefix, boldAll, fontSize));
        }

        var currentIndex = 0;
        foreach (Match match in BoldTextRegex().Matches(text))
        {
            if (match.Index > currentIndex)
            {
                paragraph.Append(CreateRun(CleanInlineMarkdown(text[currentIndex..match.Index]), boldAll, fontSize));
            }

            paragraph.Append(CreateRun(CleanInlineMarkdown(match.Groups["value"].Value), true, fontSize));
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < text.Length)
        {
            paragraph.Append(CreateRun(CleanInlineMarkdown(text[currentIndex..]), boldAll, fontSize));
        }
    }

    private static Run CreateRun(string text, bool bold, string? fontSize)
    {
        var properties = new RunProperties();
        if (bold)
        {
            properties.Append(new Bold());
        }

        if (!string.IsNullOrWhiteSpace(fontSize))
        {
            properties.Append(new FontSize { Val = fontSize });
        }

        return new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private enum ResumeLineKind
    {
        Blank,
        Heading1,
        Heading2,
        Bullet,
        Numbered,
        Paragraph
    }

    private sealed record FormattedResumeLine(ResumeLineKind Kind, string Text);

    private sealed record PdfLineStyle(
        decimal FontSize,
        decimal LineHeight,
        decimal Before,
        decimal After,
        decimal LeftIndent,
        bool Bold,
        string Prefix,
        int MaxCharacters);

    private sealed record PdfTextSegment(string Text, bool Bold);

    private sealed record AdditionalResumeContext(string Source, string Content);

    private sealed record RagChunk(string Source, int ChunkIndex, string Content, string Keywords);
    private sealed record ParsedResumeResponse(string Message, string OptimizedResume, bool ReadyToExport);
    private sealed record AiChatCompletionRequest(string Model, IReadOnlyCollection<AiMessage> Messages, decimal Temperature);
    private sealed record AiMessage(string Role, object Content);

    private sealed class CvMirrorSettings
    {
        public bool Enabled { get; private init; }
        public string Command { get; private init; } = "node";
        public IReadOnlyCollection<string> Arguments { get; private init; } =
        [
            "--input-type=module",
            "-e",
            "import { analyzeCv } from './src/lint.mjs'; const report = await analyzeCv(process.argv[1]); console.log(JSON.stringify(report));"
        ];
        public string? WorkingDirectory { get; private init; } = "external/cv-mirror-mcp";
        public int TimeoutSeconds { get; private init; } = 45;

        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));

        public static CvMirrorSettings FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("ResumeImprovements:CvMirror");
            return new CvMirrorSettings
            {
                Enabled = !bool.TryParse(section["Enabled"], out var enabled) || enabled,
                Command = string.IsNullOrWhiteSpace(section["Command"]) ? "node" : section["Command"]!,
                Arguments = section.GetSection("Arguments").Get<string[]>()
                    ?? [
                        "--input-type=module",
                        "-e",
                        "import { analyzeCv } from './src/lint.mjs'; const report = await analyzeCv(process.argv[1]); console.log(JSON.stringify(report));"
                    ],
                WorkingDirectory = string.IsNullOrWhiteSpace(section["WorkingDirectory"])
                    ? "external/cv-mirror-mcp"
                    : section["WorkingDirectory"],
                TimeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : 45
            };
        }

        public string? ResolveWorkingDirectory()
        {
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                return null;
            }

            if (Path.IsPathRooted(WorkingDirectory))
            {
                return Directory.Exists(WorkingDirectory) ? WorkingDirectory : null;
            }

            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current is not null)
            {
                var candidate = Path.GetFullPath(Path.Combine(current.FullName, WorkingDirectory));
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return null;
        }
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string ReadNullable(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "n/a";
        }

        return value.ToString();
    }

    private sealed class AiChatCompletionResponse
    {
        public List<AiChoice> Choices { get; set; } = new();
    }

    private sealed class AiChoice
    {
        public AiChoiceMessage Message { get; set; } = new();
    }

    private sealed class AiChoiceMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    [GeneratedRegex(@"\[CURRICULO_OTIMIZADO\](?<content>[\s\S]*?)\[/CURRICULO_OTIMIZADO\]", RegexOptions.IgnoreCase)]
    private static partial Regex OptimizedResumeRegex();

    [GeneratedRegex(@"^(?<level>#{1,6})\s+(?<text>.+)$")]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"^[-*]\s+(?<text>.+)$")]
    private static partial Regex BulletListRegex();

    [GeneratedRegex(@"^(?<number>\d+[\.)])\s+(?<text>.+)$")]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"\*\*(?<value>.+?)\*\*")]
    private static partial Regex BoldTextRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
