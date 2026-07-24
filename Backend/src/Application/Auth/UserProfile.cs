namespace Lessie.Application.Auth;

public sealed record UserProfile(
    Guid Id,
    string Name,
    string Email,
    string? PictureUrl,
    bool IsAdmin,
    bool HasActiveSubscription,
    bool IsPaid,
    DateTimeOffset? PaidUntil,
    int ResumeAnalysisCount,
    int ResumeAnalysisLimit,
    int ChatConversationCount,
    int ChatConversationLimit,
    int InterviewAnalysisCount,
    int InterviewAnalysisLimit,
    int CreditBalance,
    int TotalCreditsPurchased);
