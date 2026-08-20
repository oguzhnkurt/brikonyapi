-- Temsil Heyeti (committee membership) - yerel SQL Server test scripti
-- BrikonYapiDb veritabanına karşı çalıştır (SSMS).
-- Migration Postgres'e özel olduğu için yereldeki SQL Server'da çalışamıyor;
-- bu script aynı sütunu doğru SQL Server sözdizimiyle ekler ve migration'ı
-- "uygulanmış" olarak işaretler.

USE BrikonYapiDb;
GO

ALTER TABLE [OwnerProjectAccesses] ADD
    [IsCommitteeMember] bit NOT NULL CONSTRAINT [DF_OwnerProjectAccesses_IsCommitteeMember] DEFAULT (0);
GO

-- ── Migration'ı "uygulanmış" olarak işaretle ────────────────
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260820120000_AddCommitteeMembership', N'8.0.0');
GO
