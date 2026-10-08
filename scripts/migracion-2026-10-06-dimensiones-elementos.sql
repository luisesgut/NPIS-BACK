BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_CierreDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_ColgaderaDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_MuescaDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_OtrosElementosDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_ParcheRefuerzoDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_PatronPerforadoDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_PrecorteDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_SuajeDimensiones] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154056_AddElementDimensions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006154056_AddElementDimensions', N'10.0.11');
END;

COMMIT;
GO

