-- 2026-11-04 · Solicitudes de cambios y de información:
--   1) Capturas de pantalla / fotos adjuntas (ImageAttachmentIdsCsv en ChangeRequests e InformationRequests).
--   2) Retirar una solicitud de cambios antes de que Comercial la atienda (WithdrawnAt en ChangeRequests).
-- Idempotente: se puede correr más de una vez. Lo existente queda sin imágenes y sin retirar (NULL).
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261104223850_AddRequestImages')
BEGIN
    IF COL_LENGTH('InformationRequests', 'ImageAttachmentIdsCsv') IS NULL
        ALTER TABLE [InformationRequests] ADD [ImageAttachmentIdsCsv] nvarchar(400) NULL;
    IF COL_LENGTH('ChangeRequests', 'ImageAttachmentIdsCsv') IS NULL
        ALTER TABLE [ChangeRequests] ADD [ImageAttachmentIdsCsv] nvarchar(400) NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104223850_AddRequestImages', N'10.0.11');
END;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261104225041_AddChangeRequestWithdrawnAt')
BEGIN
    IF COL_LENGTH('ChangeRequests', 'WithdrawnAt') IS NULL
        ALTER TABLE [ChangeRequests] ADD [WithdrawnAt] datetimeoffset NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104225041_AddChangeRequestWithdrawnAt', N'10.0.11');
END;

COMMIT;
GO
