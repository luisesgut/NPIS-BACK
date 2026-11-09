-- 2026-11-09 · Tabla copiada de Excel/SAP al cargar el PT (columna PtSapTable en Npis).
-- Idempotente: se puede correr más de una vez. Los NPIs existentes quedan sin tabla (NULL).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109172721_AddPtSapTable')
BEGIN
    IF COL_LENGTH('Npis', 'PtSapTable') IS NULL
        ALTER TABLE [Npis] ADD [PtSapTable] nvarchar(max) NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109172721_AddPtSapTable', N'10.0.11');
END;

COMMIT;
GO
