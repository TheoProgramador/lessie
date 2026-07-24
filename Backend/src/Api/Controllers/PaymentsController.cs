using Lessie.Api.Http;
using Lessie.Application.Payments;
using MercadoPago.Error;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lessie.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    [HttpGet("credit-plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCreditPlansAsync(CancellationToken cancellationToken)
        => Ok(await paymentService.GetActiveCreditPlansAsync(cancellationToken));

    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCheckoutAsync(
        CreatePaymentPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.PlanCode))
        {
            return BadRequest(new { message = "Escolha um pacote de creditos." });
        }

        try
        {
            var returnBaseUrl = GetReturnBaseUrl();
            var notificationBaseUrl = $"{Request.Scheme}://{Request.Host}";
            var response = await paymentService.CreatePreferenceAsync(userId, request, returnBaseUrl, notificationBaseUrl, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (MercadoPagoApiException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Mercado Pago recusou a criacao da preferencia.",
                detail = exception.ApiError?.Message ?? exception.Message
            });
        }
    }

    [HttpPost("mercado-pago/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> MercadoPagoWebhookAsync(
        MercadoPagoWebhookNotification? notification,
        CancellationToken cancellationToken)
    {
        var paymentId = Request.Query["data.id"].FirstOrDefault()
            ?? notification?.Data?.Id;

        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return BadRequest(new { message = "Notificacao sem data.id." });
        }

        try
        {
            var response = await paymentService.ProcessMercadoPagoPaymentNotificationAsync(
                new MercadoPagoPaymentNotificationRequest(
                    paymentId,
                    Request.Headers["x-signature"].FirstOrDefault(),
                    Request.Headers["x-request-id"].FirstOrDefault()),
                cancellationToken);

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (MercadoPagoApiException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Mercado Pago recusou a consulta da notificacao.",
                detail = exception.ApiError?.Message ?? exception.Message
            });
        }
    }

    private string GetReturnBaseUrl()
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return originUri.GetLeftPart(UriPartial.Authority);
        }

        var referer = Request.Headers.Referer.FirstOrDefault();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            return refererUri.GetLeftPart(UriPartial.Authority);
        }

        return $"{Request.Scheme}://{Request.Host}";
    }
}

public sealed record MercadoPagoWebhookNotification(
    string? Type,
    string? Action,
    MercadoPagoWebhookData? Data);

public sealed record MercadoPagoWebhookData(string? Id);
