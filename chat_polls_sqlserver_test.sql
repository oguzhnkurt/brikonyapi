-- Sohbet anketi (chat poll) - yerel SQL Server test scripti
-- BrikonYapiDb veritabanına karşı çalıştır (SSMS).
-- Migration Postgres'e özel olduğu için yereldeki SQL Server'da çalışamıyor;
-- bu script aynı şemayı doğru SQL Server sözdizimiyle oluşturur ve migration'ı
-- "uygulanmış" olarak işaretler.

USE BrikonYapiDb;
GO

-- ── ChatPolls ────────────────────────────────────────────────
CREATE TABLE [ChatPolls] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ProjectId] int NOT NULL,
    [Question] nvarchar(300) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ChatPolls] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChatPolls_Projects_ProjectId] FOREIGN KEY ([ProjectId])
        REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_ChatPolls_ProjectId] ON [ChatPolls] ([ProjectId]);
GO

-- ── ChatPollOptions ──────────────────────────────────────────
CREATE TABLE [ChatPollOptions] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ChatPollId] int NOT NULL,
    [Text] nvarchar(200) NOT NULL,
    [OrderIndex] int NOT NULL CONSTRAINT [DF_ChatPollOptions_OrderIndex] DEFAULT (0),
    CONSTRAINT [PK_ChatPollOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChatPollOptions_ChatPolls_ChatPollId] FOREIGN KEY ([ChatPollId])
        REFERENCES [ChatPolls] ([Id]) ON DELETE CASCADE
);
CREATE INDEX [IX_ChatPollOptions_ChatPollId] ON [ChatPollOptions] ([ChatPollId]);
GO

-- ── ChatPollVotes ────────────────────────────────────────────
CREATE TABLE [ChatPollVotes] (
    [Id] int NOT NULL IDENTITY(1,1),
    [ChatPollId] int NOT NULL,
    [ChatPollOptionId] int NOT NULL,
    [OwnerId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_ChatPollVotes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChatPollVotes_ChatPolls_ChatPollId] FOREIGN KEY ([ChatPollId])
        REFERENCES [ChatPolls] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChatPollVotes_ChatPollOptions_ChatPollOptionId] FOREIGN KEY ([ChatPollOptionId])
        REFERENCES [ChatPollOptions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChatPollVotes_Owners_OwnerId] FOREIGN KEY ([OwnerId])
        REFERENCES [Owners] ([Id]) ON DELETE CASCADE
);
CREATE UNIQUE INDEX [IX_ChatPollVotes_ChatPollId_OwnerId] ON [ChatPollVotes] ([ChatPollId], [OwnerId]);
CREATE INDEX [IX_ChatPollVotes_ChatPollOptionId] ON [ChatPollVotes] ([ChatPollOptionId]);
CREATE INDEX [IX_ChatPollVotes_OwnerId] ON [ChatPollVotes] ([OwnerId]);
GO

-- ── ChatMessages: anket bağlantısı ───────────────────────────
ALTER TABLE [ChatMessages] ADD
    [IsPoll] bit NOT NULL CONSTRAINT [DF_ChatMessages_IsPoll] DEFAULT (0),
    [ChatPollId] int NULL;
GO

CREATE INDEX [IX_ChatMessages_ChatPollId] ON [ChatMessages] ([ChatPollId]);
-- NOT: ON DELETE SET NULL, "ChatPolls.ProjectId" ve "ChatMessages.ProjectId" ikisi de
-- Projects'e CASCADE bağlı olduğundan SQL Server'da "multiple cascade paths" hatası verir
-- (Msg 1785). ChatPollOptionId FK'sindeki NO ACTION deseniyle aynı çözüm uygulanır.
ALTER TABLE [ChatMessages] ADD CONSTRAINT [FK_ChatMessages_ChatPolls_ChatPollId]
    FOREIGN KEY ([ChatPollId]) REFERENCES [ChatPolls] ([Id]) ON DELETE NO ACTION;
GO

-- ── Migration'ı "uygulanmış" olarak işaretle ────────────────
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260818140000_AddChatPolls', N'8.0.0');
GO
