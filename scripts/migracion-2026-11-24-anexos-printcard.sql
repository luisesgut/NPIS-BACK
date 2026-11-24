-- 2026-11-24 · Anexos del PrintCard (v1.3.0).
-- Columna nueva en las versiones del PrintCard con la lista de anexos de cada una ('[]' en las que ya existen); no toca datos.
-- Idempotente: se puede correr más de una vez. Independiente de los scripts tipo-wicket y archivos-en-chat.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261124183205_AddPrintCardAnnexes')
BEGIN
    IF COL_LENGTH(N'PrintCardVersions', N'AnnexAttachmentIds') IS NULL
        ALTER TABLE [PrintCardVersions] ADD [AnnexAttachmentIds] nvarchar(max) NOT NULL DEFAULT N'[]';

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261124183205_AddPrintCardAnnexes', N'10.0.11');
END;

COMMIT;
GO
