using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Lessie.Application.PeopleDiscovery;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Infrastructure.Tools.PeopleDiscovery;

internal sealed partial class PeopleDiscoveryResultStore(LessieDbContext dbContext) : IPeopleDiscoveryResultStore
{
    private const int MaxPreviousResults = 60;
    private static readonly string[] StopWords =
    [
        "and",
        "com",
        "das",
        "dos",
        "for",
        "the",
        "uma",
        "with"
    ];

    public async Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> FindPreviousResultsAsync(
        Guid userId,
        string query,
        string source,
        CancellationToken cancellationToken)
    {
        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
        {
            return [];
        }

        var queryKey = BuildQueryKey(queryTokens);
        var searches = await dbContext.PeopleDiscoverySavedSearches
            .AsNoTracking()
            .Where(x => x.SearchText != null && x.SearchText.UserId == userId)
            .Include(x => x.SearchText)
            .Include(x => x.Results)
            .OrderByDescending(x => x.LastRunAt)
            .Take(80)
            .ToListAsync(cancellationToken);

        return searches
            .Select(search => new
            {
                Search = search,
                Score = Score(search, queryTokens, queryKey)
            })
            .Where(x => x.Score >= 0.45)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Search.LastRunAt)
            .SelectMany(x => x.Search.Results
                .Where(result => !result.ResumeSent)
                .Where(result => string.Equals(result.Source, source, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(result => result.LastSeenAt)
                .Select(result => new
                {
                    Result = result,
                    x.Score
                }))
            .Take(MaxPreviousResults)
            .Select(x => new PeopleDiscoveryPersonDto
            {
                ResultKey = x.Result.ResultKey,
                Name = x.Result.Name,
                Title = x.Result.Title,
                Company = x.Result.Company,
                Location = x.Result.Location,
                ContactInfo = x.Result.ContactInfo,
                ProfileUrl = x.Result.ProfileUrl,
                Source = x.Result.Source,
                ResumeSent = x.Result.ResumeSent
            })
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> SaveAndFilterAsync(
        Guid userId,
        string query,
        IReadOnlyCollection<PeopleDiscoveryPersonDto> results,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var queryKey = BuildQueryKey(Tokenize(query));
        var candidates = results
            .Where(ShouldSave)
            .Select(result => new
            {
                Result = result,
                ResultKey = BuildResultKey(result)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.ResultKey))
            .GroupBy(x => x.ResultKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        var keys = candidates.Select(x => x.ResultKey).ToArray();
        var sentKeys = await dbContext.PeopleDiscoverySavedSearchResults
            .Where(x => keys.Contains(x.ResultKey)
                && x.ResumeSent
                && x.PeopleDiscoverySavedSearch != null
                && x.PeopleDiscoverySavedSearch.SearchText != null
                && x.PeopleDiscoverySavedSearch.SearchText.UserId == userId)
            .Select(x => x.ResultKey)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var sentKeySet = sentKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        candidates = candidates
            .Where(x => !sentKeySet.Contains(x.ResultKey))
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        keys = candidates.Select(x => x.ResultKey).ToArray();
        var searchText = await dbContext.PeopleDiscoverySearchTexts
            .FirstOrDefaultAsync(x => x.UserId == userId && x.QueryKey == queryKey, cancellationToken);

        if (searchText is null)
        {
            searchText = new PeopleDiscoverySearchText
            {
                UserId = userId,
                QueryKey = Trim(queryKey, 256),
                SearchText = Trim(query, 4000),
                CreatedAt = now,
                LastUsedAt = now
            };
            dbContext.PeopleDiscoverySearchTexts.Add(searchText);
        }
        else
        {
            searchText.SearchText = Trim(query, 4000);
            searchText.LastUsedAt = now;
        }

        var savedSearch = await dbContext.PeopleDiscoverySavedSearches
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.PeopleDiscoverySearchTextId == searchText.Id, cancellationToken);

        if (savedSearch is null)
        {
            savedSearch = new PeopleDiscoverySavedSearch
            {
                SearchText = searchText,
                CreatedAt = now,
                LastRunAt = now,
                RunCount = 1
            };
            dbContext.PeopleDiscoverySavedSearches.Add(savedSearch);
        }
        else
        {
            savedSearch.LastRunAt = now;
            savedSearch.RunCount++;
        }

        var existing = await dbContext.PeopleDiscoverySavedSearchResults
            .Where(x => x.PeopleDiscoverySavedSearchId == savedSearch.Id && keys.Contains(x.ResultKey))
            .ToDictionaryAsync(x => x.ResultKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (existing.TryGetValue(candidate.ResultKey, out var saved))
            {
                Update(saved, candidate.Result, query, now);
                continue;
            }

            dbContext.PeopleDiscoverySavedSearchResults.Add(Create(savedSearch, candidate.Result, query, candidate.ResultKey, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return candidates
            .Select(candidate => WithResultState(candidate.Result, candidate.ResultKey, resumeSent: false))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PeopleDiscoveryJobDto>> SaveAndFilterJobsAsync(
        Guid userId,
        string query,
        IReadOnlyCollection<PeopleDiscoveryJobDto> results,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var queryKey = BuildQueryKey(Tokenize(query));
        var candidates = results
            .Where(ShouldSave)
            .Select(result => new
            {
                Result = result,
                ResultKey = BuildResultKey(result)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.ResultKey))
            .GroupBy(x => x.ResultKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        var keys = candidates.Select(x => x.ResultKey).ToArray();
        var sentKeys = await dbContext.PeopleDiscoverySavedSearchResults
            .Where(x => keys.Contains(x.ResultKey)
                && x.ResumeSent
                && x.PeopleDiscoverySavedSearch != null
                && x.PeopleDiscoverySavedSearch.SearchText != null
                && x.PeopleDiscoverySavedSearch.SearchText.UserId == userId)
            .Select(x => x.ResultKey)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var sentKeySet = sentKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        candidates = candidates
            .Where(x => !sentKeySet.Contains(x.ResultKey))
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        keys = candidates.Select(x => x.ResultKey).ToArray();
        var searchText = await dbContext.PeopleDiscoverySearchTexts
            .FirstOrDefaultAsync(x => x.UserId == userId && x.QueryKey == queryKey, cancellationToken);

        if (searchText is null)
        {
            searchText = new PeopleDiscoverySearchText
            {
                UserId = userId,
                QueryKey = Trim(queryKey, 256),
                SearchText = Trim(query, 4000),
                CreatedAt = now,
                LastUsedAt = now
            };
            dbContext.PeopleDiscoverySearchTexts.Add(searchText);
        }
        else
        {
            searchText.SearchText = Trim(query, 4000);
            searchText.LastUsedAt = now;
        }

        var savedSearch = await dbContext.PeopleDiscoverySavedSearches
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.PeopleDiscoverySearchTextId == searchText.Id, cancellationToken);

        if (savedSearch is null)
        {
            savedSearch = new PeopleDiscoverySavedSearch
            {
                SearchText = searchText,
                CreatedAt = now,
                LastRunAt = now,
                RunCount = 1
            };
            dbContext.PeopleDiscoverySavedSearches.Add(savedSearch);
        }
        else
        {
            savedSearch.LastRunAt = now;
            savedSearch.RunCount++;
        }

        var existing = await dbContext.PeopleDiscoverySavedSearchResults
            .Where(x => x.PeopleDiscoverySavedSearchId == savedSearch.Id && keys.Contains(x.ResultKey))
            .ToDictionaryAsync(x => x.ResultKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (existing.TryGetValue(candidate.ResultKey, out var saved))
            {
                Update(saved, candidate.Result, now);
                continue;
            }

            dbContext.PeopleDiscoverySavedSearchResults.Add(Create(savedSearch, candidate.Result, candidate.ResultKey, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return candidates
            .Select(candidate => WithResultState(candidate.Result, candidate.ResultKey, resumeSent: false))
            .ToArray();
    }

    public async Task<bool> MarkResumeSentAsync(
        Guid userId,
        string resultKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resultKey))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var results = await dbContext.PeopleDiscoverySavedSearchResults
            .Where(x => x.ResultKey == resultKey
                && x.PeopleDiscoverySavedSearch != null
                && x.PeopleDiscoverySavedSearch.SearchText != null
                && x.PeopleDiscoverySavedSearch.SearchText.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var result in results)
        {
            result.ResumeSent = true;
            result.ResumeSentAt = now;
        }

        if (results.Count == 0)
        {
            return false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool ShouldSave(PeopleDiscoveryPersonDto result)
    {
        if (string.Equals(result.Source, "LinkedIn People", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(result.ProfileUrl) || !string.IsNullOrWhiteSpace(result.Name);
        }

        return string.Equals(result.Source, "LinkedIn Post", StringComparison.OrdinalIgnoreCase)
            && (!string.IsNullOrWhiteSpace(result.ProfileUrl)
                || !string.IsNullOrWhiteSpace(result.Title)
                || !string.IsNullOrWhiteSpace(result.Name));
    }

    private static bool ShouldSave(PeopleDiscoveryJobDto result)
    {
        return !string.IsNullOrWhiteSpace(result.JobUrl)
            || !string.IsNullOrWhiteSpace(result.JobId)
            || !string.IsNullOrWhiteSpace(result.Title);
    }

    private static PeopleDiscoverySavedSearchResult Create(
        PeopleDiscoverySavedSearch search,
        PeopleDiscoveryPersonDto result,
        string query,
        string resultKey,
        DateTimeOffset now)
    {
        var saved = new PeopleDiscoverySavedSearchResult
        {
            PeopleDiscoverySavedSearch = search,
            ResultKey = resultKey,
            CreatedAt = now
        };
        Update(saved, result, query, now);
        return saved;
    }

    private static PeopleDiscoverySavedSearchResult Create(
        PeopleDiscoverySavedSearch search,
        PeopleDiscoveryJobDto result,
        string resultKey,
        DateTimeOffset now)
    {
        var saved = new PeopleDiscoverySavedSearchResult
        {
            PeopleDiscoverySavedSearch = search,
            ResultKey = resultKey,
            CreatedAt = now
        };
        Update(saved, result, now);
        return saved;
    }

    private static void Update(
        PeopleDiscoverySavedSearchResult saved,
        PeopleDiscoveryPersonDto result,
        string query,
        DateTimeOffset now)
    {
        saved.Name = Trim(result.Name, 300);
        saved.Title = Trim(result.Title, 2000);
        saved.Company = Trim(result.Company, 500);
        saved.Location = Trim(result.Location, 300);
        saved.ContactInfo = Trim(result.ContactInfo, 1000);
        saved.ProfileUrl = Trim(result.ProfileUrl, 1200);
        saved.Source = Trim(result.Source, 64);
        saved.LastSeenAt = now;
    }

    private static void Update(
        PeopleDiscoverySavedSearchResult saved,
        PeopleDiscoveryJobDto result,
        DateTimeOffset now)
    {
        saved.Name = Trim(result.Title, 300);
        saved.Title = Trim(result.Insight, 2000);
        saved.Company = Trim(result.Company, 500);
        saved.Location = Trim(result.Location, 300);
        saved.ContactInfo = Trim(result.Metadata, 1000);
        saved.ProfileUrl = Trim(result.JobUrl, 1200);
        saved.Source = Trim(result.Source, 64);
        saved.LastSeenAt = now;
    }

    private static double Score(PeopleDiscoverySavedSearch search, IReadOnlyCollection<string> queryTokens, string queryKey)
    {
        if (search.SearchText is not null
            && string.Equals(search.SearchText.QueryKey, queryKey, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var searchText = Normalize(search.SearchText?.SearchText ?? string.Empty);
        var matches = queryTokens.Count(token => searchText.Contains(token, StringComparison.OrdinalIgnoreCase));
        return queryTokens.Count == 0 ? 0 : (double)matches / queryTokens.Count;
    }

    private static string BuildQueryKey(IReadOnlyCollection<string> tokens)
    {
        return string.Join(' ', tokens.Order(StringComparer.Ordinal));
    }

    private static string BuildResultKey(PeopleDiscoveryPersonDto result)
    {
        var normalized = Normalize($"{result.Source}|{result.ProfileUrl}|{result.Name}|{result.Title}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string BuildResultKey(PeopleDiscoveryJobDto result)
    {
        var stableId = ExtractStableJobId(result.JobUrl);
        var normalized = !string.IsNullOrWhiteSpace(stableId)
            ? stableId
            : Normalize($"{result.JobId}|{result.Title}|{result.Company}|{result.Location}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string ExtractStableJobId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
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

        return Normalize(url);
    }

    private static PeopleDiscoveryPersonDto WithResultState(
        PeopleDiscoveryPersonDto result,
        string resultKey,
        bool resumeSent)
    {
        return new PeopleDiscoveryPersonDto
        {
            ResultKey = resultKey,
            Name = result.Name,
            Title = result.Title,
            Company = result.Company,
            Location = result.Location,
            ContactInfo = result.ContactInfo,
            ProfileUrl = result.ProfileUrl,
            Source = result.Source,
            ResumeSent = resumeSent
        };
    }

    private static PeopleDiscoveryJobDto WithResultState(
        PeopleDiscoveryJobDto result,
        string resultKey,
        bool resumeSent)
    {
        return new PeopleDiscoveryJobDto
        {
            ResultKey = resultKey,
            Title = result.Title,
            Company = result.Company,
            Location = result.Location,
            JobId = result.JobId,
            JobUrl = result.JobUrl,
            Insight = result.Insight,
            Metadata = result.Metadata,
            Source = result.Source,
            ResumeSent = resumeSent
        };
    }

    private static IReadOnlyCollection<string> Tokenize(string value)
    {
        return WordRegex()
            .Matches(Normalize(value))
            .Select(match => match.Value)
            .Where(token => token.Length >= 3 && !StopWords.Contains(token, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    [GeneratedRegex(@"[\p{L}\p{N}+#.]+")]
    private static partial Regex WordRegex();
}
