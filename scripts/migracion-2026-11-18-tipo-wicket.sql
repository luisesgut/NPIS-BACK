-- 2026-11-18 · Tipo de wicket en la captura del NPI (impresión por dentro, reverso, normal, fondo cuadrado).
-- Columna nueva, vacía en los NPIs que ya existen; no toca datos.
-- Idempotente: se puede correr más de una vez. Independiente del script director-comercial.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261118225412_AddTipoWicket')
BEGIN
    IF COL_LENGTH(N'Npis', N'Capture_TipoWicket') IS NULL
        ALTER TABLE [Npis] ADD [Capture_TipoWicket] nvarchar(200) NOT NULL DEFAULT N'';

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118225412_AddTipoWicket', N'10.0.11');
END;

COMMIT;
GO
