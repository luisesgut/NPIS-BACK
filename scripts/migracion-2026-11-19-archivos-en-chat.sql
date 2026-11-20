-- 2026-11-19 · Fotos y archivos en el chat del NPI.
-- Columna nueva en los mensajes con la lista de archivos que lleva cada uno ('[]' en los que ya existen); no toca datos.
-- Idempotente: se puede correr más de una vez. Independiente del script tipo-wicket.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261119155705_AddChatAttachments')
BEGIN
    IF COL_LENGTH(N'NpiComments', N'AttachmentIds') IS NULL
        ALTER TABLE [NpiComments] ADD [AttachmentIds] nvarchar(max) NOT NULL DEFAULT N'[]';

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261119155705_AddChatAttachments', N'10.0.11');
END;

COMMIT;
GO
