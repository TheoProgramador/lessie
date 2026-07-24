using Lessie.Application.Auth;

namespace Lessie.Api.Http;

internal static class HttpContextExtensions
{
    public static ClientContext GetClientContext(this HttpContext httpContext)
    {
        return new ClientContext(
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());
    }
}
