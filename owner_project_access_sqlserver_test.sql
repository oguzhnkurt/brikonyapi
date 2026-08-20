-- Malik başına proje erişimi - yerel SQL Server test scripti
-- BrikonYapiDb veritabanına karşı çalıştır (SSMS).
-- Migration Postgres'e özel olduğu için yereldeki SQL Server'da çalışamıyor;
-- bu script aynı şemayı doğru SQL Server sözdizimiyle oluşturur ve migration'ı
-- "uygulanmış" olarak işaretler.

USE BrikonYapiDb;
GO

-- ── OwnerProjectAccesses ─────────────────────────────────────
CREATE TABLE [OwnerProjectAccesses] (
    [Id] int NOT NULL IDENTITY(1,1),
    [OwnerId] int NOT NULL,
    [ProjectId] int NOT NULL,
    [CanSeeProject] bit NOT NULL CONSTRAINT [DF_OwnerProjectAccesses_CanSeeProject] DEFAULT (1),
    [CanChat] bit NOT NULL CONSTRAINT [DF_OwnerProjectAccesses_CanChat] DEFAULT (1),
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_OwnerProjectAccesses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OwnerProjectAccesses_Owners_OwnerId] FOREIGN KEY ([OwnerId])
        REFERENCES [Owners] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OwnerProjectAccesses_Projects_ProjectId] FOREIGN KEY ([ProjectId])
        REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
CREATE UNIQUE INDEX [IX_OwnerProjectAccesses_OwnerId_ProjectId] ON [OwnerProjectAccesses] ([OwnerId], [ProjectId]);
CREATE INDEX [IX_OwnerProjectAccesses_ProjectId] ON [OwnerProjectAccesses] ([ProjectId]);
GO

-- Geriye dönük uyumluluk: mevcut maliklerin sahip olduğu bağımsız bölümlerin projeleri için
-- otomatik erişim kaydı (oylama + sohbet açık) oluşturulur.
INSERT INTO [OwnerProjectAccesses] ([OwnerId], [ProjectId], [CanSeeProject], [CanChat], [CreatedAt])
SELECT DISTINCT u.[OwnerId], u.[ProjectId], 1, 1, GETDATE()
FROM [Units] u
WHERE u.[OwnerId] IS NOT NULL AND u.[IsActive] = 1
  AND NOT EXISTS (
      SELECT 1 FROM [OwnerProjectAccesses] a
      WHERE a.[OwnerId] = u.[OwnerId] AND a.[ProjectId] = u.[ProjectId]
  );
GO

-- ── Migration'ı "uygulanmış" olarak işaretle ────────────────
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260817160000_AddOwnerProjectAccess', N'8.0.0');
GO
