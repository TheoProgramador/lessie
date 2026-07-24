namespace Lessie.Application.Payments;

public interface IPaymentService
{
    Task<IReadOnlyList<CreditPlanDto>> GetActiveCreditPlansAsync(CancellationToken cancellationToken);

    Task<CreatePaymentPreferenceResponse> CreatePreferenceAsync(
        Guid userId,
        CreatePaymentPreferenceRequest request,
        string returnBaseUrl,
        string notificationBaseUrl,
        CancellationToken cancellationToken);

    Task<PaymentNotificationProcessResponse> ProcessMercadoPagoPaymentNotificationAsync(
        MercadoPagoPaymentNotificationRequest request,
        CancellationToken cancellationToken);
}
