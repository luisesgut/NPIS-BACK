-- =====================================================================================
-- Migración 2026-11-03 — PrintCard y solicitudes de cambios
-- (migraciones EF 20261102142017_AddPrintCardCommercialChecklist y 20261102144224_AddChangeRequestFieldNotes)
--
-- Agrega dos columnas, ambas nvarchar(max) NULL:
--   * PrintCardVersions.CommercialChecklistJson: puntos que Diseño pide a Comercial revisar con el
--     cliente; Comercial los palomea antes de registrar la aprobación del cliente.
--   * ChangeRequests.FieldNotesJson: el comentario de Ingeniería/Diseño para cada campo que marcan
--     para corregir; Comercial lo ve junto al campo.
-- No modifica ni borra datos existentes. Es idempotente: se puede correr dos veces.
--
-- Requisito: que ya esté aplicada 20261030181508_AddClosureProposals. Para revisarlo:
--   SELECT TOP 3 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
--
-- Publicar el backend nuevo DESPUÉS de correr este script.
-- =====================================================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102142017_AddPrintCardCommercialChecklist'
)
BEGIN
    ALTER TABLE [PrintCardVersions] ADD [CommercialChecklistJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102142017_AddPrintCardCommercialChecklist'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102142017_AddPrintCardCommercialChecklist', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102144224_AddChangeRequestFieldNotes'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [FieldNotesJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102144224_AddChangeRequestFieldNotes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102144224_AddChangeRequestFieldNotes', N'10.0.11');
END;

COMMIT;
GO

