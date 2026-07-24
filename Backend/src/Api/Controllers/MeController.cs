using Lessie.Api.Http;
using Lessie.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lessie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(IAuthService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await authService.GetCurrentUserAsync(userId, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }
}
