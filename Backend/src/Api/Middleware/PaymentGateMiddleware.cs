using Lessie.Api.Http;
using Lessie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Api.Middleware;

public sealed class PaymentGateMiddleware(RequestDelegate next)
{
    private static readonly PathString[] ExemptApiPrefixes =
    [
        new("/api/auth"),
        new("/api/me"),
        new("/api/payments"),
        new("/api/health")
    ];

    public async Task InvokeAsync(HttpContext context, LessieDbContext dbContext)
    {
        if (ShouldSkip(context) || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (!context.User.TryGetCurrentUserId(out var userId))
        {
            await next(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var access = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new
            {
                user.IsAdmin,
                HasActiveSubscription = user.Subscription != null
                    && user.Subscription.PaidUntil != null
                    && user.Subscription.PaidUntil >= now,
                PaidUntil = user.Subscription != null ? user.Subscription.PaidUntil : null
            })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (access is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (access.IsAdmin || access.HasActiveSubscription)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Assinatura expirada ou pagamento pendente.",
            paidUntil = access.PaidUntil
        }, context.RequestAborted);
    }

    private static bool ShouldSkip(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            return true;
        }

        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api"))
        {
            return true;
        }

        return ExemptApiPrefixes.Any(prefix => path.StartsWithSegments(prefix));
    }
}
