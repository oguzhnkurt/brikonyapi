-- Bağımsız bölüm: kat / oda düzeni / metrekare - yerel SQL Server test scripti
-- BrikonYapiDb veritabanına karşı çalıştır (SSMS).

USE BrikonYapiDb;
GO

ALTER TABLE [Units] ADD
    [FloorNo] int NULL,
    [RoomLayout] nvarchar(20) NULL,
    [AreaM2] int NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260817180000_AddUnitFloorRoomArea', N'8.0.0');
GO
