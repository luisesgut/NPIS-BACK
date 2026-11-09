BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016172538_AddTipoCaja'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_TipoCaja] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016172538_AddTipoCaja'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM Catalogs WHERE [Key] = N'TIPO DE CAJA')
    BEGIN
        DECLARE @Id UNIQUEIDENTIFIER = NEWID();
        INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@Id, N'TIPO DE CAJA', N'Tipo de caja');
        INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @Id, N'CAJA KRAFT', 0, 1),
        (NEWID(), @Id, N'CAJA BLANCA', 1, 1),
        (NEWID(), @Id, N'CAJA CON LOGO', 2, 1),
        (NEWID(), @Id, N'N/A', 3, 1);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016172538_AddTipoCaja'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016172538_AddTipoCaja', N'10.0.11');
END;

COMMIT;
GO

