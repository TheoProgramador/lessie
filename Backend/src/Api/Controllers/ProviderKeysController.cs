using Lessie.Api.Http;
using Lessie.Application.ProviderKeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lessie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/provider-keys")]
public sealed class ProviderKeysController(IProviderKeyService providerKeyService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var statuses = await providerKeyService.GetStatusesAsync(userId, cancellationToken);
        return Ok(statuses);
    }

    [HttpPost("groq")]
    public async Task<IActionResult> SaveGroqKeyAsync(ProviderKeyRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { message = "API Key obrigatoria." });
        }

        var status = await providerKeyService.SaveGroqKeyAsync(userId, request.ApiKey.Trim(), cancellationToken);
        return Ok(status);
    }

    [HttpPost("pollinations")]
    public async Task<IActionResult> SavePollinationsKeyAsync(ProviderKeyRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { message = "Token Pollinations obrigatorio." });
        }

        var status = await providerKeyService.SavePollinationsKeyAsync(userId, request.ApiKey.Trim(), cancellationToken);
        return Ok(status);
    }

    [HttpDelete("groq")]
    public async Task<IActionResult> DeleteGroqKeyAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await providerKeyService.DeleteGroqKeyAsync(userId, cancellationToken);
        return Ok(status);
    }

    [HttpDelete("pollinations")]
    public async Task<IActionResult> DeletePollinationsKeyAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await providerKeyService.DeletePollinationsKeyAsync(userId, cancellationToken);
        return Ok(status);
    }
}
