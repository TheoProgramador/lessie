namespace Lessie.Domain.Entities;

public sealed class CreditPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int Credits { get; set; }
    public decimal Price { get; set; }
    public string CurrencyId { get; set; } = "BRL";
    public string Badge { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CreditPromotion> Promotions { get; set; } = new List<CreditPromotion>();
    public ICollection<PaymentOrder> PaymentOrders { get; set; } = new List<PaymentOrder>();
}
