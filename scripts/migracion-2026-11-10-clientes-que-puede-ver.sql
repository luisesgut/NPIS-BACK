-- 2026-11-10 · "Clientes que puede ver": AdminTotal comparte (sólo consulta) los NPIs de ciertos
-- clientes con un usuario normal. Tabla nueva UserClientViews; no toca datos existentes.
-- Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261110185320_AddUserClientViews')
BEGIN
    IF OBJECT_ID(N'[UserClientViews]', N'U') IS NULL
    BEGIN
        CREATE TABLE [UserClientViews] (
            [UserId] uniqueidentifier NOT NULL,
            [Cliente] nvarchar(200) NOT NULL,
            [GrantedByUserId] uniqueidentifier NOT NULL,
            [GrantedAt] datetimeoffset NOT NULL,
            CONSTRAINT [PK_UserClientViews] PRIMARY KEY ([UserId], [Cliente]),
            CONSTRAINT [FK_UserClientViews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
        );
    END;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110185320_AddUserClientViews', N'10.0.11');
END;

COMMIT;
GO
