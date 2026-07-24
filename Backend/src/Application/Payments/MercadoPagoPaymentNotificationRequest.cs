namespace Lessie.Application.Payments;

public sealed record MercadoPagoPaymentNotificationRequest(
    string PaymentId,
    string? XSignature,
    string? XRequestId);
