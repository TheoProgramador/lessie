using System.Globalization;
using System.Text.RegularExpressions;
using ApInfoMcpServer.Models;
using ApInfoMcpServer.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace ApInfoMcpServer.Scrapers;

public sealed partial class ApInfoScraper(
    ApInfoBrowserFactory browserFactory,
    IOptions<ApInfoOptions> options,
    ILogger<ApInfoScraper> logger)
{
    private const string HomeUrl = "https://www.apinfo.com/apinfo/";
    private readonly ApInfoOptions options = options.Value;

    public async Task<IReadOnlyCollection<JobOpportunityDto>> SearchJobsAsync(
        string query,
        string? location,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var context = await browserFactory.CreateContextAsync(cancellationToken);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(options.NavigationTimeoutSeconds * 1000);

        await page.GotoAsync(HomeUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await DismissCookiesAsync(page);
        await page.Locator("#i-busca, input[name='keyw']").First.FillAsync(query);
        await page.Locator("#btn-busca, input[type='submit']").First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var requestedLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 80);
        var results = new List<JobOpportunityDto>();
        for (var pageNumber = 1; pageNumber <= 10 && results.Count < requestedLimit; pageNumber++)
        {
            var pageResults = await ExtractSearchPageAsync(page, cancellationToken);
            results.AddRange(FilterByLocation(pageResults, location));

            if (results.Count >= requestedLimit || !await GoToNextPageAsync(page, pageNumber + 1))
            {
                break;
            }
        }

        await browserFactory.PersistSessionAsync(context);
        return results
            .GroupBy(job => job.Id)
            .Select(group => group.First())
            .Take(requestedLimit)
            .ToArray();
    }

    public async Task<JobOpportunityDto?> GetJobDetailsAsync(string jobId, bool revealContact, CancellationToken cancellationToken)
    {
        var searchResult = await SearchJobsAsync(jobId, location: null, limit: 1, cancellationToken);
        var job = searchResult.FirstOrDefault(x => string.Equals(x.Id, jobId, StringComparison.OrdinalIgnoreCase))
            ?? searchResult.FirstOrDefault();

        if (job is null)
        {
            return null;
        }

        if (!revealContact || string.IsNullOrWhiteSpace(job.ApplyUrl))
        {
            return job;
        }

        await using var context = await browserFactory.CreateContextAsync(cancellationToken);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(options.ManualCaptchaTimeoutSeconds * 1000);
        await page.GotoAsync(job.ApplyUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await DismissCookiesAsync(page);

        logger.LogInformation("APInfo contact reveal page opened for job {JobId}. Complete the visible captcha manually if it appears.", jobId);
        await WaitForContactDetailsAsync(page, cancellationToken);
        var contact = await ExtractContactAsync(page);

        await browserFactory.PersistSessionAsync(context);
        return job with
        {
            ContactEmail = contact.Email,
            ContactSubject = contact.Subject
        };
    }

    private static async Task<IReadOnlyCollection<JobOpportunityDto>> ExtractSearchPageAsync(IPage page, CancellationToken cancellationToken)
    {
        var rawJobs = await page.Locator(".box-vagas").EvaluateAllAsync<RawApInfoJob[]>(
            """
            elements => elements.map((element) => {
              const title = element.querySelector('.cargo span')?.textContent?.trim() || '';
              const info = element.querySelector('.info-data')?.textContent?.trim() || '';
              const text = element.querySelector('.texto')?.innerText?.trim() || element.innerText?.trim() || '';
              const apply = element.querySelector("a[href*='enviecv.cfm']");
              const href = apply?.href || '';
              const companyMatch = text.match(/Empresa\s*\.*:\s*([^\n\r]+)/i);
              const codeMatch = text.match(/C[oó]digo\s*\.*:\s*(\d+)/i);
              return { title, info, text, applyUrl: href, company: companyMatch?.[1]?.trim() || '', id: codeMatch?.[1]?.trim() || '' };
            })
            """);
        cancellationToken.ThrowIfCancellationRequested();

        return rawJobs
            .Select(MapJob)
            .Where(job => !string.IsNullOrWhiteSpace(job.Id) || !string.IsNullOrWhiteSpace(job.Title))
            .ToArray();
    }

    private static JobOpportunityDto MapJob(RawApInfoJob raw)
    {
        var (location, date) = SplitInfo(raw.Info);
        var cleanText = Clean(raw.Text);
        var description = DescriptionTailRegex()
            .Replace(cleanText, string.Empty)
            .Trim();
        var id = string.IsNullOrWhiteSpace(raw.Id) ? ExtractJobId(raw.ApplyUrl) : raw.Id;

        return new JobOpportunityDto
        {
            Id = id,
            Title = Clean(raw.Title),
            Company = Clean(raw.Company),
            Location = location,
            Date = date,
            Description = description,
            Requirements = description,
            Url = string.IsNullOrWhiteSpace(id) ? raw.ApplyUrl : $"https://www.apinfo.com/apinfo/inc/list4.cfm?keyw={Uri.EscapeDataString(id)}",
            ApplyUrl = raw.ApplyUrl
        };
    }

    private static IReadOnlyCollection<JobOpportunityDto> FilterByLocation(IReadOnlyCollection<JobOpportunityDto> jobs, string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return jobs;
        }

        var terms = location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return jobs;
        }

        return jobs
            .Where(job => terms.Any(term => job.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
                || job.Description.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static async Task<bool> GoToNextPageAsync(IPage page, int nextPage)
    {
        var input = page.Locator("input[name='pag']").Last;
        if (await input.CountAsync() == 0)
        {
            return false;
        }

        await input.FillAsync(nextPage.ToString(CultureInfo.InvariantCulture));
        await page.Locator("input[type='submit'][value='OK']").Last.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return await page.Locator(".box-vagas").CountAsync() > 0;
    }

    private async Task WaitForContactDetailsAsync(IPage page, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(options.ManualCaptchaTimeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await page.Locator("body").InnerTextAsync();
            if (content.Contains("Dados para o envio", StringComparison.OrdinalIgnoreCase)
                && content.Contains("Email", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        throw new TimeoutException("APInfo contact details were not displayed before the manual captcha timeout.");
    }

    private static async Task<(string Email, string Subject)> ExtractContactAsync(IPage page)
    {
        var text = await page.Locator("body").InnerTextAsync();
        var email = EmailRegex().Match(text).Value;
        var subject = SubjectRegex().Match(text).Groups[1].Value.Trim();
        return (email, subject);
    }

    private static async Task DismissCookiesAsync(IPage page)
    {
        var button = page.GetByText("Eu concordo", new PageGetByTextOptions { Exact = false });
        if (await button.CountAsync() > 0)
        {
            await button.First.ClickAsync(new LocatorClickOptions { Timeout = 3000 });
        }
    }

    private static (string Location, string Date) SplitInfo(string value)
    {
        var parts = value.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            return ($"{parts[0]} - {parts[1]}", parts[2]);
        }

        return (Clean(value), string.Empty);
    }

    private static string ExtractJobId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in query)
        {
            var parts = item.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], "codvaga", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return string.Empty;
    }

    private static string Clean(string value)
    {
        return WhitespaceRegex().Replace(value ?? string.Empty, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"Empresa\s*\.*:.*", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DescriptionTailRegex();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"Assunto\s+a\s+ser\s+colocado\s+no\s+email\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectRegex();

    private sealed record RawApInfoJob(string Title, string Info, string Text, string ApplyUrl, string Company, string Id);
}
