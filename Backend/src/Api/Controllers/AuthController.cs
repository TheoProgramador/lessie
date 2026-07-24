using Lessie.Api.Contracts.Auth;
using Lessie.Api.Http;
using Lessie.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Lessie.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("google")]
    public async Task<IActionResult> SignInWithGoogleAsync(GoogleAuthRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Credential))
        {
            return Unauthorized();
        }

        try
        {
            var tokens = await authService.SignInWithGoogleAsync(
                request.Credential,
                HttpContext.GetClientContext(),
                cancellationToken);

            return Ok(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("dev-admin")]
    public async Task<IActionResult> SignInDevelopmentAdminAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || HttpContext.Connection.RemoteIpAddress?.IsLoopback() != true)
        {
            return NotFound();
        }

        var tokens = await authService.SignInDevelopmentAdminAsync(
            HttpContext.GetClientContext(),
            cancellationToken);

        return Ok(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Unauthorized();
        }

        var tokens = await authService.RefreshAsync(request.RefreshToken, HttpContext.GetClientContext(), cancellationToken);
        return tokens is null
            ? Unauthorized()
            : Ok(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        }

        return NoContent();
    }
}

internal static class IpAddressExtensions
{
    public static bool IsLoopback(this System.Net.IPAddress address)
        => System.Net.IPAddress.IsLoopback(address)
           || address.IsIPv4MappedToIPv6 && System.Net.IPAddress.IsLoopback(address.MapToIPv4());
}
