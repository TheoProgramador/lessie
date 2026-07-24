namespace Lessie.Application.Payments;

public sealed record CreatePaymentPreferenceRequest(string PlanCode, string? PromotionCode);
