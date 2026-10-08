BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008171325_AddNpiChat'
)
BEGIN
    ALTER TABLE [NpiComments] ADD [MentionedUserIds] nvarchar(max) NOT NULL DEFAULT N'[]';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008171325_AddNpiChat'
)
BEGIN
    ALTER TABLE [Notifications] ADD [GroupKey] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008171325_AddNpiChat'
)
BEGIN
    CREATE TABLE [NpiChatReads] (
        [NpiId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LastReadAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_NpiChatReads] PRIMARY KEY ([NpiId], [UserId]),
        CONSTRAINT [FK_NpiChatReads_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NpiChatReads_Npis_NpiId] FOREIGN KEY ([NpiId]) REFERENCES [Npis] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008171325_AddNpiChat'
)
BEGIN
    CREATE INDEX [IX_NpiChatReads_UserId] ON [NpiChatReads] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008171325_AddNpiChat'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008171325_AddNpiChat', N'10.0.11');
END;

COMMIT;
GO

