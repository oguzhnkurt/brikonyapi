-- Hakediş taksitlerini inşaat aşamalarına bağlama - yerel SQL Server test scripti
-- BrikonYapiDb veritabanına karşı çalıştır (SSMS).
-- Migration Postgres'e özel olduğu için yereldeki SQL Server'da çalışamıyor;
-- bu script aynı sütun/FK'yı doğru SQL Server sözdizimiyle ekler ve migration'ı
-- "uygulanmış" olarak işaretler.

USE BrikonYapiDb;
GO

ALTER TABLE [PaymentSchedules] ADD
    [ProjectStageId] int NULL;
GO

CREATE INDEX [IX_PaymentSchedules_ProjectStageId] ON [PaymentSchedules] ([ProjectStageId]);
GO

-- NOT: SET NULL burada "multiple cascade paths" hatası verir (Msg 1785) çünkü PaymentSchedules'a
-- Project'ten hem Unit üzerinden hem de ProjectStage üzerinden iki farklı yol var. SQL Server'da
-- NO ACTION kullanılır; temizlik uygulama katmanında yapılır (bkz. ProjectProgressController.DeleteStage).
ALTER TABLE [PaymentSchedules] ADD CONSTRAINT [FK_PaymentSchedules_ProjectStages_ProjectStageId]
    FOREIGN KEY ([ProjectStageId]) REFERENCES [ProjectStages] ([Id]) ON DELETE NO ACTION;
GO

-- ── Migration'ı "uygulanmış" olarak işaretle ────────────────
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260820150000_AddProjectStageToPaymentSchedule', N'8.0.0');
GO
