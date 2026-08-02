using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Lessie.Api.Middleware;
using Lessie.Infrastructure;
using Lessie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Secret"];
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException("JWT_SECRET must be configured with at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                logger.LogWarning(context.Exception, "JWT authentication failed.");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                logger.LogWarning("JWT challenge. Error: {Error}. Description: {Description}", context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:FrontendOrigins").Get<string[]>() ?? [];
        var singleOrigin = builder.Configuration["FRONTEND_ORIGIN"] ?? builder.Configuration["Cors:FrontendOrigin"];
        var allowedOrigins = configuredOrigins
            .Concat(string.IsNullOrWhiteSpace(singleOrigin) ? [] : [singleOrigin])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedOrigins.Length == 0)
        {
            allowedOrigins = ["http://localhost:4200"];
        }

        policy.SetIsOriginAllowed(requestOrigin =>
                allowedOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase)
                || (builder.Environment.IsDevelopment() && IsDevelopmentFrontendOrigin(requestOrigin)))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LessieDbContext>();
    await DatabaseInitializer.EnsureDevelopmentSchemaAsync(dbContext);
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PaymentGateMiddleware>();
app.MapControllers();

app.Run();

static bool IsDevelopmentFrontendOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || uri.Port != 4200)
    {
        return false;
    }

    var host = uri.Host;
    if (host is "localhost" or "127.0.0.1" or "0.0.0.0" or "::1")
    {
        return true;
    }

    if (!host.Contains('.', StringComparison.Ordinal))
    {
        return true;
    }

    if (host.StartsWith("10.", StringComparison.Ordinal) || host.StartsWith("192.168.", StringComparison.Ordinal))
    {
        return true;
    }

    if (!host.StartsWith("172.", StringComparison.Ordinal))
    {
        return false;
    }

    var parts = host.Split('.');
    return parts.Length >= 2
        && int.TryParse(parts[1], out var secondOctet)
        && secondOctet is >= 16 and <= 31;
}
