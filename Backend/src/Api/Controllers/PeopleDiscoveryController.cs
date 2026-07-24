using Lessie.Api.Http;
using Lessie.Application.PeopleDiscovery;
using Lessie.Application.Tools;
using Lessie.Infrastructure.Tools.PeopleDiscovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Lessie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/people-discovery")]
public sealed class PeopleDiscoveryController(
    IToolRegistry toolRegistry,
    PeopleDiscoveryProgressReporter progressReporter,
    IPeopleDiscoveryJobSearchService jobSearchService,
    IPeopleDiscoveryResultStore resultStore) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("search")]
    public async Task<IActionResult> SearchAsync(PeopleDiscoverySearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new PeopleDiscoverySearchResponse
            {
                Success = false,
                Source = "mcp",
                ToolName = "people.search",
                Summary = "Search query is required.",
                Error = "Query obrigatoria."
            });
        }

        var toolResult = await toolRegistry.ExecuteAsync(
            "people.search",
            new ToolRequest
            {
                Query = request.Query.Trim(),
                UserId = userId.ToString(),
                Location = request.Location?.Trim()
            },
            cancellationToken);

        return Ok(MapResponse(toolResult));
    }

    [HttpPost("posts/search")]
    public async Task<IActionResult> SearchPostsAsync(PeopleDiscoverySearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new PeopleDiscoverySearchResponse
            {
                Success = false,
                Source = "mcp",
                ToolName = "posts.search",
                Summary = "Post search query is required.",
                Error = "Query obrigatoria."
            });
        }

        var toolResult = await toolRegistry.ExecuteAsync(
            "posts.search",
            new ToolRequest
            {
                Query = request.Query.Trim(),
                UserId = userId.ToString(),
                Location = request.Location?.Trim()
            },
            cancellationToken);

        return Ok(MapResponse(toolResult));
    }

    [HttpPost("search/stream")]
    public async Task StreamSearchAsync(PeopleDiscoverySearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteStreamEventAsync(
                "error",
                new { message = "Query obrigatoria." },
                cancellationToken);
            return;
        }

        var writeLock = await PrepareProgressStreamAsync(cancellationToken);
        await WriteStreamEventAsync(
            "progress",
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = "People Discovery request accepted.",
                Progress = 0,
                Total = 100
            },
            cancellationToken);

        var toolResult = await toolRegistry.ExecuteAsync(
            "people.search",
            new ToolRequest
            {
                Query = request.Query.Trim(),
                UserId = userId.ToString(),
                Location = request.Location?.Trim()
            },
            cancellationToken);

        await WriteLockedResultAsync(writeLock, MapResponse(toolResult), cancellationToken);
    }

    [HttpPost("posts/search/stream")]
    public async Task StreamPostsSearchAsync(PeopleDiscoverySearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteStreamEventAsync(
                "error",
                new { message = "Query obrigatoria." },
                cancellationToken);
            return;
        }

        var writeLock = await PrepareProgressStreamAsync(cancellationToken);
        await WriteStreamEventAsync(
            "progress",
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = "Post Search request accepted.",
                Progress = 0,
                Total = 100
            },
            cancellationToken);

        var toolResult = await toolRegistry.ExecuteAsync(
            "posts.search",
            new ToolRequest
            {
                Query = request.Query.Trim(),
                UserId = userId.ToString(),
                Location = request.Location?.Trim()
            },
            cancellationToken);

        await WriteLockedResultAsync(writeLock, MapResponse(toolResult), cancellationToken);
    }

    [HttpPost("jobs/search")]
    public async Task<IActionResult> SearchJobsAsync(PeopleDiscoveryJobSearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Keywords))
        {
            return BadRequest(new PeopleDiscoveryJobSearchResponse
            {
                Success = false,
                Summary = "Job search keywords are required.",
                Error = "Keywords obrigatorio."
            });
        }

        try
        {
            var results = await jobSearchService.SearchAsync(request, userId, cancellationToken);
            return Ok(new PeopleDiscoveryJobSearchResponse
            {
                Success = true,
                Summary = results.Count > 0 ? "Jobs found." : "No jobs found for this search.",
                Results = results
            });
        }
        catch (Exception exception)
        {
            var authenticationError = exception.Message.Contains("LinkedIn session is not authenticated", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("Authentication required", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("--login", StringComparison.OrdinalIgnoreCase);

            return Ok(new PeopleDiscoveryJobSearchResponse
            {
                Success = false,
                Summary = authenticationError ? "LinkedIn session is not authenticated." : "JobSpy LinkedIn jobs search is not available.",
                Error = exception.Message
            });
        }
    }

    [HttpPost("jobs/search/stream")]
    public async Task StreamJobsSearchAsync(PeopleDiscoveryJobSearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Keywords))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteStreamEventAsync(
                "error",
                new { message = "Keywords obrigatorio." },
                cancellationToken);
            return;
        }

        var writeLock = await PrepareProgressStreamAsync(cancellationToken);
        await WriteStreamEventAsync(
            "progress",
            new PeopleDiscoveryProgressEvent
            {
                Level = "info",
                Message = "Job Search request accepted.",
                Progress = 0,
                Total = 100
            },
            cancellationToken);

        PeopleDiscoveryJobSearchResponse response;
        try
        {
            var results = await jobSearchService.SearchAsync(request, userId, cancellationToken);
            response = new PeopleDiscoveryJobSearchResponse
            {
                Success = true,
                Summary = results.Count > 0 ? "Jobs found." : "No jobs found for this search.",
                Results = results
            };
        }
        catch (Exception exception)
        {
            var authenticationError = exception.Message.Contains("LinkedIn session is not authenticated", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("Authentication required", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("--login", StringComparison.OrdinalIgnoreCase);

            response = new PeopleDiscoveryJobSearchResponse
            {
                Success = false,
                Summary = authenticationError ? "LinkedIn session is not authenticated." : "JobSpy LinkedIn jobs search is not available.",
                Error = exception.Message
            };
        }

        await WriteLockedResultAsync(writeLock, response, cancellationToken);
    }

    [HttpPost("results/resume-sent")]
    public async Task<IActionResult> MarkResumeSentAsync(PeopleDiscoveryResumeSentRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ResultKey))
        {
            return BadRequest(new { success = false, error = "ResultKey obrigatorio." });
        }

        var updated = await resultStore.MarkResumeSentAsync(userId, request.ResultKey.Trim(), cancellationToken);
        return Ok(new { success = updated });
    }

    private static PeopleDiscoverySearchResponse MapResponse(ToolResult toolResult)
    {
        var results = toolResult.Data as IReadOnlyCollection<PeopleDiscoveryPersonDto> ?? [];

        return new PeopleDiscoverySearchResponse
        {
            Success = toolResult.Success,
            Source = "mcp",
            ToolName = toolResult.ToolName,
            Summary = toolResult.Summary,
            Results = results,
            Error = toolResult.Error
        };
    }

    private async Task<SemaphoreSlim> PrepareProgressStreamAsync(CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.XContentTypeOptions = "nosniff";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await Response.StartAsync(cancellationToken);

        var writeLock = new SemaphoreSlim(1, 1);
        progressReporter.Subscribe(async (progressEvent, token) =>
        {
            await writeLock.WaitAsync(token);
            try
            {
                await WriteStreamEventAsync("progress", progressEvent, token);
            }
            finally
            {
                writeLock.Release();
            }
        });

        return writeLock;
    }

    private async Task WriteLockedResultAsync(SemaphoreSlim writeLock, object response, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await WriteStreamEventAsync("result", response, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task WriteStreamEventAsync(string type, object data, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { type, data }, JsonOptions);
        await Response.WriteAsync(payload, cancellationToken);
        await Response.WriteAsync("\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
