BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022232626_AddMangaPrensaLabioYPrecortes'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_Labio] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022232626_AddMangaPrensaLabioYPrecortes'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_PrecorteWicket] nvarchar(200) NOT NULL DEFAULT N'NO';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022232626_AddMangaPrensaLabioYPrecortes'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_PrecorteZipper] nvarchar(200) NOT NULL DEFAULT N'NO';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022232626_AddMangaPrensaLabioYPrecortes'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_PrensaManga] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022232626_AddMangaPrensaLabioYPrecortes'
)
BEGIN

    DECLARE @MangaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA');
    IF @MangaId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM CatalogOptions WHERE CatalogId = @MangaId AND Value = N'730')
            INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive)
            VALUES (NEWID(), @MangaId, N'730', (SELECT DisplayOrder FROM CatalogOptions WHERE CatalogId = @MangaId AND Value = N'720'), 1);
        UPDATE CatalogOptions SET IsActive = 0 WHERE CatalogId = @MangaId AND Value IN (N'680', N'732');
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022232626_AddMangaPrensaLabioYPrecortes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022232626_AddMangaPrensaLabioYPrecortes', N'10.0.11');
END;

COMMIT;
GO

