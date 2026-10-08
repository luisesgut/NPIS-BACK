BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194616_AddCatalogChangeLog'
)
BEGIN
    CREATE TABLE [CatalogChangeLogs] (
        [Id] uniqueidentifier NOT NULL,
        [CatalogId] uniqueidentifier NOT NULL,
        [CatalogKey] nvarchar(50) NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [OldValue] nvarchar(150) NULL,
        [NewValue] nvarchar(150) NULL,
        [ChangedByUserId] uniqueidentifier NOT NULL,
        [ChangedByName] nvarchar(200) NOT NULL,
        [ChangedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CatalogChangeLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CatalogChangeLogs_Catalogs_CatalogId] FOREIGN KEY ([CatalogId]) REFERENCES [Catalogs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194616_AddCatalogChangeLog'
)
BEGIN
    CREATE INDEX [IX_CatalogChangeLogs_CatalogId_ChangedAt] ON [CatalogChangeLogs] ([CatalogId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194616_AddCatalogChangeLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008194616_AddCatalogChangeLog', N'10.0.11');
END;

COMMIT;
GO

