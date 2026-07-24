using Lessie.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lessie.Infrastructure.Persistence;

public sealed class LessieDbContext(DbContextOptions<LessieDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<UserProviderApiKey> UserProviderApiKeys => Set<UserProviderApiKey>();
    public DbSet<PeopleDiscoverySearchText> PeopleDiscoverySearchTexts => Set<PeopleDiscoverySearchText>();
    public DbSet<PeopleDiscoverySavedSearch> PeopleDiscoverySavedSearches => Set<PeopleDiscoverySavedSearch>();
    public DbSet<PeopleDiscoverySavedSearchResult> PeopleDiscoverySavedSearchResults => Set<PeopleDiscoverySavedSearchResult>();
    public DbSet<OpportunitySearchText> OpportunitySearchTexts => Set<OpportunitySearchText>();
    public DbSet<OpportunitySavedSearch> OpportunitySavedSearches => Set<OpportunitySavedSearch>();
    public DbSet<OpportunitySavedSearchResult> OpportunitySavedSearchResults => Set<OpportunitySavedSearchResult>();
    public DbSet<ResumeImprovementSession> ResumeImprovementSessions => Set<ResumeImprovementSession>();
    public DbSet<ResumeImprovementMessage> ResumeImprovementMessages => Set<ResumeImprovementMessage>();
    public DbSet<ResumeImprovementDocumentChunk> ResumeImprovementDocumentChunks => Set<ResumeImprovementDocumentChunk>();
    public DbSet<CreditPlan> CreditPlans => Set<CreditPlan>();
    public DbSet<CreditPromotion> CreditPromotions => Set<CreditPromotion>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.GoogleId).HasMaxLength(128);
            entity.Property(x => x.PictureUrl).HasMaxLength(1024);
            entity.Property(x => x.IsAdmin).HasDefaultValue(false);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.GoogleId).IsUnique().HasFilter("[GoogleId] IS NOT NULL");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.ToTable("UserSubscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PaymentProvider).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ExternalReference).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ResumeAnalysisCount).HasDefaultValue(0);
            entity.Property(x => x.ResumeAnalysisLimit).HasDefaultValue(20);
            entity.Property(x => x.ChatConversationCount).HasDefaultValue(0);
            entity.Property(x => x.ChatConversationLimit).HasDefaultValue(50);
            entity.Property(x => x.InterviewAnalysisCount).HasDefaultValue(0);
            entity.Property(x => x.InterviewAnalysisLimit).HasDefaultValue(5);
            entity.Property(x => x.CreditBalance).HasDefaultValue(0);
            entity.Property(x => x.TotalCreditsPurchased).HasDefaultValue(0);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne(x => x.User)
                .WithOne(x => x.Subscription)
                .HasForeignKey<UserSubscription>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserProviderApiKey>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EncryptedApiKey).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditPlan>(entity =>
        {
            entity.ToTable("CreditPlans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyId).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Badge).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.SortOrder });
        });

        modelBuilder.Entity<CreditPromotion>(entity =>
        {
            entity.ToTable("CreditPromotions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.StartsAt, x.EndsAt });
            entity.HasOne(x => x.CreditPlan)
                .WithMany(x => x.Promotions)
                .HasForeignKey(x => x.CreditPlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PaymentOrder>(entity =>
        {
            entity.ToTable("PaymentOrders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.OriginalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.FinalAmount).HasPrecision(18, 2);
            entity.Property(x => x.CurrencyId).HasMaxLength(3).IsRequired();
            entity.Property(x => x.ExternalReference).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PreferenceId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MercadoPagoPaymentId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.StatusDetail).HasMaxLength(120).IsRequired();
            entity.Property(x => x.InitPoint).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.SandboxInitPoint).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.PromotionCode).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.ExternalReference).IsUnique();
            entity.HasIndex(x => x.PreferenceId);
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreditPlan)
                .WithMany(x => x.PaymentOrders)
                .HasForeignKey(x => x.CreditPlanId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.CreditPromotion)
                .WithMany(x => x.PaymentOrders)
                .HasForeignKey(x => x.CreditPromotionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PeopleDiscoverySearchText>(entity =>
        {
            entity.ToTable("PeopleDiscoverySearchTexts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SearchText).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.QueryKey).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.QueryKey }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PeopleDiscoverySavedSearch>(entity =>
        {
            entity.ToTable("PeopleDiscoverySavedSearches");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PeopleDiscoverySearchTextId);
            entity.HasOne(x => x.SearchText)
                .WithMany(x => x.SavedSearches)
                .HasForeignKey(x => x.PeopleDiscoverySearchTextId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PeopleDiscoverySavedSearchResult>(entity =>
        {
            entity.ToTable("PeopleDiscoverySavedSearchResults");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResultKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Company).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ContactInfo).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ProfileUrl).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ResumeSent).HasDefaultValue(false);
            entity.HasIndex(x => new { x.PeopleDiscoverySavedSearchId, x.ResultKey }).IsUnique();
            entity.HasOne(x => x.PeopleDiscoverySavedSearch)
                .WithMany(x => x.Results)
                .HasForeignKey(x => x.PeopleDiscoverySavedSearchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpportunitySearchText>(entity =>
        {
            entity.ToTable("OpportunitySearchTexts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SearchText).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.QueryKey).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.QueryKey }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpportunitySavedSearch>(entity =>
        {
            entity.ToTable("OpportunitySavedSearches");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OpportunitySearchTextId);
            entity.HasOne(x => x.SearchText)
                .WithMany(x => x.SavedSearches)
                .HasForeignKey(x => x.OpportunitySearchTextId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<OpportunitySavedSearchResult>(entity =>
        {
            entity.ToTable("OpportunitySavedSearchResults");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResultKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.JobId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(600).IsRequired();
            entity.Property(x => x.Company).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Date).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Requirements).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.ApplyUrl).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.ContactEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ContactSubject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.OpportunitySavedSearchId, x.ResultKey }).IsUnique();
            entity.HasOne(x => x.OpportunitySavedSearch)
                .WithMany(x => x.Results)
                .HasForeignKey(x => x.OpportunitySavedSearchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResumeImprovementSession>(entity =>
        {
            entity.ToTable("ResumeImprovementSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ResumeFileName).HasMaxLength(512).IsRequired();
            entity.Property(x => x.JobContextSummary).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.ChatSummary).IsRequired();
            entity.Property(x => x.CurrentOptimizedResume).IsRequired();
            entity.Property(x => x.AtsAnalysisJson).IsRequired();
            entity.Property(x => x.CanonicalResumeJson).IsRequired();
            entity.Property(x => x.LinkedInProfileUrl).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.GitHubProfileUrl).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.PortfolioUrl).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.UpdatedAt });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResumeImprovementMessage>(entity =>
        {
            entity.ToTable("ResumeImprovementMessages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(24).IsRequired();
            entity.Property(x => x.CompactContent).HasMaxLength(3000).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => new { x.ResumeImprovementSessionId, x.CreatedAt });
            entity.HasOne(x => x.Session)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ResumeImprovementSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResumeImprovementDocumentChunk>(entity =>
        {
            entity.ToTable("ResumeImprovementDocumentChunks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Keywords).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => new { x.ResumeImprovementSessionId, x.Source, x.ChunkIndex }).IsUnique();
            entity.HasOne(x => x.Session)
                .WithMany(x => x.DocumentChunks)
                .HasForeignKey(x => x.ResumeImprovementSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
