using Microsoft.EntityFrameworkCore;

namespace Lessie.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task EnsureDevelopmentSchemaAsync(LessieDbContext dbContext)
    {
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[Users]', 'IsAdmin') IS NULL
            BEGIN
                ALTER TABLE [Users] ADD [IsAdmin] bit NOT NULL CONSTRAINT [DF_Users_IsAdmin] DEFAULT 0;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [UserSubscriptions] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [IsPaid] bit NOT NULL,
                    [PaidUntil] datetimeoffset NULL,
                    [LastPaymentAt] datetimeoffset NULL,
                    [PaymentProvider] nvarchar(80) NOT NULL,
                    [ExternalReference] nvarchar(200) NOT NULL,
                    [Notes] nvarchar(1000) NOT NULL,
                    [ResumeAnalysisCount] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ResumeAnalysisCount] DEFAULT 0,
                    [ResumeAnalysisLimit] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ResumeAnalysisLimit] DEFAULT 20,
                    [ChatConversationCount] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ChatConversationCount] DEFAULT 0,
                    [ChatConversationLimit] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ChatConversationLimit] DEFAULT 50,
                    [InterviewAnalysisCount] int NOT NULL CONSTRAINT [DF_UserSubscriptions_InterviewAnalysisCount] DEFAULT 0,
                    [InterviewAnalysisLimit] int NOT NULL CONSTRAINT [DF_UserSubscriptions_InterviewAnalysisLimit] DEFAULT 5,
                    [CreditBalance] int NOT NULL CONSTRAINT [DF_UserSubscriptions_CreditBalance] DEFAULT 0,
                    [TotalCreditsPurchased] int NOT NULL CONSTRAINT [DF_UserSubscriptions_TotalCreditsPurchased] DEFAULT 0,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_UserSubscriptions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_UserSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_UserSubscriptions_UserId] ON [UserSubscriptions] ([UserId]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'ResumeAnalysisCount') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [ResumeAnalysisCount] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ResumeAnalysisCount] DEFAULT 0;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'ChatConversationCount') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [ChatConversationCount] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ChatConversationCount] DEFAULT 0;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'ChatConversationLimit') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [ChatConversationLimit] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ChatConversationLimit] DEFAULT 50;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'ResumeAnalysisLimit') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [ResumeAnalysisLimit] int NOT NULL CONSTRAINT [DF_UserSubscriptions_ResumeAnalysisLimit] DEFAULT 20;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'InterviewAnalysisCount') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [InterviewAnalysisCount] int NOT NULL CONSTRAINT [DF_UserSubscriptions_InterviewAnalysisCount] DEFAULT 0;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'InterviewAnalysisLimit') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [InterviewAnalysisLimit] int NOT NULL CONSTRAINT [DF_UserSubscriptions_InterviewAnalysisLimit] DEFAULT 5;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'CreditBalance') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [CreditBalance] int NOT NULL CONSTRAINT [DF_UserSubscriptions_CreditBalance] DEFAULT 0;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserSubscriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[UserSubscriptions]', 'TotalCreditsPurchased') IS NULL
            BEGIN
                ALTER TABLE [UserSubscriptions] ADD [TotalCreditsPurchased] int NOT NULL CONSTRAINT [DF_UserSubscriptions_TotalCreditsPurchased] DEFAULT 0;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL
            BEGIN
                UPDATE [Users]
                SET
                    [IsAdmin] = 1,
                    [UpdatedAt] = SYSDATETIMEOFFSET()
                WHERE LOWER([Email]) = N'theo.miliani@gmail.com';

                INSERT INTO [UserSubscriptions] (
                    [Id],
                    [UserId],
                    [IsPaid],
                    [PaidUntil],
                    [LastPaymentAt],
                    [PaymentProvider],
                    [ExternalReference],
                    [Notes],
                    [ResumeAnalysisCount],
                    [ResumeAnalysisLimit],
                    [ChatConversationCount],
                    [ChatConversationLimit],
                    [InterviewAnalysisCount],
                    [InterviewAnalysisLimit],
                    [CreditBalance],
                    [TotalCreditsPurchased],
                    [CreatedAt],
                    [UpdatedAt]
                )
                SELECT
                    NEWID(),
                    [Id],
                    CASE WHEN [IsAdmin] = 1 THEN 1 ELSE 0 END,
                    CASE WHEN [IsAdmin] = 1 THEN DATEADD(year, 10, SYSDATETIMEOFFSET()) ELSE NULL END,
                    CASE WHEN [IsAdmin] = 1 THEN SYSDATETIMEOFFSET() ELSE NULL END,
                    CASE WHEN [IsAdmin] = 1 THEN N'system' ELSE N'' END,
                    CASE WHEN [IsAdmin] = 1 THEN N'admin-seed' ELSE N'' END,
                    CASE WHEN [IsAdmin] = 1 THEN N'Assinatura inicial para admin existente.' ELSE N'' END,
                    0,
                    20,
                    0,
                    50,
                    0,
                    5,
                    0,
                    0,
                    SYSDATETIMEOFFSET(),
                    SYSDATETIMEOFFSET()
                FROM [Users] u
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [UserSubscriptions] s
                    WHERE s.[UserId] = u.[Id]
                );

                UPDATE s
                SET
                    [IsPaid] = 1,
                    [PaidUntil] = CASE WHEN s.[PaidUntil] IS NULL OR s.[PaidUntil] < DATEADD(year, 1, SYSDATETIMEOFFSET()) THEN DATEADD(year, 10, SYSDATETIMEOFFSET()) ELSE s.[PaidUntil] END,
                    [PaymentProvider] = CASE WHEN s.[PaymentProvider] = N'' THEN N'system' ELSE s.[PaymentProvider] END,
                    [ExternalReference] = CASE WHEN s.[ExternalReference] = N'' THEN N'seed-admin' ELSE s.[ExternalReference] END,
                    [Notes] = CASE WHEN s.[Notes] = N'' THEN N'Administrador do sistema.' ELSE s.[Notes] END,
                    [UpdatedAt] = SYSDATETIMEOFFSET()
                FROM [UserSubscriptions] s
                INNER JOIN [Users] u ON u.[Id] = s.[UserId]
                WHERE u.[IsAdmin] = 1;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[UserProviderApiKeys]', N'U') IS NULL
            BEGIN
                CREATE TABLE [UserProviderApiKeys] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [Provider] nvarchar(64) NOT NULL,
                    [EncryptedApiKey] nvarchar(max) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL,
                    [LastUsedAt] datetimeoffset NULL,
                    [IsActive] bit NOT NULL,
                    CONSTRAINT [PK_UserProviderApiKeys] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_UserProviderApiKeys_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_UserProviderApiKeys_UserId_Provider] ON [UserProviderApiKeys] ([UserId], [Provider]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[CreditPlans]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CreditPlans] (
                    [Id] uniqueidentifier NOT NULL,
                    [Code] nvarchar(64) NOT NULL,
                    [Name] nvarchar(160) NOT NULL,
                    [Description] nvarchar(1000) NOT NULL,
                    [Credits] int NOT NULL,
                    [Price] decimal(18,2) NOT NULL,
                    [CurrencyId] nvarchar(3) NOT NULL,
                    [Badge] nvarchar(80) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_CreditPlans] PRIMARY KEY ([Id])
                );

                CREATE UNIQUE INDEX [IX_CreditPlans_Code] ON [CreditPlans] ([Code]);
                CREATE INDEX [IX_CreditPlans_IsActive_SortOrder] ON [CreditPlans] ([IsActive], [SortOrder]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[CreditPromotions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CreditPromotions] (
                    [Id] uniqueidentifier NOT NULL,
                    [CreditPlanId] uniqueidentifier NULL,
                    [Code] nvarchar(80) NOT NULL,
                    [Name] nvarchar(180) NOT NULL,
                    [DiscountPercent] decimal(5,2) NULL,
                    [DiscountAmount] decimal(18,2) NULL,
                    [BonusCredits] int NOT NULL,
                    [StartsAt] datetimeoffset NULL,
                    [EndsAt] datetimeoffset NULL,
                    [MaxRedemptions] int NULL,
                    [RedemptionCount] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_CreditPromotions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CreditPromotions_CreditPlans_CreditPlanId] FOREIGN KEY ([CreditPlanId]) REFERENCES [CreditPlans] ([Id]) ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX [IX_CreditPromotions_Code] ON [CreditPromotions] ([Code]);
                CREATE INDEX [IX_CreditPromotions_IsActive_StartsAt_EndsAt] ON [CreditPromotions] ([IsActive], [StartsAt], [EndsAt]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PaymentOrders]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PaymentOrders] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [CreditPlanId] uniqueidentifier NOT NULL,
                    [CreditPromotionId] uniqueidentifier NULL,
                    [Provider] nvarchar(80) NOT NULL,
                    [Status] nvarchar(40) NOT NULL,
                    [OriginalAmount] decimal(18,2) NOT NULL,
                    [DiscountAmount] decimal(18,2) NOT NULL,
                    [FinalAmount] decimal(18,2) NOT NULL,
                    [CurrencyId] nvarchar(3) NOT NULL,
                    [Credits] int NOT NULL,
                    [BonusCredits] int NOT NULL,
                    [ExternalReference] nvarchar(120) NOT NULL,
                    [PreferenceId] nvarchar(200) NOT NULL,
                    [MercadoPagoPaymentId] nvarchar(80) NOT NULL,
                    [StatusDetail] nvarchar(120) NOT NULL,
                    [InitPoint] nvarchar(1200) NOT NULL,
                    [SandboxInitPoint] nvarchar(1200) NOT NULL,
                    [PromotionCode] nvarchar(80) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL,
                    [PaidAt] datetimeoffset NULL,
                    CONSTRAINT [PK_PaymentOrders] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PaymentOrders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_PaymentOrders_CreditPlans_CreditPlanId] FOREIGN KEY ([CreditPlanId]) REFERENCES [CreditPlans] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PaymentOrders_CreditPromotions_CreditPromotionId] FOREIGN KEY ([CreditPromotionId]) REFERENCES [CreditPromotions] ([Id]) ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX [IX_PaymentOrders_ExternalReference] ON [PaymentOrders] ([ExternalReference]);
                CREATE INDEX [IX_PaymentOrders_PreferenceId] ON [PaymentOrders] ([PreferenceId]);
                CREATE INDEX [IX_PaymentOrders_UserId_CreatedAt] ON [PaymentOrders] ([UserId], [CreatedAt]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PaymentOrders]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PaymentOrders]', 'MercadoPagoPaymentId') IS NULL
            BEGIN
                ALTER TABLE [PaymentOrders] ADD [MercadoPagoPaymentId] nvarchar(80) NOT NULL CONSTRAINT [DF_PaymentOrders_MercadoPagoPaymentId] DEFAULT N'';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PaymentOrders]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PaymentOrders]', 'StatusDetail') IS NULL
            BEGIN
                ALTER TABLE [PaymentOrders] ADD [StatusDetail] nvarchar(120) NOT NULL CONSTRAINT [DF_PaymentOrders_StatusDetail] DEFAULT N'';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[CreditPlans]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [CreditPlans] WHERE [Code] = N'starter')
                BEGIN
                    INSERT INTO [CreditPlans] ([Id], [Code], [Name], [Description], [Credits], [Price], [CurrencyId], [Badge], [SortOrder], [IsActive], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), N'starter', N'Inicial', N'Para testar a Lessie em uma rodada curta de curriculo e vagas.', 40, 19.00, N'BRL', N'', 10, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                END

                IF NOT EXISTS (SELECT 1 FROM [CreditPlans] WHERE [Code] = N'focus')
                BEGIN
                    INSERT INTO [CreditPlans] ([Id], [Code], [Name], [Description], [Credits], [Price], [CurrencyId], [Badge], [SortOrder], [IsActive], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), N'focus', N'Foco', N'Para acompanhar uma semana de candidatura com mais contexto.', 140, 49.00, N'BRL', N'Mais escolhido', 20, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                END

                IF NOT EXISTS (SELECT 1 FROM [CreditPlans] WHERE [Code] = N'pro')
                BEGIN
                    INSERT INTO [CreditPlans] ([Id], [Code], [Name], [Description], [Credits], [Price], [CurrencyId], [Badge], [SortOrder], [IsActive], [CreatedAt], [UpdatedAt])
                    VALUES (NEWID(), N'pro', N'Pro', N'Para uso intenso em varias vagas, versoes e pesquisas.', 360, 99.00, N'BRL', N'', 30, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                END
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySearchTexts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PeopleDiscoverySearchTexts] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [SearchText] nvarchar(4000) NOT NULL,
                    [QueryKey] nvarchar(256) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [LastUsedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_PeopleDiscoverySearchTexts] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PeopleDiscoverySearchTexts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_PeopleDiscoverySearchTexts_UserId_QueryKey] ON [PeopleDiscoverySearchTexts] ([UserId], [QueryKey]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearches]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PeopleDiscoverySavedSearches] (
                    [Id] uniqueidentifier NOT NULL,
                    [PeopleDiscoverySearchTextId] uniqueidentifier NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [LastRunAt] datetimeoffset NOT NULL,
                    [RunCount] int NOT NULL,
                    CONSTRAINT [PK_PeopleDiscoverySavedSearches] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PeopleDiscoverySavedSearches_SearchTexts_PeopleDiscoverySearchTextId] FOREIGN KEY ([PeopleDiscoverySearchTextId]) REFERENCES [PeopleDiscoverySearchTexts] ([Id]) ON DELETE NO ACTION
                );

                CREATE INDEX [IX_PeopleDiscoverySavedSearches_PeopleDiscoverySearchTextId] ON [PeopleDiscoverySavedSearches] ([PeopleDiscoverySearchTextId]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearches]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'PeopleDiscoverySearchTextId') IS NULL
            BEGIN
                ALTER TABLE [PeopleDiscoverySavedSearches] ADD [PeopleDiscoverySearchTextId] uniqueidentifier NULL;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearches]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'PeopleDiscoverySearchTextId') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'UserId') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'QueryKey') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'SearchText') IS NOT NULL
                   AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'Query') IS NOT NULL
                BEGIN
                    EXEC(N'
                    INSERT INTO [PeopleDiscoverySearchTexts] ([Id], [UserId], [SearchText], [QueryKey], [CreatedAt], [LastUsedAt])
                    SELECT NEWID(), s.[UserId], LEFT(COALESCE(NULLIF(s.[SearchText], N''''), s.[Query], N''''), 4000), s.[QueryKey], s.[CreatedAt], s.[LastRunAt]
                    FROM [PeopleDiscoverySavedSearches] s
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [PeopleDiscoverySearchTexts] t
                        WHERE t.[UserId] = s.[UserId] AND t.[QueryKey] = s.[QueryKey]
                    );');
                END
                ELSE IF COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'SearchText') IS NOT NULL
                BEGIN
                    EXEC(N'
                    INSERT INTO [PeopleDiscoverySearchTexts] ([Id], [UserId], [SearchText], [QueryKey], [CreatedAt], [LastUsedAt])
                    SELECT NEWID(), s.[UserId], LEFT(COALESCE(NULLIF(s.[SearchText], N''''), N''''), 4000), s.[QueryKey], s.[CreatedAt], s.[LastRunAt]
                    FROM [PeopleDiscoverySavedSearches] s
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [PeopleDiscoverySearchTexts] t
                        WHERE t.[UserId] = s.[UserId] AND t.[QueryKey] = s.[QueryKey]
                    );');
                END
                ELSE IF COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'Query') IS NOT NULL
                BEGIN
                    EXEC(N'
                    INSERT INTO [PeopleDiscoverySearchTexts] ([Id], [UserId], [SearchText], [QueryKey], [CreatedAt], [LastUsedAt])
                    SELECT NEWID(), s.[UserId], LEFT(COALESCE(NULLIF(s.[Query], N''''), N''''), 4000), s.[QueryKey], s.[CreatedAt], s.[LastRunAt]
                    FROM [PeopleDiscoverySavedSearches] s
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [PeopleDiscoverySearchTexts] t
                        WHERE t.[UserId] = s.[UserId] AND t.[QueryKey] = s.[QueryKey]
                    );');
                END
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearches]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'PeopleDiscoverySearchTextId') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'UserId') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'QueryKey') IS NOT NULL
            BEGIN
                EXEC(N'
                UPDATE s
                SET [PeopleDiscoverySearchTextId] = t.[Id]
                FROM [PeopleDiscoverySavedSearches] s
                INNER JOIN [PeopleDiscoverySearchTexts] t
                    ON t.[UserId] = s.[UserId] AND t.[QueryKey] = s.[QueryKey];');
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearches]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'PeopleDiscoverySearchTextId') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] = N'FK_PeopleDiscoverySavedSearches_SearchTexts_PeopleDiscoverySearchTextId'
               )
            BEGIN
                EXEC(N'
                IF NOT EXISTS (
                    SELECT 1
                    FROM [PeopleDiscoverySavedSearches]
                    WHERE [PeopleDiscoverySearchTextId] IS NULL
                )
                BEGIN
                    ALTER TABLE [PeopleDiscoverySavedSearches] ALTER COLUMN [PeopleDiscoverySearchTextId] uniqueidentifier NOT NULL;
                    ALTER TABLE [PeopleDiscoverySavedSearches]
                        ADD CONSTRAINT [FK_PeopleDiscoverySavedSearches_SearchTexts_PeopleDiscoverySearchTextId]
                        FOREIGN KEY ([PeopleDiscoverySearchTextId]) REFERENCES [PeopleDiscoverySearchTexts] ([Id]) ON DELETE NO ACTION;
                END');
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearches]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'PeopleDiscoverySearchTextId') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_PeopleDiscoverySavedSearches_PeopleDiscoverySearchTextId'
                      AND [object_id] = OBJECT_ID(N'[PeopleDiscoverySavedSearches]')
               )
            BEGIN
                EXEC(N'CREATE INDEX [IX_PeopleDiscoverySavedSearches_PeopleDiscoverySearchTextId] ON [PeopleDiscoverySavedSearches] ([PeopleDiscoverySearchTextId]);');
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearches]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearches]', 'SearchText') IS NOT NULL
            BEGIN
                ALTER TABLE [PeopleDiscoverySavedSearches] DROP COLUMN [SearchText];
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearchResults]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PeopleDiscoverySavedSearchResults] (
                    [Id] uniqueidentifier NOT NULL,
                    [PeopleDiscoverySavedSearchId] uniqueidentifier NOT NULL,
                    [ResultKey] nvarchar(128) NOT NULL,
                    [Name] nvarchar(300) NOT NULL,
                    [Title] nvarchar(2000) NOT NULL,
                    [Company] nvarchar(500) NOT NULL,
                    [Location] nvarchar(300) NOT NULL,
                    [ContactInfo] nvarchar(1000) NOT NULL,
                    [ProfileUrl] nvarchar(1200) NOT NULL,
                    [Source] nvarchar(64) NOT NULL,
                    [ResumeSent] bit NOT NULL CONSTRAINT [DF_PeopleDiscoverySavedSearchResults_ResumeSent] DEFAULT 0,
                    [ResumeSentAt] datetimeoffset NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [LastSeenAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_PeopleDiscoverySavedSearchResults] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PeopleDiscoverySavedSearchResults_Searches_PeopleDiscoverySavedSearchId] FOREIGN KEY ([PeopleDiscoverySavedSearchId]) REFERENCES [PeopleDiscoverySavedSearches] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_PeopleDiscoverySavedSearchResults_Search_ResultKey] ON [PeopleDiscoverySavedSearchResults] ([PeopleDiscoverySavedSearchId], [ResultKey]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearchResults]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearchResults]', 'ResumeSent') IS NULL
            BEGIN
                ALTER TABLE [PeopleDiscoverySavedSearchResults] ADD [ResumeSent] bit NOT NULL CONSTRAINT [DF_PeopleDiscoverySavedSearchResults_ResumeSent] DEFAULT 0;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearchResults]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearchResults]', 'ResumeSentAt') IS NULL
            BEGIN
                ALTER TABLE [PeopleDiscoverySavedSearchResults] ADD [ResumeSentAt] datetimeoffset NULL;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearchResults]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearchResults]', 'SearchText') IS NOT NULL
            BEGIN
                ALTER TABLE [PeopleDiscoverySavedSearchResults] DROP COLUMN [SearchText];
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PeopleDiscoverySavedSearchResults]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearchResults]', 'PeopleDiscoverySavedSearchId') IS NULL
               AND COL_LENGTH(N'[PeopleDiscoverySavedSearchResults]', 'SearchId') IS NOT NULL
            BEGIN
                EXEC sp_rename 'PeopleDiscoverySavedSearchResults.SearchId', 'PeopleDiscoverySavedSearchId', 'COLUMN';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[OpportunitySearchTexts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [OpportunitySearchTexts] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [SearchText] nvarchar(4000) NOT NULL,
                    [QueryKey] nvarchar(256) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [LastUsedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_OpportunitySearchTexts] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_OpportunitySearchTexts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_OpportunitySearchTexts_UserId_QueryKey] ON [OpportunitySearchTexts] ([UserId], [QueryKey]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[OpportunitySavedSearches]', N'U') IS NULL
            BEGIN
                CREATE TABLE [OpportunitySavedSearches] (
                    [Id] uniqueidentifier NOT NULL,
                    [OpportunitySearchTextId] uniqueidentifier NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [LastRunAt] datetimeoffset NOT NULL,
                    [RunCount] int NOT NULL,
                    CONSTRAINT [PK_OpportunitySavedSearches] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_OpportunitySavedSearches_SearchTexts_OpportunitySearchTextId] FOREIGN KEY ([OpportunitySearchTextId]) REFERENCES [OpportunitySearchTexts] ([Id]) ON DELETE NO ACTION
                );

                CREATE INDEX [IX_OpportunitySavedSearches_OpportunitySearchTextId] ON [OpportunitySavedSearches] ([OpportunitySearchTextId]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[OpportunitySavedSearchResults]', N'U') IS NULL
            BEGIN
                CREATE TABLE [OpportunitySavedSearchResults] (
                    [Id] uniqueidentifier NOT NULL,
                    [OpportunitySavedSearchId] uniqueidentifier NOT NULL,
                    [ResultKey] nvarchar(128) NOT NULL,
                    [JobId] nvarchar(64) NOT NULL,
                    [Title] nvarchar(600) NOT NULL,
                    [Company] nvarchar(500) NOT NULL,
                    [Location] nvarchar(300) NOT NULL,
                    [Date] nvarchar(32) NOT NULL,
                    [Description] nvarchar(4000) NOT NULL,
                    [Requirements] nvarchar(4000) NOT NULL,
                    [Url] nvarchar(1200) NOT NULL,
                    [ApplyUrl] nvarchar(1200) NOT NULL,
                    [ContactEmail] nvarchar(320) NOT NULL,
                    [ContactSubject] nvarchar(500) NOT NULL,
                    [Source] nvarchar(64) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [LastSeenAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_OpportunitySavedSearchResults] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_OpportunitySavedSearchResults_Searches_OpportunitySavedSearchId] FOREIGN KEY ([OpportunitySavedSearchId]) REFERENCES [OpportunitySavedSearches] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_OpportunitySavedSearchResults_Search_ResultKey] ON [OpportunitySavedSearchResults] ([OpportunitySavedSearchId], [ResultKey]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementSessions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ResumeImprovementSessions] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [Title] nvarchar(300) NOT NULL,
                    [ResumeFileName] nvarchar(512) NOT NULL,
                    [JobContextSummary] nvarchar(4000) NOT NULL,
                    [ChatSummary] nvarchar(max) NOT NULL,
                    [CurrentOptimizedResume] nvarchar(max) NOT NULL,
                    [AtsAnalysisJson] nvarchar(max) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_AtsAnalysisJson] DEFAULT N'{{}}',
                    [CanonicalResumeJson] nvarchar(max) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_CanonicalResumeJson] DEFAULT N'{{}}',
                    [LinkedInProfileUrl] nvarchar(1000) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_LinkedInProfileUrl] DEFAULT N'',
                    [GitHubProfileUrl] nvarchar(1000) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_GitHubProfileUrl] DEFAULT N'',
                    [PortfolioUrl] nvarchar(1000) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_PortfolioUrl] DEFAULT N'',
                    [ReadyToExport] bit NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL,
                    [LastMessageAt] datetimeoffset NULL,
                    CONSTRAINT [PK_ResumeImprovementSessions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ResumeImprovementSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_ResumeImprovementSessions_UserId_UpdatedAt] ON [ResumeImprovementSessions] ([UserId], [UpdatedAt]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementSessions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[ResumeImprovementSessions]', 'LinkedInProfileUrl') IS NULL
            BEGIN
                ALTER TABLE [ResumeImprovementSessions] ADD [LinkedInProfileUrl] nvarchar(1000) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_LinkedInProfileUrl] DEFAULT N'';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementSessions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[ResumeImprovementSessions]', 'GitHubProfileUrl') IS NULL
            BEGIN
                ALTER TABLE [ResumeImprovementSessions] ADD [GitHubProfileUrl] nvarchar(1000) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_GitHubProfileUrl] DEFAULT N'';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementSessions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[ResumeImprovementSessions]', 'PortfolioUrl') IS NULL
            BEGIN
                ALTER TABLE [ResumeImprovementSessions] ADD [PortfolioUrl] nvarchar(1000) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_PortfolioUrl] DEFAULT N'';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementSessions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[ResumeImprovementSessions]', 'AtsAnalysisJson') IS NULL
            BEGIN
                    ALTER TABLE [ResumeImprovementSessions] ADD [AtsAnalysisJson] nvarchar(max) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_AtsAnalysisJson] DEFAULT N'{{}}';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementSessions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[ResumeImprovementSessions]', 'CanonicalResumeJson') IS NULL
            BEGIN
                    ALTER TABLE [ResumeImprovementSessions] ADD [CanonicalResumeJson] nvarchar(max) NOT NULL CONSTRAINT [DF_ResumeImprovementSessions_CanonicalResumeJson] DEFAULT N'{{}}';
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementMessages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ResumeImprovementMessages] (
                    [Id] uniqueidentifier NOT NULL,
                    [ResumeImprovementSessionId] uniqueidentifier NOT NULL,
                    [Role] nvarchar(24) NOT NULL,
                    [CompactContent] nvarchar(3000) NOT NULL,
                    [Content] nvarchar(max) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_ResumeImprovementMessages] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ResumeImprovementMessages_Sessions_ResumeImprovementSessionId] FOREIGN KEY ([ResumeImprovementSessionId]) REFERENCES [ResumeImprovementSessions] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_ResumeImprovementMessages_Session_CreatedAt] ON [ResumeImprovementMessages] ([ResumeImprovementSessionId], [CreatedAt]);
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementMessages]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[ResumeImprovementMessages]', 'Content') IS NULL
            BEGIN
                EXEC(N'ALTER TABLE [ResumeImprovementMessages] ADD [Content] nvarchar(max) NULL;');
                EXEC(N'UPDATE [ResumeImprovementMessages] SET [Content] = [CompactContent] WHERE [Content] IS NULL;');
                EXEC(N'ALTER TABLE [ResumeImprovementMessages] ALTER COLUMN [Content] nvarchar(max) NOT NULL;');
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ResumeImprovementDocumentChunks]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ResumeImprovementDocumentChunks] (
                    [Id] uniqueidentifier NOT NULL,
                    [ResumeImprovementSessionId] uniqueidentifier NOT NULL,
                    [Source] nvarchar(64) NOT NULL,
                    [ChunkIndex] int NOT NULL,
                    [Content] nvarchar(2000) NOT NULL,
                    [Keywords] nvarchar(1000) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    CONSTRAINT [PK_ResumeImprovementDocumentChunks] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ResumeImprovementDocumentChunks_Sessions_ResumeImprovementSessionId] FOREIGN KEY ([ResumeImprovementSessionId]) REFERENCES [ResumeImprovementSessions] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_ResumeImprovementDocumentChunks_Session_Source_Chunk] ON [ResumeImprovementDocumentChunks] ([ResumeImprovementSessionId], [Source], [ChunkIndex]);
            END
            """);
    }
}
