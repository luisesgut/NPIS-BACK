BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183236_AddCaptureHistory'
)
BEGIN
    ALTER TABLE [Npis] ADD [FirstSubmittedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183236_AddCaptureHistory'
)
BEGIN
    ALTER TABLE [Npis] ADD [FirstSubmittedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183236_AddCaptureHistory'
)
BEGIN
    ALTER TABLE [Npis] ADD [InitialCaptureJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183236_AddCaptureHistory'
)
BEGIN
    CREATE TABLE [NpiCaptureChanges] (
        [Id] uniqueidentifier NOT NULL,
        [NpiId] uniqueidentifier NOT NULL,
        [Field] nvarchar(100) NOT NULL,
        [Before] nvarchar(2000) NOT NULL,
        [After] nvarchar(2000) NOT NULL,
        [ChangedByUserId] uniqueidentifier NOT NULL,
        [ChangedByName] nvarchar(200) NOT NULL,
        [ChangedAt] datetimeoffset NOT NULL,
        [ChangeRequestId] uniqueidentifier NULL,
        [WasRequested] bit NOT NULL,
        CONSTRAINT [PK_NpiCaptureChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NpiCaptureChanges_Npis_NpiId] FOREIGN KEY ([NpiId]) REFERENCES [Npis] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183236_AddCaptureHistory'
)
BEGIN
    CREATE INDEX [IX_NpiCaptureChanges_NpiId_ChangedAt] ON [NpiCaptureChanges] ([NpiId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008183236_AddCaptureHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008183236_AddCaptureHistory', N'10.0.11');
END;

COMMIT;
GO

