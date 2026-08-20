-- Kat Maliki Portalı genişletmesi - yerel SQL Server test scripti
-- BrikonYapiDb veritabanına karşı çalıştır (SSMS).
-- Migration Postgres'e özel olduğu için yereldeki SQL Server'da çalışamıyor;
-- bu script aynı şemayı doğru SQL Server sözdizimiyle oluşturur ve migration'ı
-- "uygulanmış" olarak işaretler.

USE BrikonYapiDb;
GO

-- ── Projects: portal alanları ───────────────────────────────
ALTER TABLE [Projects] ADD
    [VirtualTourUrl] nvarchar(500) NULL,
    [EstimatedDeliveryDate] datetime2 NULL,
    [OverallProgressPercentage] int NOT NULL DEFAULT 0;
GO

-- ── PaymentSchedules: taksit detay alanları ─────────────────
ALTER TABLE [PaymentSchedules] ADD
    [InstallmentNo] int NOT NULL DEFAULT 0,
    [HakedisPercentage] int NULL,
    [PaidAt] datetime2 NULL;
GO

-- ── ProjectStages ───────────────────────────────────────────
CREATE TABLE [ProjectStages] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ProjectId] int NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [OrderIndex] int NOT NULL,
    [ThresholdPercentage] int NOT NULL,
    [Status] int NOT NULL,
    [CompletedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_ProjectStages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectStages_Projects_ProjectId] FOREIGN KEY ([ProjectId])
        REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_ProjectStages_ProjectId_OrderIndex] ON [ProjectStages] ([ProjectId], [OrderIndex]);
GO

-- ── SitePhotos ──────────────────────────────────────────────
CREATE TABLE [SitePhotos] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ProjectId] int NOT NULL,
    [ImagePath] nvarchar(500) NOT NULL,
    [Caption] nvarchar(200) NULL,
    [TakenAt] datetime2 NOT NULL,
    [Is360] bit NOT NULL,
    [OrderIndex] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SitePhotos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SitePhotos_Projects_ProjectId] FOREIGN KEY ([ProjectId])
        REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_SitePhotos_ProjectId_TakenAt] ON [SitePhotos] ([ProjectId], [TakenAt]);
GO

-- ── Polls ───────────────────────────────────────────────────
CREATE TABLE [Polls] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ProjectId] int NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Category] nvarchar(60) NULL,
    [Status] int NOT NULL,
    [StartsAt] datetime2 NULL,
    [EndsAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Polls] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Polls_Projects_ProjectId] FOREIGN KEY ([ProjectId])
        REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_Polls_ProjectId] ON [Polls] ([ProjectId]);
GO

-- ── PollOptions ─────────────────────────────────────────────
CREATE TABLE [PollOptions] (
    [Id] int NOT NULL IDENTITY(1,1),
    [PollId] int NOT NULL,
    [Text] nvarchar(200) NOT NULL,
    [ImagePath] nvarchar(500) NULL,
    [OrderIndex] int NOT NULL,
    CONSTRAINT [PK_PollOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PollOptions_Polls_PollId] FOREIGN KEY ([PollId])
        REFERENCES [Polls] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_PollOptions_PollId] ON [PollOptions] ([PollId]);
GO

-- ── PollVotes ───────────────────────────────────────────────
-- Not: PollOptionId FK'si NO ACTION (Restrict) - cift cascade yolu olusmasin.
CREATE TABLE [PollVotes] (
    [Id] int NOT NULL IDENTITY(1,1),
    [PollId] int NOT NULL,
    [PollOptionId] int NOT NULL,
    [OwnerId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PollVotes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PollVotes_Polls_PollId] FOREIGN KEY ([PollId])
        REFERENCES [Polls] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PollVotes_PollOptions_PollOptionId] FOREIGN KEY ([PollOptionId])
        REFERENCES [PollOptions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PollVotes_Owners_OwnerId] FOREIGN KEY ([OwnerId])
        REFERENCES [Owners] ([Id]) ON DELETE CASCADE
);
-- Bir malik bir oylamada yalnizca bir kez oy kullanabilir:
CREATE UNIQUE INDEX [IX_PollVotes_PollId_OwnerId] ON [PollVotes] ([PollId], [OwnerId]);
CREATE INDEX [IX_PollVotes_PollOptionId] ON [PollVotes] ([PollOptionId]);
CREATE INDEX [IX_PollVotes_OwnerId] ON [PollVotes] ([OwnerId]);
GO

-- ── ChatMessages ────────────────────────────────────────────
CREATE TABLE [ChatMessages] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ProjectId] int NOT NULL,
    [OwnerId] int NULL,
    [SenderUserId] nvarchar(450) NOT NULL,
    [SenderName] nvarchar(150) NOT NULL,
    [IsFromManagement] bit NOT NULL,
    [Body] nvarchar(2000) NOT NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedByUserId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChatMessages_Projects_ProjectId] FOREIGN KEY ([ProjectId])
        REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChatMessages_Owners_OwnerId] FOREIGN KEY ([OwnerId])
        REFERENCES [Owners] ([Id]) ON DELETE SET NULL
);
CREATE INDEX [IX_ChatMessages_ProjectId_CreatedAt] ON [ChatMessages] ([ProjectId], [CreatedAt]);
CREATE INDEX [IX_ChatMessages_OwnerId] ON [ChatMessages] ([OwnerId]);
GO

-- ── OwnerNotificationPreferences ────────────────────────────
CREATE TABLE [OwnerNotificationPreferences] (
    [Id] int NOT NULL IDENTITY(1,1),
    [OwnerId] int NOT NULL,
    [PushEnabled] bit NOT NULL,
    [SmsEnabled] bit NOT NULL,
    [EmailEnabled] bit NOT NULL,
    [NotifyPayment] bit NOT NULL,
    [NotifyProgress] bit NOT NULL,
    [NotifyPoll] bit NOT NULL,
    [NotifyChat] bit NOT NULL,
    [NotifyNews] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_OwnerNotificationPreferences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OwnerNotificationPreferences_Owners_OwnerId] FOREIGN KEY ([OwnerId])
        REFERENCES [Owners] ([Id]) ON DELETE CASCADE
);
CREATE UNIQUE INDEX [IX_OwnerNotificationPreferences_OwnerId] ON [OwnerNotificationPreferences] ([OwnerId]);
GO

-- ── Migration'ı "uygulanmış" olarak işaretle ────────────────
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260806150000_AddPortalModules', N'8.0.0');
GO
