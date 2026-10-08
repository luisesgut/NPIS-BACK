BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008181941_AddDesignRejections'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [EvidenceAttachmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008181941_AddDesignRejections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008181941_AddDesignRejections', N'10.0.11');
END;

COMMIT;
GO

