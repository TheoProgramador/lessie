using Lessie.Application.Auth;
using Lessie.Application.Chatbot;
using Lessie.Application.InterviewAnalysis;
using Lessie.Application.PeopleDiscovery;
using Lessie.Application.Opportunities;
using Lessie.Application.Payments;
using Lessie.Application.ProviderKeys;
using Lessie.Application.ResumeImprovements;
using Lessie.Application.Tools;
using Lessie.Infrastructure.Auth;
using Lessie.Infrastructure.Chatbot;
using Lessie.Infrastructure.InterviewAnalysis;
using Lessie.Infrastructure.Persistence;
using Lessie.Infrastructure.Payments;
using Lessie.Infrastructure.ProviderKeys;
using Lessie.Infrastructure.ResumeImprovements;
using Lessie.Infrastructure.Tools;
using Lessie.Infrastructure.Tools.Opportunities;
using Lessie.Infrastructure.Tools.PeopleDiscovery;
using MercadoPago.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lessie.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["CONNECTION_STRING"]
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("CONNECTION_STRING is not configured.");

        services.AddDbContext<LessieDbContext>(options => options.UseSqlServer(connectionString));
        services.Configure<MercadoPagoOptions>(configuration.GetSection("MercadoPago"));
        services.PostConfigure<MercadoPagoOptions>(options =>
        {
            var publicKey = configuration["MERCADO_PAGO_PUBLIC_KEY"];
            var accessToken = configuration["MERCADO_PAGO_ACCESS_TOKEN"];
            var webhookSecret = configuration["MERCADO_PAGO_WEBHOOK_SECRET"];
            var notificationUrl = configuration["MERCADO_PAGO_NOTIFICATION_URL"];

            if (!string.IsNullOrWhiteSpace(publicKey))
            {
                options.PublicKey = publicKey;
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                options.AccessToken = accessToken;
            }

            if (!string.IsNullOrWhiteSpace(webhookSecret))
            {
                options.WebhookSecret = webhookSecret;
            }

            if (!string.IsNullOrWhiteSpace(notificationUrl))
            {
                options.NotificationUrl = notificationUrl;
            }
        });
        ConfigureMercadoPago(configuration);

        services.AddScoped<GoogleTokenValidator>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPaymentService, MercadoPagoPaymentService>();
        services.AddScoped<ProviderKeyEncryption>();
        services.AddScoped<IProviderKeyService, ProviderKeyService>();
        services.AddScoped<IResumeAtsAnalyzer, ResumeAtsAnalyzer>();
        services.AddHttpClient<IResumeExternalMcpContextService, ResumeExternalMcpContextService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        services.AddScoped<PeopleDiscoveryProgressReporter>();
        services.AddScoped<IPeopleDiscoveryProgressReporter>(provider => provider.GetRequiredService<PeopleDiscoveryProgressReporter>());
        services.AddScoped<IPeopleDiscoveryResultStore, PeopleDiscoveryResultStore>();
        services.AddScoped<IPeopleDiscoveryAdapter, LinkedInPeopleMcpAdapter>();
        services.AddScoped<IPeopleDiscoveryJobSearchService, JobSpyPeopleDiscoveryJobSearchService>();
        services.AddScoped<IOpportunityResultStore, OpportunityResultStore>();
        services.AddScoped<IOpportunityProvider, JobSpyOpportunityProvider>();
        services.AddScoped<IOpportunityProvider, JdIntelOpportunityProvider>();
        services.AddHttpClient<JobscopeOpportunityProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Lessie/1.0");
        });
        services.AddScoped<IOpportunityProvider>(provider => provider.GetRequiredService<JobscopeOpportunityProvider>());
        services.AddScoped<IOpportunityProvider, JobsearchBuddyOpportunityProvider>();
        services.AddScoped<IOpportunitySearchService, OpportunitySearchService>();
        services.AddScoped<ITool, PeopleDiscoveryTool>();
        services.AddScoped<ITool, PeopleDiscoveryPostsTool>();
        services.AddScoped<ITool, OpportunitySearchTool>();
        services.AddScoped<ITool, ResumeAtsTool>();
        services.AddScoped<IToolRegistry, ToolRegistry>();
        services.AddHttpClient<IChatbotService, GroqChatbotService>(client =>
        {
            client.BaseAddress = new Uri("https://api.groq.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<IPollinationsChatbotService, PollinationsChatbotService>(client =>
        {
            client.BaseAddress = new Uri("https://gen.pollinations.ai/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<IResumeImprovementService, ResumeImprovementService>(client =>
        {
            client.BaseAddress = new Uri("https://gen.pollinations.ai/");
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddHttpClient<IInterviewAnalysisService, InterviewAnalysisService>(client =>
        {
            client.BaseAddress = new Uri("https://api.groq.com/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        return services;
    }

    private static void ConfigureMercadoPago(IConfiguration configuration)
    {
        var accessToken = configuration["MERCADO_PAGO_ACCESS_TOKEN"]
            ?? configuration["MercadoPago:AccessToken"];

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            MercadoPagoConfig.AccessToken = accessToken;
        }
    }
}
