using ApInfoMcpServer.Playwright;
using ApInfoMcpServer.Scrapers;
using ApInfoMcpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();

builder.Services.Configure<ApInfoOptions>(builder.Configuration.GetSection("ApInfo"));
builder.Services.AddSingleton<ApInfoBrowserFactory>();
builder.Services.AddSingleton<ApInfoScraper>();
builder.Services.AddSingleton<ApInfoTools>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "apinfo-mcp-server",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools<ApInfoTools>();

await builder.Build().RunAsync();
