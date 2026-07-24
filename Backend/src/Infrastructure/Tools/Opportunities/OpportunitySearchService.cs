using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lessie.Application.Opportunities;
using Microsoft.Extensions.Logging;

namespace Lessie.Infrastructure.Tools.Opportunities;

internal sealed class OpportunitySearchService(
    IEnumerable<IOpportunityProvider> providers,
    ILogger<OpportunitySearchService> logger) : IOpportunitySearchService
{
    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchAsync(
        OpportunitySearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var providerList = providers.ToArray();
        if (providerList.Length == 0)
        {
            return [];
        }

        var tasks = providerList.Select(provider => SearchProviderAsync(provider, request, userId, cancellationToken)).ToArray();
        var responses = await Task.WhenAll(tasks);
        var results = responses.SelectMany(response => response).ToArray();

        return Rank(Deduplicate(results), request).Take(Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 200)).ToArray();
    }

    public async Task<JobOpportunityDto?> GetDetailsAsync(
        OpportunityDetailsRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        foreach (var provider in providers)
        {
            try
            {
                var result = await provider.GetDetailsAsync(request, userId, cancellationToken);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Opportunity provider {ProviderName} could not fetch details for {JobId}.",
                    provider.ProviderName,
                    request.JobId);
            }
        }

        return null;
    }

    private async Task<IReadOnlyCollection<JobOpportunityDto>> SearchProviderAsync(
        IOpportunityProvider provider,
        OpportunitySearchRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAsync(request, userId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Opportunity provider {ProviderName} failed during search.",
                provider.ProviderName);
            return [];
        }
    }

    private static IReadOnlyCollection<JobOpportunityDto> Deduplicate(IReadOnlyCollection<JobOpportunityDto> results)
    {
        var selected = new Dictionary<string, JobOpportunityDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            var key = BuildDedupKey(result);
            if (!selected.TryGetValue(key, out var current) || ScoreCompleteness(result) > ScoreCompleteness(current))
            {
                selected[key] = result;
            }
        }

        return selected.Values.ToArray();
    }

    private static IEnumerable<JobOpportunityDto> Rank(
        IReadOnlyCollection<JobOpportunityDto> results,
        OpportunitySearchRequest request)
    {
        var queryTerms = Tokenize(request.Query).ToArray();
        return results
            .OrderByDescending(result => Score(result, queryTerms))
            .ThenByDescending(result => result.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(result => result.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Company, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase);
    }

    private static int Score(JobOpportunityDto result, IReadOnlyCollection<string> queryTerms)
    {
        var score = 0;
        var searchable = Normalize($"{result.Title} {result.Company} {result.Location} {result.Description}");

        score += queryTerms.Count(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase)) * 8;

        if (result.PublishedAt is { } publishedAt)
        {
            var age = DateTimeOffset.UtcNow - publishedAt.ToUniversalTime();
            score += age.TotalDays switch
            {
                <= 1 => 25,
                <= 7 => 15,
                <= 30 => 8,
                _ => 0
            };
        }

        if (!string.IsNullOrWhiteSpace(result.Salary))
        {
            score += 8;
        }

        if (result.RemoteType.Contains("remote", StringComparison.OrdinalIgnoreCase)
            || result.Location.Contains("remoto", StringComparison.OrdinalIgnoreCase)
            || result.Location.Contains("remote", StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
        }

        score += result.Source.ToLowerInvariant() switch
        {
            "apinfo" => 7,
            "indeed" => 6,
            "linkedin" => 5,
            "glassdoor" => 4,
            "ziprecruiter" => 3,
            _ => 1
        };

        return score;
    }

    private static int ScoreCompleteness(JobOpportunityDto result)
    {
        return new[]
        {
            result.Description,
            result.Requirements,
            result.Url,
            result.ApplyUrl,
            result.ContactEmail,
            result.Salary
        }.Count(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string BuildDedupKey(JobOpportunityDto result)
    {
        var urlId = ExtractStableUrlId(FirstNonEmpty(result.Url, result.ApplyUrl));
        if (!string.IsNullOrWhiteSpace(urlId))
        {
            return urlId;
        }

        if (!string.IsNullOrWhiteSpace(result.Id) && !string.IsNullOrWhiteSpace(result.Company))
        {
            return Normalize($"{result.Company}|{result.Id}");
        }

        return Normalize($"{result.Company}|{result.Title}|{result.Location}");
    }

    private static string ExtractStableUrlId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var jk = Regex.Match(url, @"[?&]jk=([^&]+)", RegexOptions.IgnoreCase);
        if (jk.Success)
        {
            return jk.Groups[1].Value;
        }

        var linkedIn = Regex.Match(url, @"/jobs/view/(\d+)", RegexOptions.IgnoreCase);
        if (linkedIn.Success)
        {
            return $"linkedin:{linkedIn.Groups[1].Value}";
        }

        var greenhouse = Regex.Match(url, @"greenhouse\.io/.+?/jobs/(\d+)", RegexOptions.IgnoreCase);
        if (greenhouse.Success)
        {
            return $"greenhouse:{greenhouse.Groups[1].Value}";
        }

        var lever = Regex.Match(url, @"jobs\.lever\.co/([^/]+)/([^/?#]+)", RegexOptions.IgnoreCase);
        if (lever.Success)
        {
            return $"lever:{lever.Groups[1].Value}:{lever.Groups[2].Value}";
        }

        var ashby = Regex.Match(url, @"jobs\.ashbyhq\.com/([^/]+)/([^/?#]+)", RegexOptions.IgnoreCase);
        if (ashby.Success)
        {
            return $"ashby:{ashby.Groups[1].Value}:{ashby.Groups[2].Value}";
        }

        var smartRecruiters = Regex.Match(url, @"smartrecruiters\.com/(?:[^/]+/)?([^/?#]+)$", RegexOptions.IgnoreCase);
        if (smartRecruiters.Success)
        {
            return $"smartrecruiters:{smartRecruiters.Groups[1].Value}";
        }

        var teamtailor = Regex.Match(url, @"teamtailor\.com/jobs/(?<id>\d+)", RegexOptions.IgnoreCase);
        if (teamtailor.Success)
        {
            return $"teamtailor:{teamtailor.Groups["id"].Value}";
        }

        var recruitee = Regex.Match(url, @"recruitee\.com/(?:o|l)/([^/?#]+)", RegexOptions.IgnoreCase);
        if (recruitee.Success)
        {
            return $"recruitee:{recruitee.Groups[1].Value}";
        }

        var workday = Regex.Match(url, @"myworkdayjobs\.com/.+?/(?:job/)?([^/?#]+)$", RegexOptions.IgnoreCase);
        if (workday.Success)
        {
            return $"workday:{workday.Groups[1].Value}";
        }

        return Normalize(url);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }
}
