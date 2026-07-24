namespace Lessie.Application.Payments;

public sealed record PaymentNotificationProcessResponse(
    Guid? OrderId,
    string PaymentId,
    string Status,
    string StatusDetail,
    bool CreditsGranted,
    int Credits,
    int BonusCredits);
