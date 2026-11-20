-- 2026-11-11 · "Partir de un NPI anterior": cada NPI guarda de cuál partió (Npis.BasedOnNpiId).
-- Columna nueva, vacía en los NPIs que ya existen; no toca datos.
-- Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261111171847_AddNpiBasedOn')
BEGIN
    IF COL_LENGTH(N'Npis', N'BasedOnNpiId') IS NULL
        ALTER TABLE [Npis] ADD [BasedOnNpiId] uniqueidentifier NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111171847_AddNpiBasedOn', N'10.0.11');
END;

COMMIT;
GO
