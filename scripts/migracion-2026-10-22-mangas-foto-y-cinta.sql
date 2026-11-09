BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022233522_AlignMangaCatalogWithInventory'
)
BEGIN

    DECLARE @MangaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA');
    IF @MangaId IS NOT NULL
    BEGIN
        UPDATE CatalogOptions SET IsActive = 1 WHERE CatalogId = @MangaId AND Value IN (N'680', N'732');
        UPDATE CatalogOptions SET IsActive = 0 WHERE CatalogId = @MangaId AND Value = N'730';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022233522_AlignMangaCatalogWithInventory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022233522_AlignMangaCatalogWithInventory', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023000804_AddColorCintaUniones'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_ColorCintaUniones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023000804_AddColorCintaUniones'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023000804_AddColorCintaUniones', N'10.0.11');
END;

COMMIT;
GO

