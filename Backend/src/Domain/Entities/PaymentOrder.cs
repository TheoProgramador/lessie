namespace Lessie.Domain.Entities;

public sealed class PaymentOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CreditPlanId { get; set; }
    public Guid? CreditPromotionId { get; set; }
    public string Provider { get; set; } = "MercadoPago";
    public string Status { get; set; } = "pending";
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string CurrencyId { get; set; } = "BRL";
    public int Credits { get; set; }
    public int BonusCredits { get; set; }
    public string ExternalReference { get; set; } = "";
    public string PreferenceId { get; set; } = "";
    public string MercadoPagoPaymentId { get; set; } = "";
    public string StatusDetail { get; set; } = "";
    public string InitPoint { get; set; } = "";
    public string SandboxInitPoint { get; set; } = "";
    public string PromotionCode { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }

    public User? User { get; set; }
    public CreditPlan? CreditPlan { get; set; }
    public CreditPromotion? CreditPromotion { get; set; }
}
