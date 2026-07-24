namespace Lessie.Domain.Entities;

public sealed class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public bool IsPaid { get; set; }
    public DateTimeOffset? PaidUntil { get; set; }
    public DateTimeOffset? LastPaymentAt { get; set; }
    public string PaymentProvider { get; set; } = "";
    public string ExternalReference { get; set; } = "";
    public string Notes { get; set; } = "";
    public int ResumeAnalysisCount { get; set; }
    public int ResumeAnalysisLimit { get; set; } = 20;
    public int ChatConversationCount { get; set; }
    public int ChatConversationLimit { get; set; } = 50;
    public int InterviewAnalysisCount { get; set; }
    public int InterviewAnalysisLimit { get; set; } = 5;
    public int CreditBalance { get; set; }
    public int TotalCreditsPurchased { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }

    public bool HasActiveAccess(DateTimeOffset now) => PaidUntil.HasValue && PaidUntil.Value >= now;
}
