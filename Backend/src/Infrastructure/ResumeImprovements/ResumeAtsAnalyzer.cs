using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lessie.Application.ResumeImprovements;

namespace Lessie.Infrastructure.ResumeImprovements;

internal sealed partial class ResumeAtsAnalyzer : IResumeAtsAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private static readonly string[] ImpactSignals =
    [
        "%", "percentual", "reduziu", "aumentou", "economia", "receita", "performance", "latencia",
        "tempo", "custo", "produtividade", "throughput", "erros", "sla", "usuarios", "transacoes"
    ];

    private static readonly string[] ActionVerbs =
    [
        "desenvolvi", "implementei", "liderei", "automatizei", "integrei", "otimizei", "reduzi",
        "aumentei", "criei", "modelei", "migrei", "entreguei", "coordenei", "estruturei"
    ];

    private static readonly string[] TechnicalSignals =
    [
        "c#", ".net", "java", "python", "javascript", "typescript", "angular", "react", "vue",
        "sql", "postgres", "mysql", "mongodb", "aws", "azure", "docker", "kubernetes", "api",
        "rest", "graphql", "microservicos", "ci/cd", "git", "redis", "mensageria", "observabilidade",
        "arquitetura", "seguranca", "cloud", "sql server", "devops", "scrum", "agile"
    ];

    private static readonly Dictionary<string, string[]> KeywordSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        [".net"] = ["dotnet", "asp.net", "net core", "net framework"],
        ["c#"] = ["csharp"],
        ["javascript"] = ["js"],
        ["typescript"] = ["ts"],
        ["sql server"] = ["mssql", "t-sql", "sqlserver"],
        ["postgres"] = ["postgresql"],
        ["kubernetes"] = ["k8s"],
        ["aws"] = ["amazon web services"],
        ["ci/cd"] = ["devops", "pipeline", "continuous integration", "continuous delivery"],
        ["mensageria"] = ["rabbitmq", "kafka", "sqs", "pub/sub"],
        ["microservicos"] = ["microservices"],
        ["observabilidade"] = ["monitoramento", "logs", "tracing", "metrics", "grafana", "prometheus"]
    };

    private static readonly Dictionary<string, string[]> JobSearchKeywordSignals = new(StringComparer.OrdinalIgnoreCase)
    {
        [".NET"] = [".net", "dotnet", "asp.net"],
        ["C#"] = ["c#", "csharp"],
        ["ASP.NET"] = ["asp.net"],
        ["Web API"] = ["web api", "api rest", "rest"],
        ["SQL Server"] = ["sql server", "mssql", "t-sql"],
        ["Angular"] = ["angular"],
        ["TypeScript"] = ["typescript"],
        ["JavaScript"] = ["javascript"],
        ["AWS"] = ["aws", "amazon web services"],
        ["Azure"] = ["azure"],
        ["Docker"] = ["docker"],
        ["Kubernetes"] = ["kubernetes", "k8s"],
        ["CI/CD"] = ["ci/cd", "pipeline", "devops"],
        ["Microservicos"] = ["microservicos", "microservices"],
        ["Sistemas financeiros"] = ["sistemas financeiros", "financeiro", "financeira", "bancario", "bancaria"],
        ["Meios de pagamento"] = ["meios de pagamento", "pagamento", "pagamentos"],
        ["PIX"] = ["pix"],
        ["Boletos"] = ["boleto", "boletos"],
        ["OCR"] = ["ocr"],
        ["RPA"] = ["rpa"],
        ["APIs REST"] = ["api rest", "apis rest", "rest api"],
        ["Full Stack"] = ["full stack", "fullstack"],
        ["Tech Lead"] = ["tech lead", "lead"],
        ["Desenvolvedor Senior"] = ["senior", "sênior"]
    };

    public ResumeAtsAnalysis Analyze(string resumeText, string jobContext = "")
    {
        var normalizedResume = Normalize(resumeText);
        var normalizedJob = Normalize(jobContext);
        var canonicalResume = BuildCanonicalResume(resumeText);
        var keywordMatch = BuildKeywordMatch(normalizedResume, normalizedJob);
        var requirementCoverage = BuildRequirementCoverage(normalizedResume, jobContext);
        var keywordStrategy = BuildKeywordStrategy(keywordMatch, requirementCoverage, normalizedJob);
        var sections = new List<ResumeAtsSectionScore>
        {
            ScoreContact(normalizedResume),
            ScoreSummary(normalizedResume),
            ScoreExperience(resumeText, normalizedResume),
            ScoreImpact(resumeText, normalizedResume),
            ScoreSkills(normalizedResume),
            ScoreJobMatch(normalizedResume, normalizedJob),
            ScoreAtsFormat(resumeText)
        };
        var subscores = BuildSubscores(sections);
        var overallScore = subscores.Count == 0
            ? Clamp((int)Math.Round(sections.Average(section => section.Score)))
            : Clamp(subscores.Values.Sum());

        var recommendations = sections
            .SelectMany(section => section.Recommendations)
            .Concat(keywordMatch.Missing.Take(5).Select(keyword => $"Se for verdadeiro, incorpore a palavra-chave da vaga de forma natural: {keyword}."))
            .Concat(requirementCoverage
                .Where(item => item.Status != "coberto")
                .Take(3)
                .Select(item => item.Recommendation))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        var strengths = sections
            .Where(section => section.Score >= 80)
            .Select(section => $"{section.Name}: {section.Summary}")
            .Take(5)
            .ToList();
        var risks = sections
            .Where(section => section.Score < 70)
            .Select(section => $"{section.Name}: {section.Summary}")
            .Concat(BuildCriticalGaps(keywordMatch, requirementCoverage))
            .Take(5)
            .ToList();

        return new ResumeAtsAnalysis
        {
            Provider = "ATS Resume MCP",
            OverallScore = overallScore,
            AnalyzedAt = DateTimeOffset.UtcNow,
            Sections = sections,
            Strengths = strengths,
            Risks = risks,
            Recommendations = recommendations,
            KeywordsPresent = keywordMatch.Present,
            KeywordsMissing = keywordMatch.Missing,
            KeywordsPartial = keywordMatch.Partial,
            JobSearchKeywords = BuildJobSearchKeywords(normalizedResume),
            CriticalGaps = risks,
            MatchRecommendation = overallScore >= 70 ? "otimizar" : "desenvolver gaps antes de prometer aderencia total",
            Subscores = subscores,
            RequirementCoverage = requirementCoverage,
            KeywordStrategy = keywordStrategy,
            CanonicalResumeJson = JsonSerializer.Serialize(canonicalResume, JsonOptions)
        };
    }

    private static ResumeAtsSectionScore ScoreContact(string text)
    {
        var score = 35;
        var recommendations = new List<string>();
        if (EmailRegex().IsMatch(text)) score += 25; else recommendations.Add("Inclua um e-mail profissional no cabecalho.");
        if (PhoneRegex().IsMatch(text)) score += 20; else recommendations.Add("Inclua telefone com DDD em formato simples.");
        if (text.Contains("linkedin.com/in", StringComparison.OrdinalIgnoreCase)) score += 10; else recommendations.Add("Inclua URL publica do LinkedIn.");
        if (text.Contains("github.com", StringComparison.OrdinalIgnoreCase)) score += 10;

        return Section("Contato", score, "Dados basicos de contato e links profissionais.", recommendations);
    }

    private static ResumeAtsSectionScore ScoreSummary(string text)
    {
        var hasSummary = ContainsAny(text, "resumo", "perfil profissional", "objetivo", "summary");
        var hasSeniority = ContainsAny(text, "senior", "pleno", "junior", "lead", "especialista", "arquiteto");
        var hasDomain = ContainsAny(text, "financeiro", "pagamento", "credito", "banco", "saude", "varejo", "industria", "seguros");
        var score = 35 + (hasSummary ? 30 : 0) + (hasSeniority ? 15 : 0) + (hasDomain ? 20 : 0);
        var recommendations = new List<string>();
        if (!hasSummary) recommendations.Add("Adicione um resumo profissional curto no inicio.");
        if (!hasSeniority) recommendations.Add("Declare senioridade ou posicionamento profissional com clareza.");
        if (!hasDomain) recommendations.Add("Inclua o dominio de negocio mais forte quando ele for relevante para a vaga.");

        return Section("Resumo", score, "Clareza do posicionamento profissional no inicio do curriculo.", recommendations);
    }

    private static ResumeAtsSectionScore ScoreExperience(string rawText, string text)
    {
        var hasExperienceSection = ContainsAny(text, "experiencia", "experiencias", "historico profissional", "work experience");
        var bulletCount = rawText.Split('\n').Count(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal) || line.TrimStart().StartsWith("* ", StringComparison.Ordinal));
        var actionVerbCount = ActionVerbs.Count(verb => text.Contains(verb, StringComparison.OrdinalIgnoreCase));
        var score = 25 + (hasExperienceSection ? 25 : 0) + Math.Min(25, bulletCount * 3) + Math.Min(25, actionVerbCount * 5);
        var recommendations = new List<string>();
        if (!hasExperienceSection) recommendations.Add("Use uma secao clara de Experiencia Profissional.");
        if (bulletCount < 6) recommendations.Add("Transforme responsabilidades em bullets objetivos por cargo/projeto.");
        if (actionVerbCount < 3) recommendations.Add("Comece bullets com verbos de acao como implementei, otimizei, liderei ou automatizei.");

        return Section("Experiencia", score, "Estrutura das experiencias e uso de bullets orientados a acao.", recommendations);
    }

    private static ResumeAtsSectionScore ScoreImpact(string rawText, string text)
    {
        var impactCount = ImpactSignals.Count(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
        var numberCount = NumberRegex().Matches(rawText).Count;
        var score = 25 + Math.Min(35, impactCount * 5) + Math.Min(40, numberCount * 5);
        var recommendations = new List<string>();
        if (impactCount < 3) recommendations.Add("Inclua impacto atingido nos bullets: reducao de tempo, erros, custos, throughput ou escala.");
        if (numberCount < 3) recommendations.Add("Quando souber, acrescente numeros reais ou faixas aproximadas de impacto.");

        return Section("Impacto", score, "Evidencias de resultado, escala e impacto mensuravel.", recommendations);
    }

    private static ResumeAtsSectionScore ScoreSkills(string text)
    {
        var technicalCount = TechnicalSignals.Count(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
        var hasSkillsSection = ContainsAny(text, "competencias", "habilidades", "skills", "tecnologias");
        var score = 30 + (hasSkillsSection ? 25 : 0) + Math.Min(45, technicalCount * 5);
        var recommendations = new List<string>();
        if (!hasSkillsSection) recommendations.Add("Crie uma secao de competencias/tecnologias facil de parsear por ATS.");
        if (technicalCount < 6) recommendations.Add("Liste tecnologias chave de forma literal, sem depender apenas de descricoes longas.");

        return Section("Competencias", score, "Presenca de palavras-chave tecnicas e secao facil de parsear.", recommendations);
    }

    private static ResumeAtsSectionScore ScoreJobMatch(string resume, string job)
    {
        if (string.IsNullOrWhiteSpace(job))
        {
            return Section(
                "Aderencia a vaga",
                60,
                "Nenhuma vaga especifica foi informada para comparar palavras-chave.",
                ["Cole o texto da vaga ou anexe prints para medir aderencia especifica."]);
        }

        var jobKeywords = ExtractKeywords(job).Take(40).ToArray();
        var matched = jobKeywords.Count(keyword => resume.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        var score = jobKeywords.Length == 0 ? 60 : 35 + (int)Math.Round(65m * matched / jobKeywords.Length);
        var missing = jobKeywords
            .Where(keyword => !resume.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToArray();
        var recommendations = missing.Length == 0
            ? new List<string>()
            : [$"Considere evidenciar palavras-chave da vaga se forem verdadeiras: {string.Join(", ", missing)}."];

        return Section("Aderencia a vaga", score, $"{matched} de {jobKeywords.Length} palavras-chave relevantes aparecem no curriculo.", recommendations);
    }

    private static ResumeAtsSectionScore ScoreAtsFormat(string rawText)
    {
        var score = 90;
        var recommendations = new List<string>();
        if (rawText.Contains('|')) { score -= 10; recommendations.Add("Evite excesso de separadores verticais no corpo do curriculo."); }
        if (rawText.Contains("```")) { score -= 20; recommendations.Add("Remova marcadores de codigo/markdown antes de exportar."); }
        if (rawText.Contains("![", StringComparison.Ordinal)) { score -= 20; recommendations.Add("Evite imagens dentro do curriculo ATS."); }
        if (rawText.Split('\n').Any(line => line.Length > 180)) { score -= 10; recommendations.Add("Quebre linhas muito longas em bullets menores."); }

        return Section("Formato ATS", score, "Risco local de parse por ATS comuns.", recommendations);
    }

    private static ResumeAtsSectionScore Section(string name, int score, string summary, List<string> recommendations)
    {
        var boundedScore = Clamp(score);
        var status = boundedScore >= 80 ? "bom" : boundedScore >= 65 ? "atencao" : "critico";
        return new ResumeAtsSectionScore
        {
            Name = name,
            Score = boundedScore,
            Status = status,
            Summary = summary,
            Recommendations = recommendations
        };
    }

    private static KeywordMatchResult BuildKeywordMatch(string resume, string job)
    {
        if (string.IsNullOrWhiteSpace(job))
        {
            return new KeywordMatchResult([], [], []);
        }

        var keywords = ExtractJobKeywords(job).Take(50).ToArray();
        var present = new List<string>();
        var partial = new List<string>();
        var missing = new List<string>();

        foreach (var keyword in keywords)
        {
            if (KeywordExists(resume, keyword))
            {
                present.Add(keyword);
            }
            else if (KeywordPartiallyExists(resume, keyword))
            {
                partial.Add(keyword);
            }
            else
            {
                missing.Add(keyword);
            }
        }

        return new KeywordMatchResult(
            present.Take(16).ToList(),
            missing.Take(16).ToList(),
            partial.Take(10).ToList());
    }

    private static List<ResumeAtsKeywordStrategy> BuildKeywordStrategy(
        KeywordMatchResult keywordMatch,
        IReadOnlyCollection<ResumeAtsRequirementCoverage> requirementCoverage,
        string normalizedJob)
    {
        var allKeywords = keywordMatch.Present
            .Concat(keywordMatch.Partial)
            .Concat(keywordMatch.Missing)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var strategy = new List<ResumeAtsKeywordStrategy>();
        AddKeywordGroup(
            strategy,
            "Obrigatorias",
            "Resumo e Experiencia",
            ExtractRequirementKeywords(requirementCoverage, "obrigatorio", "requisito", "necessario", "precisa", "deve", "mandatorio")
                .Concat(keywordMatch.Missing.Take(4))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8),
            "Se houver evidencia real, espelhe essas palavras no resumo e em bullets de experiencia com acao + impacto.");

        AddKeywordGroup(
            strategy,
            "Tecnicas",
            "Competencias",
            allKeywords.Where(IsTechnicalKeyword).Take(12),
            "Liste literalmente em Competencias e reforce nos bullets apenas quando houver uso real.");

        AddKeywordGroup(
            strategy,
            "Responsabilidades",
            "Experiencia",
            allKeywords.Where(IsResponsibilityKeyword).Take(10),
            "Converta em bullets de trabalho feito, conectando escopo, tecnologia e resultado.");

        AddKeywordGroup(
            strategy,
            "Dominio",
            "Resumo",
            allKeywords.Where(IsDomainKeyword).Take(8),
            "Use no posicionamento inicial para mostrar aderencia de negocio sem repetir demais.");

        AddKeywordGroup(
            strategy,
            "Senioridade",
            "Titulo e Resumo",
            allKeywords.Where(IsSeniorityKeyword)
                .Concat(ExtractKeywords(normalizedJob).Where(IsSeniorityKeyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5),
            "Ajuste o titulo profissional e o resumo para refletir senioridade real.");

        AddKeywordGroup(
            strategy,
            "Desejaveis",
            "Competencias ou Projetos",
            ExtractRequirementKeywords(requirementCoverage, "desejavel", "diferencial", "plus", "preferencial").Take(8),
            "Inclua somente se houver evidencia; caso contrario, mantenha fora do curriculo e trate como plano de desenvolvimento.");

        return strategy;
    }

    private static List<string> BuildJobSearchKeywords(string normalizedResume)
    {
        return JobSearchKeywordSignals
            .Select(item => new
            {
                Keyword = item.Key,
                Score = item.Value.Count(signal => normalizedResume.Contains(Normalize(signal), StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Keyword)
            .Select(item => item.Keyword)
            .Take(18)
            .ToList();
    }

    private static void AddKeywordGroup(
        ICollection<ResumeAtsKeywordStrategy> strategy,
        string group,
        string targetSection,
        IEnumerable<string> keywords,
        string instruction)
    {
        var selected = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        if (selected.Count == 0)
        {
            return;
        }

        strategy.Add(new ResumeAtsKeywordStrategy
        {
            Group = group,
            TargetSection = targetSection,
            Keywords = selected,
            Instruction = instruction
        });
    }

    private static Dictionary<string, int> BuildSubscores(IReadOnlyCollection<ResumeAtsSectionScore> sections)
    {
        var skills = ScoreOf(sections, "Competencias");
        var job = ScoreOf(sections, "Aderencia a vaga");
        var experience = ScoreOf(sections, "Experiencia");
        var impact = ScoreOf(sections, "Impacto");
        var summary = ScoreOf(sections, "Resumo");
        var format = ScoreOf(sections, "Formato ATS");
        var contact = ScoreOf(sections, "Contato");

        var tecnica = Scale((skills * 0.65m) + (job * 0.35m), 40);
        var responsabilidades = Scale((experience * 0.55m) + (impact * 0.25m) + (job * 0.20m), 25);
        var dominio = Scale((summary * 0.45m) + (job * 0.35m) + (skills * 0.20m), 20);
        var clareza = Scale((format * 0.45m) + (contact * 0.30m) + (summary * 0.25m), 15);

        return new Dictionary<string, int>
        {
            ["aderencia_tecnica"] = tecnica,
            ["aderencia_responsabilidades"] = responsabilidades,
            ["aderencia_dominio"] = dominio,
            ["clareza_comunicacao"] = clareza
        };
    }

    private static List<ResumeAtsRequirementCoverage> BuildRequirementCoverage(string normalizedResume, string jobContext)
    {
        if (string.IsNullOrWhiteSpace(jobContext))
        {
            return [];
        }

        return SplitRequirements(jobContext)
            .Select(requirement =>
            {
                var normalizedRequirement = Normalize(requirement);
                var keywords = ExtractJobKeywords(normalizedRequirement).Take(8).ToArray();
                var matched = keywords.Where(keyword => KeywordExists(normalizedResume, keyword)).ToArray();
                var status = keywords.Length == 0 || matched.Length == 0
                    ? "ausente"
                    : matched.Length >= Math.Ceiling(keywords.Length * 0.6m)
                        ? "coberto"
                        : "parcial";
                var evidence = matched.Length == 0
                    ? "Sem evidencia literal no curriculo."
                    : $"Evidencias: {string.Join(", ", matched.Take(4))}.";
                var recommendation = status == "coberto"
                    ? "Manter evidencia no curriculo."
                    : $"Se for verdadeiro, explicite este requisito: {TrimToRequirement(requirement)}";

                return new ResumeAtsRequirementCoverage
                {
                    Requirement = TrimToRequirement(requirement),
                    Status = status,
                    Evidence = evidence,
                    Recommendation = recommendation
                };
            })
            .Take(8)
            .ToList();
    }

    private static IEnumerable<string> BuildCriticalGaps(KeywordMatchResult keywordMatch, IReadOnlyCollection<ResumeAtsRequirementCoverage> coverage)
    {
        foreach (var keyword in keywordMatch.Missing.Take(4))
        {
            yield return $"Keyword ausente: {keyword}";
        }

        foreach (var requirement in coverage.Where(item => item.Status == "ausente").Take(3))
        {
            yield return $"Requisito sem evidencia: {requirement.Requirement}";
        }
    }

    private static IEnumerable<string> ExtractRequirementKeywords(
        IEnumerable<ResumeAtsRequirementCoverage> coverage,
        params string[] markers)
    {
        return coverage
            .Where(item => markers.Any(marker => Normalize(item.Requirement).Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(item => ExtractJobKeywords(item.Requirement))
            .Where(keyword => !StopWords.Contains(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTechnicalKeyword(string keyword)
    {
        var normalized = Normalize(keyword);
        return TechnicalSignals.Any(signal => Normalize(signal).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            || KeywordSynonyms.ContainsKey(normalized)
            || ContainsAny(normalized, ".net", "c#", "api", "sql", "aws", "azure", "docker", "kubernetes", "javascript", "typescript", "angular", "react", "python", "java");
    }

    private static bool IsResponsibilityKeyword(string keyword)
    {
        return ContainsAny(
            Normalize(keyword),
            "desenvolvimento",
            "desenvolver",
            "arquitetura",
            "qualidade",
            "integracao",
            "integracoes",
            "testes",
            "documentacao",
            "automacao",
            "sistema",
            "sistemas",
            "manutencao",
            "sustentacao",
            "lideranca",
            "implementacao");
    }

    private static bool IsDomainKeyword(string keyword)
    {
        return ContainsAny(
            Normalize(keyword),
            "financeiro",
            "financeira",
            "pagamento",
            "pagamentos",
            "pix",
            "boleto",
            "boletos",
            "bancario",
            "bancaria",
            "credito",
            "seguros",
            "saude",
            "varejo",
            "rh",
            "recrutamento");
    }

    private static bool IsSeniorityKeyword(string keyword)
    {
        return ContainsAny(Normalize(keyword), "senior", "pleno", "junior", "lead", "lider", "especialista", "arquiteto", "tech lead");
    }

    private static IReadOnlyCollection<string> ExtractJobKeywords(string text)
    {
        var normalized = Normalize(text);
        var technical = TechnicalSignals
            .Where(signal => normalized.Contains(signal, StringComparison.OrdinalIgnoreCase))
            .Select(signal => Normalize(signal));

        return ExtractKeywords(normalized)
            .Concat(technical)
            .Where(word => word.Length >= 3)
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => TechnicalSignals.Any(signal => Normalize(signal).Equals(group.Key, StringComparison.OrdinalIgnoreCase)) ? 2 : 1)
            .ThenByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .ToArray();
    }

    private static bool KeywordExists(string resume, string keyword)
    {
        var normalizedKeyword = Normalize(keyword);
        if (resume.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return KeywordSynonyms.TryGetValue(normalizedKeyword, out var synonyms)
            && synonyms.Any(synonym => resume.Contains(Normalize(synonym), StringComparison.OrdinalIgnoreCase));
    }

    private static bool KeywordPartiallyExists(string resume, string keyword)
    {
        var parts = Normalize(keyword)
            .Split([' ', '/', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length >= 3)
            .ToArray();

        return parts.Length > 1 && parts.Any(part => resume.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitRequirements(string jobContext)
    {
        return jobContext
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(['\n', '.', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().TrimStart('-', '*', '•').Trim())
            .Where(item => item.Length is >= 20 and <= 240)
            .Where(item => ContainsAny(
                Normalize(item),
                "experiencia",
                "conhecimento",
                "desejavel",
                "obrigatorio",
                "requisito",
                "atuar",
                "desenvolvimento",
                "foco",
                "familiaridade",
                "responsavel",
                "arquitetura",
                "qualidade",
                "integracoes",
                "sistemas"))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static int ScoreOf(IEnumerable<ResumeAtsSectionScore> sections, string name)
    {
        return sections.FirstOrDefault(section => section.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Score ?? 60;
    }

    private static int Scale(decimal score, int max)
    {
        return Math.Max(0, Math.Min(max, (int)Math.Round(score * max / 100m)));
    }

    private static string TrimToRequirement(string value)
    {
        var clean = Regex.Replace(value.Trim(), @"\s+", " ");
        return clean.Length <= 160 ? clean : clean[..160].Trim();
    }

    private static object BuildCanonicalResume(string rawText)
    {
        var lines = rawText.Replace("\r\n", "\n").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        return new
        {
            candidate = lines.FirstOrDefault() ?? string.Empty,
            summary = ExtractSection(lines, "resumo", "perfil profissional", "summary"),
            skills = ExtractSection(lines, "competencias", "habilidades", "skills", "tecnologias"),
            experience = ExtractSection(lines, "experiencia", "historico profissional", "work experience"),
            education = ExtractSection(lines, "formacao", "educacao", "education"),
            certifications = ExtractSection(lines, "certificacoes", "certifications"),
            links = lines.Where(line => line.Contains("http", StringComparison.OrdinalIgnoreCase) || line.Contains("linkedin", StringComparison.OrdinalIgnoreCase) || line.Contains("github", StringComparison.OrdinalIgnoreCase)).Take(12)
        };
    }

    private static string[] ExtractSection(IReadOnlyList<string> lines, params string[] headings)
    {
        var start = Array.FindIndex(lines.ToArray(), line => headings.Any(heading => Normalize(line).Contains(heading, StringComparison.OrdinalIgnoreCase)));
        if (start < 0)
        {
            return [];
        }

        return lines
            .Skip(start + 1)
            .TakeWhile(line => !LooksLikeHeading(line))
            .Take(20)
            .ToArray();
    }

    private static bool LooksLikeHeading(string line)
    {
        var normalized = Normalize(line);
        return line.Length <= 64 && ContainsAny(normalized, "resumo", "perfil", "competencias", "habilidades", "experiencia", "formacao", "educacao", "certificacoes", "projetos", "idiomas");
    }

    private static IReadOnlyCollection<string> ExtractKeywords(string text)
    {
        return WordRegex()
            .Matches(Normalize(text))
            .Select(match => match.Value)
            .Where(word => word.Length >= 3 && !StopWords.Contains(word))
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .ToArray();
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static int Clamp(int score)
    {
        return Math.Max(0, Math.Min(100, score));
    }

    private static string Normalize(string value)
    {
        return value.ToLowerInvariant()
            .Replace("ç", "c", StringComparison.Ordinal)
            .Replace("ã", "a", StringComparison.Ordinal)
            .Replace("á", "a", StringComparison.Ordinal)
            .Replace("à", "a", StringComparison.Ordinal)
            .Replace("â", "a", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("ê", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal)
            .Replace("ó", "o", StringComparison.Ordinal)
            .Replace("ô", "o", StringComparison.Ordinal)
            .Replace("õ", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "para", "com", "uma", "por", "dos", "das", "que", "este", "esta", "mais", "sobre",
        "entre", "como", "seu", "sua", "the", "and", "with", "from", "this", "that"
    };

    [GeneratedRegex(@"[a-zA-Z0-9+#./-]{3,}")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\b[\w._%+-]+@[\w.-]+\.[a-zA-Z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(\+?\d{1,3}\s?)?(\(?\d{2}\)?\s?)?\d{4,5}[-\s]?\d{4}")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\d+([,.]\d+)?%?")]
    private static partial Regex NumberRegex();

    private sealed record KeywordMatchResult(List<string> Present, List<string> Missing, List<string> Partial);
}
