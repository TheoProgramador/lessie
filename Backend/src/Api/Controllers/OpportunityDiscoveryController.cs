using Lessie.Api.Http;
using Lessie.Application.Opportunities;
using Lessie.Application.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lessie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/opportunity-discovery")]
public sealed class OpportunityDiscoveryController(
    IToolRegistry toolRegistry,
    IOpportunitySearchService opportunitySearchService) : ControllerBase
{
    [HttpPost("search")]
    public async Task<IActionResult> SearchAsync(OpportunitySearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new OpportunitySearchResponse
            {
                Success = false,
                Summary = "Search query is required.",
                Error = "Query obrigatoria."
            });
        }

        var toolResult = await toolRegistry.ExecuteAsync(
            "opportunity.search",
            new ToolRequest
            {
                Query = request.Query.Trim(),
                UserId = userId.ToString(),
                Location = request.Location?.Trim(),
                Limit = request.Limit
            },
            cancellationToken);

        return Ok(MapResponse(toolResult));
    }

    [HttpPost("details")]
    public async Task<IActionResult> DetailsAsync(OpportunityDetailsRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            return BadRequest(new { success = false, error = "JobId obrigatorio." });
        }

        try
        {
            var result = await opportunitySearchService.GetDetailsAsync(
                new OpportunityDetailsRequest
                {
                    JobId = request.JobId.Trim(),
                    RevealContact = request.RevealContact
                },
                userId,
                cancellationToken);

            return Ok(new
            {
                success = result is not null,
                source = result?.Source ?? "Opportunity Discovery",
                toolName = "opportunity.details",
                result,
                error = result is null ? "Opportunity not found." : null
            });
        }
        catch (Exception exception)
        {
            return Ok(new
            {
                success = false,
                source = "Opportunity Discovery",
                toolName = "opportunity.details",
                result = (JobOpportunityDto?)null,
                error = exception.Message
            });
        }
    }

    private static OpportunitySearchResponse MapResponse(ToolResult toolResult)
    {
        var results = toolResult.Data as IReadOnlyCollection<JobOpportunityDto> ?? [];
        return new OpportunitySearchResponse
        {
            Success = toolResult.Success,
            Source = results.Count == 0
                ? "Opportunity Discovery"
                : string.Join(", ", results.Select(result => result.Provider).Where(provider => !string.IsNullOrWhiteSpace(provider)).Distinct()),
            ToolName = toolResult.ToolName,
            Summary = toolResult.Summary,
            Results = results,
            Error = toolResult.Error
        };
    }
}
