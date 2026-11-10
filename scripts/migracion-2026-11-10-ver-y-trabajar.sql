-- 2026-11-10 · "Ver y trabajar": un cliente compartido puede ser sólo de consulta o también para
-- trabajar sus NPIs. Columna nueva UserClientViews.CanWork (en 0 = sólo ver, como hasta ahora).
-- REQUIERE haber corrido antes migracion-2026-11-10-clientes-que-puede-ver.sql (crea la tabla).
-- Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261110221617_AddClientViewCanWork')
BEGIN
    IF COL_LENGTH(N'UserClientViews', N'CanWork') IS NULL
        ALTER TABLE [UserClientViews] ADD [CanWork] bit NOT NULL CONSTRAINT [DF_UserClientViews_CanWork] DEFAULT CAST(0 AS bit);

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_UserClientViews_Cliente' AND [object_id] = OBJECT_ID(N'[UserClientViews]'))
        CREATE INDEX [IX_UserClientViews_Cliente] ON [UserClientViews] ([Cliente]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110221617_AddClientViewCanWork', N'10.0.11');
END;

COMMIT;
GO
