namespace Lessie.Application.Payments;

public sealed record CreatePaymentPreferenceResponse(
    Guid OrderId,
    string PreferenceId,
    string CheckoutUrl,
    string InitPoint,
    string SandboxInitPoint,
    string PublicKey,
    decimal OriginalAmount,
    decimal DiscountAmount,
    decimal FinalAmount,
    int Credits,
    int BonusCredits,
    string CurrencyId);
