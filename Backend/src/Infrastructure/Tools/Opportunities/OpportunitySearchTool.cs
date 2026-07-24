using Lessie.Application.Opportunities;
using Lessie.Application.Tools;

namespace Lessie.Infrastructure.Tools.Opportunities;

internal sealed class OpportunitySearchTool(IOpportunitySearchService searchService) : ITool
{
    public string Name => "opportunity.search";

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = Guid.TryParse(request.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
            var results = await searchService.SearchAsync(
                new OpportunitySearchRequest
                {
                    Query = request.Query,
                    Location = request.Location,
                    Limit = request.Limit ?? 20
                },
                userId,
                cancellationToken);

            return new ToolResult
            {
                Success = true,
                ToolName = Name,
                Summary = results.Count > 0 ? "Opportunities found." : "No opportunities found for this search.",
                Data = results
            };
        }
        catch (Exception exception)
        {
            return new ToolResult
            {
                Success = false,
                ToolName = Name,
                Summary = "Opportunity search is not available.",
                Error = exception.Message
            };
        }
    }
}
