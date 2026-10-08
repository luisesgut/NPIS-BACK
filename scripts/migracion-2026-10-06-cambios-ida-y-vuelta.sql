BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006190404_AddChangeRequestResolution'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [CaptureSnapshotJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006190404_AddChangeRequestResolution'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ChangesAppliedJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006190404_AddChangeRequestResolution'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ResolvedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006190404_AddChangeRequestResolution'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ResolvedByName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006190404_AddChangeRequestResolution'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ResolvedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006190404_AddChangeRequestResolution'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ResponseComment] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006190404_AddChangeRequestResolution'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006190404_AddChangeRequestResolution', N'10.0.11');
END;

COMMIT;
GO

