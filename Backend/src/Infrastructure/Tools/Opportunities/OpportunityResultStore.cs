using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Lessie.Application.Opportunities;
using Lessie.Domain.Entities;
using Lessie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Infrastructure.Tools.Opportunities;

internal sealed partial class OpportunityResultStore(LessieDbContext dbContext) : IOpportunityResultStore
{
    public async Task<IReadOnlyCollection<JobOpportunityDto>> SaveSearchResultsAsync(
        Guid userId,
        string query,
        IReadOnlyCollection<JobOpportunityDto> results,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return [];
        }

        var savedSearch = await GetOrCreateSavedSearchAsync(userId, query, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var candidates = results
            .Select(result => WithResultKey(result, BuildResultKey(result)))
            .Where(result => !string.IsNullOrWhiteSpace(result.ResultKey))
            .GroupBy(result => result.ResultKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        var keys = candidates.Select(x => x.ResultKey).ToArray();
        var existing = await dbContext.OpportunitySavedSearchResults
            .Where(x => x.OpportunitySavedSearchId == savedSearch.Id && keys.Contains(x.ResultKey))
            .ToDictionaryAsync(x => x.ResultKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var result in candidates)
        {
            if (existing.TryGetValue(result.ResultKey, out var saved))
            {
                Update(saved, result, now);
                continue;
            }

            dbContext.OpportunitySavedSearchResults.Add(Create(savedSearch, result, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return candidates;
    }

    public async Task<JobOpportunityDto?> SaveDetailsAsync(
        Guid userId,
        string query,
        JobOpportunityDto result,
        CancellationToken cancellationToken)
    {
        var saved = await SaveSearchResultsAsync(userId, query, [result], cancellationToken);
        return saved.FirstOrDefault();
    }

    private async Task<OpportunitySavedSearch> GetOrCreateSavedSearchAsync(
        Guid userId,
        string query,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var queryKey = BuildQueryKey(query);
        var searchText = await dbContext.OpportunitySearchTexts
            .FirstOrDefaultAsync(x => x.UserId == userId && x.QueryKey == queryKey, cancellationToken);

        if (searchText is null)
        {
            searchText = new OpportunitySearchText
            {
                UserId = userId,
                QueryKey = queryKey,
                SearchText = Trim(query, 4000),
                CreatedAt = now,
                LastUsedAt = now
            };
            dbContext.OpportunitySearchTexts.Add(searchText);
        }
        else
        {
            searchText.SearchText = Trim(query, 4000);
            searchText.LastUsedAt = now;
        }

        var savedSearch = await dbContext.OpportunitySavedSearches
            .FirstOrDefaultAsync(x => x.OpportunitySearchTextId == searchText.Id, cancellationToken);

        if (savedSearch is null)
        {
            savedSearch = new OpportunitySavedSearch
            {
                SearchText = searchText,
                CreatedAt = now,
                LastRunAt = now,
                RunCount = 1
            };
            dbContext.OpportunitySavedSearches.Add(savedSearch);
        }
        else
        {
            savedSearch.LastRunAt = now;
            savedSearch.RunCount++;
        }

        return savedSearch;
    }

    private static OpportunitySavedSearchResult Create(
        OpportunitySavedSearch savedSearch,
        JobOpportunityDto result,
        DateTimeOffset now)
    {
        var saved = new OpportunitySavedSearchResult
        {
            OpportunitySavedSearch = savedSearch,
            ResultKey = result.ResultKey,
            CreatedAt = now
        };
        Update(saved, result, now);
        return saved;
    }

    private static void Update(OpportunitySavedSearchResult saved, JobOpportunityDto result, DateTimeOffset now)
    {
        saved.JobId = Trim(result.Id, 64);
        saved.Title = Trim(result.Title, 600);
        saved.Company = Trim(result.Company, 500);
        saved.Location = Trim(result.Location, 300);
        saved.Date = Trim(result.Date, 32);
        saved.Description = Trim(result.Description, 4000);
        saved.Requirements = Trim(result.Requirements, 4000);
        saved.Url = Trim(result.Url, 1200);
        saved.ApplyUrl = Trim(result.ApplyUrl, 1200);
        saved.ContactEmail = Trim(result.ContactEmail, 320);
        saved.ContactSubject = Trim(result.ContactSubject, 500);
        saved.Source = Trim(string.IsNullOrWhiteSpace(result.Source) ? "APInfo" : result.Source, 64);
        saved.LastSeenAt = now;
    }

    private static JobOpportunityDto WithResultKey(JobOpportunityDto result, string resultKey)
    {
        return new JobOpportunityDto
        {
            ResultKey = resultKey,
            Id = result.Id,
            Title = result.Title,
            Company = result.Company,
            Location = result.Location,
            Country = result.Country,
            RemoteType = result.RemoteType,
            EmploymentType = result.EmploymentType,
            Salary = result.Salary,
            PublishedAt = result.PublishedAt,
            Date = result.Date,
            Description = result.Description,
            Requirements = result.Requirements,
            Url = result.Url,
            ApplyUrl = result.ApplyUrl,
            ContactEmail = result.ContactEmail,
            ContactSubject = result.ContactSubject,
            Source = string.IsNullOrWhiteSpace(result.Source) ? "APInfo" : result.Source,
            Provider = string.IsNullOrWhiteSpace(result.Provider) ? "APInfo" : result.Provider
        };
    }

    private static string BuildResultKey(JobOpportunityDto result)
    {
        if (!string.IsNullOrWhiteSpace(result.Id))
        {
            return $"apinfo:{Normalize(result.Id)}";
        }

        var seed = $"{result.Url}|{result.ApplyUrl}|{result.Title}|{result.Company}";
        return $"apinfo:{Hash(seed)}";
    }

    private static string BuildQueryKey(string query)
    {
        var normalized = Normalize(query);
        return normalized.Length <= 256 ? normalized : Hash(normalized);
    }

    private static string Normalize(string value)
    {
        return WhitespaceRegex().Replace(value.Trim().ToLowerInvariant(), " ");
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Trim(string value, int maxLength)
    {
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
