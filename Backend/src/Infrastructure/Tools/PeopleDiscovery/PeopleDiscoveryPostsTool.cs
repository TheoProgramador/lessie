using Lessie.Application.Tools;

namespace Lessie.Infrastructure.Tools.PeopleDiscovery;

internal sealed class PeopleDiscoveryPostsTool(IPeopleDiscoveryAdapter adapter) : ITool
{
    public string Name => "posts.search";

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var results = await adapter.SearchPostsAsync(request.Query, request.UserId, request.Location, cancellationToken);
            return new ToolResult
            {
                Success = true,
                ToolName = Name,
                Summary = results.Count > 0 ? "Posts found." : "No posts found for this search.",
                Data = results
            };
        }
        catch (Exception exception)
        {
            var authenticationError = exception.Message.Contains("LinkedIn session is not authenticated", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("Authentication required", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("--login", StringComparison.OrdinalIgnoreCase);

            return new ToolResult
            {
                Success = false,
                ToolName = Name,
                Summary = authenticationError ? "LinkedIn session is not authenticated." : "LinkedIn MCP posts search is not available.",
                Error = exception.Message
            };
        }
    }
}
