-- Duyurular (Announcements) modulu - yerel SQL Server test scripti
-- BrikonYapiDb veritabanina karsi calistir (SSMS).
-- Migration Postgres'e ozel oldugu icin yereldeki SQL Server'da calisamiyor;
-- bu script tabloyu dogru SQL Server sozdizimiyle olusturur ve migration'i
-- "uygulanmis" olarak isaretler (daha once Kat Malikleri modulunde yapilan yontemin aynisi).

CREATE TABLE [Announcements] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Title] nvarchar(150) NOT NULL,
    [Body] nvarchar(1000) NOT NULL,
    [Tag] nvarchar(40) NULL,
    [IsActive] bit NOT NULL,
    [OrderIndex] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Announcements] PRIMARY KEY ([Id])
);

-- Migration'i "uygulanmis" olarak isaretle:
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260801140337_AddAnnouncements', N'8.0.0');
