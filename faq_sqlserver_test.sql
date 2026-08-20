-- Sıkça Sorulan Sorular (Faqs) modülü - yerel SQL Server test scripti
-- BrikonYapiDb veritabanına karşı çalıştır (SSMS).
-- Migration Postgres'e özel olduğu için yereldeki SQL Server'da çalışamıyor;
-- bu script tabloyu doğru SQL Server sözdizimiyle oluşturur ve migration'ı
-- "uygulanmış" olarak işaretler (Announcements modülünde yapılan yöntemin aynısı).

CREATE TABLE [Faqs] (
    [Id] int NOT NULL IDENTITY(1,1),
    [Question] nvarchar(300) NOT NULL,
    [Answer] nvarchar(4000) NOT NULL,
    [IsActive] bit NOT NULL,
    [OrderIndex] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Faqs] PRIMARY KEY ([Id])
);

-- Migration'ı "uygulanmış" olarak işaretle:
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260806120000_AddFaqItems', N'8.0.0');
