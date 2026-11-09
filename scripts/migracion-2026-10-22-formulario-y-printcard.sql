BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022180319_AddNombreExtranjeroYClasificacionArte'
)
BEGIN
    ALTER TABLE [Npis] ADD [ArtClassification] nvarchar(2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022180319_AddNombreExtranjeroYClasificacionArte'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_NombreExtranjeroProducto] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022180319_AddNombreExtranjeroYClasificacionArte'
)
BEGIN
    ALTER TABLE [Npis] ADD [HasLabValues] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022180319_AddNombreExtranjeroYClasificacionArte'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022180319_AddNombreExtranjeroYClasificacionArte', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022195715_AddPrintCardRejectionAndClientChecklist'
)
BEGIN
    ALTER TABLE [PrintCardVersions] ADD [RejectedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022195715_AddPrintCardRejectionAndClientChecklist'
)
BEGIN
    ALTER TABLE [PrintCardVersions] ADD [RejectedByName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022195715_AddPrintCardRejectionAndClientChecklist'
)
BEGIN
    ALTER TABLE [PrintCardVersions] ADD [RejectedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022195715_AddPrintCardRejectionAndClientChecklist'
)
BEGIN
    ALTER TABLE [PrintCardVersions] ADD [RejectionEvidenceAttachmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022195715_AddPrintCardRejectionAndClientChecklist'
)
BEGIN
    ALTER TABLE [PrintCardVersions] ADD [RejectionReason] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022195715_AddPrintCardRejectionAndClientChecklist'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ChecklistJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022195715_AddPrintCardRejectionAndClientChecklist'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022195715_AddPrintCardRejectionAndClientChecklist', N'10.0.11');
END;

COMMIT;
GO

