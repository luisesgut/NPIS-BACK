-- 2026-11-09 · Libreta de correos de cada vendedor por cliente ("Enviar al cliente" por Outlook).
-- Tabla nueva ClientRecipients; no toca datos existentes.
-- Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109194537_AddClientRecipients')
BEGIN
    IF OBJECT_ID(N'[ClientRecipients]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ClientRecipients] (
            [Id] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [Cliente] nvarchar(200) NOT NULL,
            [Email] nvarchar(254) NOT NULL,
            [Kind] nvarchar(2) NOT NULL,
            [Selected] bit NOT NULL,
            [UpdatedAt] datetimeoffset NOT NULL,
            CONSTRAINT [PK_ClientRecipients] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_ClientRecipients_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX [IX_ClientRecipients_UserId_Cliente_Email] ON [ClientRecipients] ([UserId], [Cliente], [Email]);
    END;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109194537_AddClientRecipients', N'10.0.11');
END;

COMMIT;
GO
