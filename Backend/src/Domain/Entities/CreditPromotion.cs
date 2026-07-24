namespace Lessie.Domain.Entities;

public sealed class CreditPromotion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CreditPlanId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public int BonusCredits { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public CreditPlan? CreditPlan { get; set; }
    public ICollection<PaymentOrder> PaymentOrders { get; set; } = new List<PaymentOrder>();
}
