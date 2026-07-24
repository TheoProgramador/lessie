using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace ApInfoMcpServer.Playwright;

public sealed class ApInfoBrowserFactory(IOptions<ApInfoOptions> options, ILogger<ApInfoBrowserFactory> logger)
{
    private const string SessionFile = "storage/apinfo-session.json";
    private readonly ApInfoOptions options = options.Value;

    public async Task<IBrowserContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);

        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = options.Headless,
            SlowMo = options.SlowMoMs
        };

        IBrowser browser;
        try
        {
            launchOptions.Channel = options.BrowserChannel;
            browser = await playwright.Chromium.LaunchAsync(launchOptions);
        }
        catch when (!string.IsNullOrWhiteSpace(launchOptions.Channel))
        {
            logger.LogWarning("Configured browser channel '{Channel}' was not available. Falling back to bundled Chromium.", launchOptions.Channel);
            launchOptions.Channel = null;
            browser = await playwright.Chromium.LaunchAsync(launchOptions);
        }

        var contextOptions = new BrowserNewContextOptions
        {
            Locale = "pt-BR",
            TimezoneId = "America/Sao_Paulo",
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 }
        };

        if (File.Exists(SessionPath))
        {
            contextOptions.StorageStatePath = SessionPath;
        }

        var context = await browser.NewContextAsync(contextOptions);
        cancellationToken.ThrowIfCancellationRequested();
        return context;
    }

    public async Task PersistSessionAsync(IBrowserContext context)
    {
        await context.StorageStateAsync(new BrowserContextStorageStateOptions
        {
            Path = SessionPath
        });
    }

    private static string SessionPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", SessionFile));
}
